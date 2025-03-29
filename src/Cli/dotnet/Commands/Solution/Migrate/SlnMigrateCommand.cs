// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli;

internal class SlnMigrateCommand(
    ParseResult parseResult,
    IReporter reporter = null) : CommandBase(parseResult)
{
    private readonly string _slnFileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);
    private readonly IReporter _reporter = reporter ?? Reporter.Output;

    public override int Execute()
    {
        string slnFileFullPath = SlnFileFactory.GetSolutionFileFullPath(_slnFileOrDirectory);
        if (slnFileFullPath.HasExtension(".slnx"))
        {
            throw new GracefulException(LocalizableStrings.CannotMigrateSlnx);
        }
        string slnxFileFullPath = Path.ChangeExtension(slnFileFullPath, "slnx");
        try
        {
            ConvertToSlnxAsync(slnFileFullPath, slnxFileFullPath, CancellationToken.None).Wait();
            return 0;
        } catch (Exception ex) {
            throw new GracefulException(ex.Message, ex);
        }
    }

    private async Task ConvertToSlnxAsync(string filePath, string slnxFilePath, CancellationToken cancellationToken)
    {
        SolutionModel solution = SlnFileFactory.CreateFromFileOrDirectory(filePath);
        await SolutionSerializers.SlnXml.SaveAsync(slnxFilePath, solution, cancellationToken);
        _reporter.WriteLine(LocalizableStrings.SlnxGenerated, slnxFilePath);
    }
}
