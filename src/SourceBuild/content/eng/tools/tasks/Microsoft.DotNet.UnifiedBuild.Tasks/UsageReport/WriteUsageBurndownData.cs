// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.UsageReport
{
    public class WriteUsageBurndownData : Task
    {
        /// <summary>
        /// Specifies the root directory for git.
        /// Note: Requires a trailing "/" when specifying the directory.
        /// </summary>
        [Required]
        public string RootDirectory { get; set; }

        /// <summary>
        /// Specifies the path to the prebuilt baseline file
        /// to be used to generate the burndown.
        /// </summary>
        [Required]
        public string PrebuiltBaselineFile { get; set; }

        /// <summary>
        /// Output data CSV file. 
        /// </summary>
        [Required]
        public string OutputFilePath { get; set; }

        /// <summary>
        ///  Sends HTTP requests and receives HTTP responses.
        /// </summary>
        private readonly HttpClient client = new();

        public override bool Execute() => ExecuteAsync().GetAwaiter().GetResult();

        private async Task<bool> ExecuteAsync()
        {
            string baselineRelativeFileName = PrebuiltBaselineFile.Replace(RootDirectory, "");
            string gitLogCommand = $"log --first-parent --pretty=format:%H,%f,%ci -- {PrebuiltBaselineFile}";

            DateTime startTime = DateTime.Now;
            Log.LogMessage(MessageImportance.High, "Generating summary usage burndown data...");


            IEnumerable<Task<Commit>> getCommitTasks = ExecuteGitCommand(RootDirectory, gitLogCommand)
                .Select(async commitLine =>
                {
                    var splitLine = commitLine.Split(',');
                    var commit = new Commit()
                    {
                        Sha = splitLine[0],
                        Title = splitLine[1],
                        CommitDate = DateTime.Parse(splitLine[2])
                    };
                    string fileContents = await GetFileContentsAsync(baselineRelativeFileName, commit.Sha);
                    Usage[] usages = UsageData.Parse(XElement.Parse(fileContents)).Usages.NullAsEmpty().ToArray();
                    commit.PackageVersionCount = usages.Count();
                    commit.PackageCount = usages.GroupBy(i => i.PackageIdentity.Id).Select(grp => grp.First()).Count();
                    return commit;
                });

            Commit[] commits = await System.Threading.Tasks.Task.WhenAll(getCommitTasks);
            IEnumerable<string> data = commits.Select(c => c.ToString());
    
            Directory.CreateDirectory(Path.GetDirectoryName(OutputFilePath));

            File.WriteAllLines(OutputFilePath, data);

            Log.LogMessage(
                MessageImportance.High,
                $"Generating summary usage burndown data at {OutputFilePath} done. Took {DateTime.Now - startTime}");

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Get the contents of a git file based on the commit sha.
        /// </summary>
        /// <param name="relativeFilePath">The relative path (from the git root) to the file.</param>
        /// <param name="commitSha">The commit sha for the version of the file to get.</param>
        /// <returns>The contents of the specified file.</returns>
        private Task<string> GetFileContentsAsync(string relativeFilePath, string commitSha) => 
            client.GetStringAsync($"https://raw.githubusercontent.com/dotnet/source-build/{commitSha}/{relativeFilePath.Replace('\\', '/')}");

        /// <summary>
        /// Executes a git command and returns the result.
        /// </summary>
        /// <param name="workingDirectory">The working directory for the git command.</param>
        /// <param name="command">The git command to execute.</param>
        /// <returns>An array of the output lines of the git command.</returns>
        private string[] ExecuteGitCommand(string workingDirectory, string command)
        {
            string[] returnData;
            Process _process = new Process();
            _process.StartInfo.FileName = "git";
            _process.StartInfo.Arguments = command;
            _process.StartInfo.WorkingDirectory = workingDirectory;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.UseShellExecute = false;
            _process.Start();
            returnData = _process.StandardOutput.ReadToEnd().Split('\n');
            _process.WaitForExit();
            return returnData;
        }

        private class Commit
        {
            public string Sha { get; set; }
            public string Title { get; set; }
            public DateTime CommitDate { get; set; }
            public int PackageVersionCount { get; set; }
            public int PackageCount { get; set; }

            public override string ToString()
            {
                return $"{Sha}, {Title}, {CommitDate}, {PackageVersionCount}, {PackageCount}";
            }
        }
    }
}
