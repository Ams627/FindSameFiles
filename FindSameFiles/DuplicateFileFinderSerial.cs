using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FindSameFiles
{
    class DuplicateFileFinderSerial
    {
        private readonly Dictionary<long, List<FilenameAndHash>> _lengthToFilenames = new Dictionary<long, List<FilenameAndHash>>();
        private readonly BlockingCollection<FilenameAndHash> _hashQueue = new BlockingCollection<FilenameAndHash>();
        private List<string> _errors;

        public DuplicateFileFinderSerial()
        {
        }

        /// <summary>
        /// Returns a list of list of strings - each entry in the outer list contains a list of filenames of files with the same contents.
        /// </summary>
        /// <param name="startingDirectory">The directory in which to start searching</param>
        /// <returns>A list of list of files whose contents are the same</returns>
        public List<List<string>> GetDups(string startingDirectory, out List<string> errors)
        {
            _errors = new List<string>();

            var stack = new Stack<string>();
            stack.Push(startingDirectory);

            while (stack.Any())
            {
                var dir = stack.Pop();
                foreach (var file in Directory.GetFiles(dir))
                {
                    var finfo = new FileInfo(file);
                    if (!_lengthToFilenames.TryGetValue(finfo.Length, out var fileList))
                    {
                        fileList = new List<FilenameAndHash>();
                        _lengthToFilenames.Add(finfo.Length, fileList);
                    }
                    fileList.Add(new FilenameAndHash { Filename = file });

                    // only compute the SHA-1 for the file if this is the second or subsequent file with this particular length.
                    // If this is the second file we also need to compute the SHA-1 for the first file:

                    if (fileList.Count > 1)
                    {
                        if (fileList.Count == 2)
                        {
                            CalculateHash(fileList.First());
                        }
                        CalculateHash(fileList.Last());
                    }
                }

                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    stack.Push(subDir);
                }
            }

            var sha1Comparer = new HashEqualityComparer();
            var duplicateEntriesByLength = _lengthToFilenames.Where(x => x.Value.Count > 1);

            // transform the length dictionary into an IEnumerable<ILookup<byte[]...note that we lose the dictionary key as
            // we are no longer interested in the length of the file. Also note that we check if a file actually **has** a computed
            // hash as it may not due to our inability to open it:
            var shaLookups = duplicateEntriesByLength.Select(x => x.Value.Where(y => y.HasComputedHash).GroupBy(z => z.Sha1, sha1Comparer)).ToList();

            // transform each lookup into a list of lists (each "bucket in the lookup becomes a list) - so we then 
            // we flatten the entire result to a list-of-lists - we could have done this with SelectMany:

            var dups = (from lookup in shaLookups       // each entry is a lookup "bucketed" by SHA1
                        from lubucket in lookup         // take each bucket where the number of items in the bucket is greater than 1
                        where lubucket.Count() > 1
                        let fileList = (from file in lubucket select file.Filename).ToList() // get all the items from the bucket, but we want ONLY THE FILENAME for each item
                        select fileList).ToList();

            errors = _errors;
            return dups;
        }

        private void CalculateHash(FilenameAndHash filenameAndHash)
        {
            try
            {
                filenameAndHash.Sha1 = GetSha1Sum(filenameAndHash.Filename);
                filenameAndHash.HasComputedHash = true;
            }
            catch (Exception)
            {
                _errors.Add(filenameAndHash.Filename);
            }
        }


        /// <summary>
        /// This function is run using Task.Run - i.e. it's basically a thread performing a Sha1 computation for any files in the queue
        /// </summary>
        private void HashEngine()
        {
            foreach (var item in _hashQueue.GetConsumingEnumerable())
            {
                try
                {
                    item.Sha1 = GetSha1Sum(item.Filename);
                    item.HasComputedHash = true;
                }
                catch (Exception)
                {
                    _errors.Add(item.Filename);
                }
            }
        }

        private static byte[] GetSha1Sum(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open))
            using (BufferedStream bs = new BufferedStream(fs))
            {
                using (SHA1Managed sha1 = new SHA1Managed())
                {
                    byte[] hash = sha1.ComputeHash(bs);
                    return hash;
                }
            }
        }

        class FilenameAndHash
        {
            public string Filename { get; set; }
            public byte[] Sha1 { get; set; }
            public bool HasComputedHash { get; set; } = false;
        }

        class HashEqualityComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] x, byte[] y)
            {
                return x.SequenceEqual(y);
            }

            public int GetHashCode(byte[] x)
            {
                return x.Length.GetHashCode();
            }
        }

    }
}