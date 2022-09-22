// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.TestHelper
{
    public static class TestUtils
    {
        public static string CreateTemporaryFolder(string name = "")
        {
            string workingDir = Path.Combine(Path.GetTempPath(), "TemplateEngine.Tests", Guid.NewGuid().ToString(), name);
            Directory.CreateDirectory(workingDir);
            return workingDir;
        }

        public static void SetupNuGetConfigForPackagesLocation(string projectDirectory, string packagesLocation)
        {
            string nugetConfigShim =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <config>
    <add key=""globalPackagesFolder"" value=""{CreateTemporaryFolder("Packages")}"" />
  </config>
  <packageSources>
    <clear />
    <add key=""testPackages"" value=""{packagesLocation}"" />
  </packageSources>
</configuration>";

            File.WriteAllText(Path.Combine(projectDirectory, "nuget.config"), nugetConfigShim);
        }

        public static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + dir.FullName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }

        public static bool CompareFiles(string file1, string file2)
        {
            using var fs1 = new FileStream(file1, FileMode.Open, FileAccess.Read);
            using var fs2 = new FileStream(file2, FileMode.Open, FileAccess.Read);
            if (fs1.Length != fs2.Length)
            {
                return false;
            }

            int file1byte;
            int file2byte;
            do
            {
                file1byte = fs1.ReadByte();
                file2byte = fs2.ReadByte();
            }
            while ((file1byte == file2byte) && (file1byte != -1));
            return (file1byte - file2byte) == 0;
        }

        public static async Task<T> AttemptSearch<T, TEx>(int count, TimeSpan interval, Func<Task<T>> execute) where TEx : Exception
        {
            T? result = default;
            int attempt = 0;
            while (attempt < count)
            {
                try
                {
                    result = await execute();
                    break;
                }
                catch (Exception ex)
                {
                    if (attempt + 1 == count)
                    {
                        throw ex;
                    }

                    if (ex is AggregateException agEx)
                    {
                        if (!agEx.InnerExceptions.Any(e => e is TEx))
                        {
                            throw ex;
                        }
                    }
                    else if (ex is not TEx)
                    {
                        throw ex;
                    }
                }
                await Task.Delay(interval);
                attempt++;
            }
            return result!;
        }
    }
}
