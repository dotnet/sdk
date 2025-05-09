// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShim
{
    internal class MacOSEnvironmentPath : IEnvironmentPath
    {
        private const string PathName = "PATH";
        private readonly BashPathUnderHomeDirectory _packageExecutablePath;
        private readonly IFile _fileSystem;
        private readonly IEnvironmentProvider _environmentProvider;
        private readonly IReporter _reporter;

        internal static readonly string DotnetCliToolsPathsDPath
            = Environment.GetEnvironmentVariable("DOTNET_CLI_TEST_OSX_PATHSD_PATH")
              ?? @"/etc/paths.d/dotnet-cli-tools";

        public MacOSEnvironmentPath(
            BashPathUnderHomeDirectory executablePath,
            IReporter reporter,
            IEnvironmentProvider environmentProvider,
            IFile fileSystem
        )
        {
            _packageExecutablePath = executablePath;
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _environmentProvider
                = environmentProvider ?? throw new ArgumentNullException(nameof(environmentProvider));
            _reporter
                = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        public void AddPackageExecutablePathToUserPath()
        {
            if (PackageExecutablePathExists())
            {
                return;
            }

            _fileSystem.WriteAllText(DotnetCliToolsPathsDPath, _packageExecutablePath.Path);
        }

        private bool PackageExecutablePathExists()
        {
            var value = _environmentProvider.GetEnvironmentVariable(PathName);
            if (value == null)
            {
                return false;
            }

            return value
                .Split(':')
                .Any(p => p.Equals(_packageExecutablePath.Path, StringComparison.OrdinalIgnoreCase));
        }

        public void PrintAddPathInstructionIfPathDoesNotExist()
        {
            if (!PackageExecutablePathExists())
            {
                if (_fileSystem.Exists(DotnetCliToolsPathsDPath))
                {
                    _reporter.WriteLine(
                        CommonLocalizableStrings.EnvironmentPathOSXNeedReopen);
                }
                else
                {
                    // similar to https://code.visualstudio.com/docs/setup/mac
                    _reporter.WriteLine(
                        ZshDetector.IsZshTheUsersShell(_environmentProvider)
                            ? string.Format(
                                CommonLocalizableStrings.EnvironmentPathOSXZshManualInstructions,
                                _packageExecutablePath.Path)
                            : string.Format(
                                CommonLocalizableStrings.EnvironmentPathOSXBashManualInstructions,
                                _packageExecutablePath.Path));
                }
            }
        }
    }
}
