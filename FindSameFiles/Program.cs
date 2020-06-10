using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FindSameFiles
{
    static class DupSearcher
    {

        class HashComparer : IEqualityComparer<byte[]>
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

        public static List<List<string>> GetDups(string startingDirectory)
        {
            var fileList = new List<(string filename, long length)>();
            var stack = new Stack<string>();
            stack.Push(startingDirectory);

            while (stack.Any())
            {
                var dir = stack.Pop();
                foreach (var file in Directory.GetFiles(dir))
                {
                    var finfo = new FileInfo(file);
                    fileList.Add((file, finfo.Length));
                }

                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    stack.Push(subDir);
                }
            }
            Console.WriteLine("done1");

            // make dups: each entry contains a list of files whose length is the same:
            var dups = fileList.ToLookup(x => x.length).Where(y => y.Count() > 1);

            // 124876=>{(file1.c, 124876), (file2.c, 124876), (file3.c, 124876)}
            // 129741=>{(file4.c, 129741), (file4.c, 129741), (file4.c, 129741)}

            var d2 = dups.Select(x => x.Select(y => y));

            var hashLookup = dups.Select(x => x.Select(y => (y.filename, hash: GetSha1Sum(y.filename))).ToLookup(z => z.hash, new HashComparer()));

            var duplicateNames = from lengthGroup in hashLookup
                                 from hashgroup in lengthGroup
                                 from entry in hashgroup
                                 let kprint = string.Join("-", hashgroup.Key.Select(x => $"{x:X2}"))
                                 select entry.filename;


            foreach (var lengthGroup in hashLookup)
            {
                foreach (var hashGroup in lengthGroup)
                {
                    var kprint = string.Join("-", hashGroup.Key.Select(x => $"{x:X2}"));
                    Console.WriteLine($"{kprint}");
                    foreach (var (filename, hash) in hashGroup)
                    {
                        Console.WriteLine($"{filename}");
                    }
                }
            }

            return null;
        }

        private static byte[] GetSha1Sum(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open))
            using (BufferedStream bs = new BufferedStream(fs, 65536))
            {
                using (SHA1Managed sha1 = new SHA1Managed())
                {
                    byte[] hash = sha1.ComputeHash(bs);
                    return hash;
                }
            }
        }

    }

    class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                var dir = Directory.GetCurrentDirectory();
                var searcher = new DuplicateFileFinder();

                var t0 = DateTime.Now.Ticks;

                var dups2 = searcher.GetDups(dir);
                foreach (var dupBucket in dups2)
                {
                    foreach (var filename in dupBucket)
                    {
                        Console.WriteLine($"{filename}");
                    }
                    Console.WriteLine($"--------------------");
                }

                var t1 = DateTime.Now.Ticks;

                var totalMilliSeconds = (t1 - t0) / 10_000;
                Console.WriteLine($"{totalMilliSeconds} ms");
            }
            catch (AggregateException ex)
            {
                Console.WriteLine($"{ex}");
            }
            catch (Exception ex)
            {
                var fullname = System.Reflection.Assembly.GetEntryAssembly().Location;
                var progname = Path.GetFileNameWithoutExtension(fullname);
                Console.Error.WriteLine(progname + ": Error: " + ex.Message);
            }

        }
    }
}
