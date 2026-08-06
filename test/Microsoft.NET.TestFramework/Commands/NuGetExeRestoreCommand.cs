// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Commands
{
    public class NuGetExeRestoreCommand : TestCommand
    {
        private readonly string _projectRootPath;
        public string ProjectRootPath => _projectRootPath;

        public string ProjectFile { get; }

        public string? NuGetExeVersion { get; set; }

        public string FullPathProjectFile => Path.Combine(ProjectRootPath, ProjectFile);

        public string? PackagesDirectory { get; set; }

        public NuGetExeRestoreCommand(ITestOutputHelper log, string projectRootPath, string? relativePathToProject = null) : base(log)
        {
            _projectRootPath = projectRootPath;
            ProjectFile = MSBuildCommand.FindProjectFile(ref _projectRootPath, relativePathToProject);
        }

        protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            if (string.IsNullOrEmpty(TestContext.Current.NuGetExePath))
            {
                throw new InvalidOperationException("Path to nuget.exe not set");
            }

            var nugetExePath = TestContext.Current.NuGetExePath;
            if (!string.IsNullOrEmpty(NuGetExeVersion))
            {
                nugetExePath = Path.Combine(Path.GetDirectoryName(nugetExePath) ?? string.Empty, NuGetExeVersion, "nuget.exe");
            }

            if (!File.Exists(nugetExePath))
            {
                string directory = Path.GetDirectoryName(nugetExePath) ?? string.Empty;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string url = string.IsNullOrEmpty(NuGetExeVersion) ?
                    "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" :
                    $"https://dist.nuget.org/win-x86-commandline/v{NuGetExeVersion}/nuget.exe";

                DownloadNuGetAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                async Task DownloadNuGetAsync()
                {
                    using var client = new System.Net.Http.HttpClient();
                    using var response = await client.GetAsync(url).ConfigureAwait(false);
                    using var fs = new FileStream(nugetExePath, FileMode.CreateNew);
                    await response.Content.CopyToAsync(fs).ConfigureAwait(false);
                }
            }

            var ret = new SdkCommandSpec()
            {
                FileName = nugetExePath,
                Arguments =
                [
                    "restore",
                    FullPathProjectFile,
                    "-PackagesDirectory",
                    PackagesDirectory ?? TestContext.Current.NuGetCachePath ?? string.Empty,
                    .. args
                ]
            };

            TestContext.Current.AddTestEnvironmentVariables(ret.Environment);

            return ret;
        }
    }
}
