// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
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
        /// A list of directories, semicolon deliminated, relative to the root of the archive to include. If empty all directories will be copied.
        /// </summary>
        public ITaskItem[] DirectoriesToCopy { get; set; }

        protected override bool ValidateParameters()
        {
            base.ValidateParameters();

            var retVal = true;

            if (Directory.Exists(DestinationDirectory))
            {
                if (CleanDestination == true)
                {
                    Log.LogMessage(MessageImportance.Low, "'{0}' already exists, trying to delete before unzipping...", DestinationDirectory);
                    Directory.Delete(DestinationDirectory, recursive: true);
                }
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

            //  Inherits from ToolTask in order to shell out to tar.
            //  If the file is a .zip, then don't call the base Execute method, just run as a normal task
            if (Path.GetExtension(SourceArchive).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                if (ValidateParameters())
                {
                    if (DirectoriesToCopy != null && DirectoriesToCopy.Length != 0)
                    {
                        var zip = new ZipArchive(File.OpenRead(SourceArchive));
                        string loc = DestinationDirectory;
                        foreach (var entry in zip.Entries)
                        {
                            foreach (var directory in DirectoriesToCopy)
                            {
                                if (entry.FullName.StartsWith(directory.ItemSpec))
                                {
                                    if (!Directory.Exists(Path.Combine(DestinationDirectory, Path.GetDirectoryName(entry.FullName))))
                                    {
                                        Directory.CreateDirectory(Path.Combine(DestinationDirectory, Path.GetDirectoryName(entry.FullName)));
                                    }

                                    Log.LogMessage(Path.GetDirectoryName(entry.FullName));
                                    entry.ExtractToFile(Path.Combine(loc, entry.FullName));
                                }
                            }
                        }
                    }
                    else
                    { 
#if NETFRAMEWORK
                        //  .NET Framework doesn't have overload to overwrite files
                        ZipFile.ExtractToDirectory(SourceArchive, DestinationDirectory);
#else

                        ZipFile.ExtractToDirectory(SourceArchive, DestinationDirectory, overwriteFiles: true);
#endif
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

        protected override string ToolName
        {
            get { return "tar"; }
        }

        protected override MessageImportance StandardOutputLoggingImportance
        {
            get { return MessageImportance.High; } // or else the output doesn't get logged by default
        }

        protected override string GenerateFullPathToTool()
        {
            return "tar";
        }

        protected override string GenerateCommandLineCommands()
        {
            return $"xf {SourceArchive} -C {DestinationDirectory}";
        }
    }
}
