// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if !NETFRAMEWORK
using System.Formats.Tar;
#endif
using System.IO.Compression;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Extracts a .zip or .tar.gz file to a directory.
    /// </summary>
    public sealed class ExtractArchiveToDirectory : ToolTask
    {
        /// <summary>
        /// The path to the archive to extract.
        /// </summary>
        [Required]
        public string SourceArchive { get; set; }

        /// <summary>
        /// The path of the directory to which the archive should be extracted.
        /// </summary>
        [Required]
        public string DestinationDirectory { get; set; }

        /// <summary>
        /// Indicates if the destination directory should be cleaned if it already exists.
        /// </summary>
        public bool CleanDestination { get; set; }

        /// <summary>
        /// A list of directories, relative to the root of the archive to include. If empty all directories will be copied.
        /// </summary>
        public ITaskItem[] DirectoriesToCopy { get; set; }

        protected override bool ValidateParameters()
        {
            base.ValidateParameters();

            var retVal = true;

            if (Directory.Exists(DestinationDirectory) && CleanDestination == true)
            {
                Log.LogMessage(MessageImportance.Low, "'{0}' already exists, trying to delete before unzipping...", DestinationDirectory);
                Directory.Delete(DestinationDirectory, recursive: true);
            }

            if (!File.Exists(SourceArchive))
            {
                Log.LogError($"SourceArchive '{SourceArchive} does not exist.");

                retVal = false;
            }

            if (retVal)
            {
                Log.LogMessage($"Creating Directory {DestinationDirectory}");
                Directory.CreateDirectory(DestinationDirectory);
            }
            
            return retVal;
        }

        public override bool Execute()
        {
            bool retVal = true;
            bool isZipArchive = Path.GetExtension(SourceArchive).Equals(".zip", StringComparison.OrdinalIgnoreCase);
            bool isTarballArchive = SourceArchive.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase);

            //  Inherits from ToolTask in order to shell out to tar for complete extraction
            //  If the file is a .zip, then don't call the base Execute method, just run as a normal task
            //  If the file is a .tar.gz, and DirectoriesToCopy isn't empty, also run a normal task.
            if (isZipArchive || isTarballArchive)
            {
                if (ValidateParameters())
                {
                    if (DirectoriesToCopy != null && DirectoriesToCopy.Length != 0)
                    {
                        // Partial archive extraction
                        if (isZipArchive)
                        {
                            using var zip = new ZipArchive(File.OpenRead(SourceArchive));
                            foreach (var entry in zip.Entries)
                            {
                                if (ShouldExtractItem(entry.FullName))
                                {
                                    string destinationPath = Path.Combine(DestinationDirectory, entry.FullName); // codeql [SM02729] This is checked in the CheckDestinationPath method below before being used
                                    string destinationFileName = GetFullDirectoryPathWithSeperator(destinationPath);
                                    string fullDestDirPath = GetFullDirectoryPathWithSeperator(DestinationDirectory);

                                    CheckDestinationPath(destinationFileName, fullDestDirPath);

                                    if (!Directory.Exists(Path.Combine(DestinationDirectory, Path.GetDirectoryName(entry.FullName))))
                                    {
                                        Directory.CreateDirectory(Path.Combine(DestinationDirectory, Path.GetDirectoryName(entry.FullName)));
                                    }

                                    Log.LogMessage(Path.GetDirectoryName(entry.FullName));
                                    entry.ExtractToFile(destinationPath);
                                }
                            }
                        }
                        else
                        {
#if NETFRAMEWORK
                            // Run the base tool, which uses external 'tar' command
                            retVal = base.Execute();
#else
                            // Decompress GZip content
                            using FileStream compressedFileStream = File.OpenRead(SourceArchive);
                            using var decompressor = new GZipStream(compressedFileStream, CompressionMode.Decompress);
                            using var decompressedStream = new MemoryStream();
                            decompressor.CopyTo(decompressedStream);
                            decompressedStream.Seek(0, SeekOrigin.Begin);

                            // Extract Tar content
                            using TarReader tr = new TarReader(decompressedStream);
                            while (tr.GetNextEntry() is TarEntry tarEntry)
                            {
                                if (tarEntry.EntryType != TarEntryType.Directory)
                                {
                                    string entryName = tarEntry.Name;
                                    entryName = entryName.StartsWith("./") ? entryName[2..] : entryName;
                                    if (ShouldExtractItem(entryName))
                                    {
                                        string destinationPath = Path.Combine(DestinationDirectory, entryName);
                                        string destinationFileName = GetFullDirectoryPathWithSeperator(destinationPath);
                                        string fullDestDirPath = GetFullDirectoryPathWithSeperator(DestinationDirectory);

                                        CheckDestinationPath(destinationFileName, fullDestDirPath);

                                        Log.LogMessage(entryName);

                                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                                        tarEntry.ExtractToFile(destinationPath, overwrite: true);
                                    }
                                }
                            }
#endif
                        }
                    }
                    else
                    {
                        // Complete archive extraction
                        if (isZipArchive)
                        {
#if NETFRAMEWORK
                            //  .NET Framework doesn't have overload to overwrite files
                            ZipFile.ExtractToDirectory(SourceArchive, DestinationDirectory);
#else

                            ZipFile.ExtractToDirectory(SourceArchive, DestinationDirectory, overwriteFiles: true);
#endif
                        }
                        else
                        {
                            // Run the base tool, which uses external 'tar' command
                            retVal = base.Execute();
                        }
                    }
                }
                else
                {
                    retVal = false;
                }
            }
            else
            {
                retVal = base.Execute();
            }

            if (!retVal)
            {
                Log.LogMessage($"Deleting Directory {DestinationDirectory}");
                Directory.Delete(DestinationDirectory);
            }

            return retVal;
        }

        private string GetFullDirectoryPathWithSeperator(string directory)
        {
            string fullDirectoryPath = Path.GetFullPath(directory);

            if (!fullDirectoryPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                fullDirectoryPath = string.Concat(fullDirectoryPath, Path.DirectorySeparatorChar);
            }

            return fullDirectoryPath;
        }

        private void CheckDestinationPath(string destinationFileName, string fullDestDirPath)
        {
            if (!destinationFileName.StartsWith(fullDestDirPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new System.InvalidOperationException("Entry is outside the target dir: " + destinationFileName);
            }
        }

        private bool ShouldExtractItem(string path) => DirectoriesToCopy?.Any(p => path.StartsWith(p.ItemSpec)) ?? false;

        protected override string ToolName => "tar";

        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

        protected override string GenerateFullPathToTool() => "tar";

        protected override string GenerateCommandLineCommands() => $"xf {SourceArchive} -C {DestinationDirectory}";
    }
}
