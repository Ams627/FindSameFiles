using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FindSameFiles
{
    class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                var parallel = args.Length == 0 || args[0] != "s";

                var dir = Directory.GetCurrentDirectory();
                var searcher = new DuplicateFileFinder(parallel);

                var t0 = DateTime.Now.Ticks;

                var duplicates = searcher.GetDups(dir, out var errors);

                var t1 = DateTime.Now.Ticks;
                var totalMilliSeconds = (t1 - t0) / 10_000;
                Console.Error.WriteLine($"{totalMilliSeconds} ms");

                File.WriteAllLines($"dups.errors.txt", errors);

                foreach (var dupBucket in duplicates)
                {
                    foreach (var filename in dupBucket)
                    {
                        Console.WriteLine($"{filename}");
                    }
                    Console.WriteLine($"--------------------");
                }

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
