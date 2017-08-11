using System;
using System.Collections.Generic;
using System.IO;

namespace ComparisonCleanup
{
    class Program
    {
        private static readonly HashSet<string> DirectoriesToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin",
            "obj",
            ".vs"
        };

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                UsageMessage();
                return;
            }

            string baseDir = args[0];
            if (!Directory.Exists(baseDir))
            {
                Console.WriteLine($"directory '{baseDir}' doesn't exist");
                return;
            }

            IList<string> toRemove = new List<string>();
            foreach (string directory in Directory.EnumerateDirectories(baseDir, "*", SearchOption.AllDirectories))
            {
                string dirName = Path.GetFileName(directory);

                if (DirectoriesToRemove.Contains(dirName))
                {
                    toRemove.Add(directory);
                    Console.WriteLine($"going to delete: {directory}");
                }
            }

            if (toRemove.Count == 0)
            {
                Console.WriteLine("Nothing found to delete...exiting");
                return;
            }

            Console.WriteLine("Actually delete (Y/N)?");
            string confirmation = Console.ReadLine();
            if (!string.Equals(confirmation, "Y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Not deleting");
                return;
            }
            else
            {
                Console.WriteLine("Proceeding with deletes...");
            }

            foreach (string dirToRemove in toRemove)
            {
                Directory.Delete(dirToRemove, true);
            }
        }

        private static void UsageMessage()
        {
            Console.WriteLine("Usage: ComparisonCleanup <base dir>");
        }
    }
}
