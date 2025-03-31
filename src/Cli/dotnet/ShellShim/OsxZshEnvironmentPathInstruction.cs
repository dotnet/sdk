// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Cli.ShellShim;

internal class OsxZshEnvironmentPathInstruction(
    BashPathUnderHomeDirectory executablePath,
    IReporter reporter,
    IEnvironmentProvider environmentProvider
    ) : IEnvironmentPathInstruction
{
    private const string PathName = "PATH";
    private readonly BashPathUnderHomeDirectory _packageExecutablePath = executablePath;
    private readonly IEnvironmentProvider _environmentProvider = environmentProvider ?? throw new ArgumentNullException(nameof(environmentProvider));
    private readonly IReporter _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));

    private bool PackageExecutablePathExists()
    {
        string value = _environmentProvider.GetEnvironmentVariable(PathName);
        if (value == null)
        {
            return false;
        }

        return value.Split(':').Any(p => p == _packageExecutablePath.Path);
    }

    public void PrintAddPathInstructionIfPathDoesNotExist()
    {
        if (!PackageExecutablePathExists())
        {
            // similar to https://code.visualstudio.com/docs/setup/mac
            _reporter.WriteLine(
                string.Format(
                    CommonLocalizableStrings.EnvironmentPathOSXZshManualInstructions,
                    _packageExecutablePath.Path));
        }
    }
}
