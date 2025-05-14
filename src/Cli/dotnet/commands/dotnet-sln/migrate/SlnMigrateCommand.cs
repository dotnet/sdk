// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Cli
{
    internal class SlnMigrateCommand : CommandBase
    {
        private readonly string _slnFileOrDirectory;
        private readonly IReporter _reporter;
        public SlnMigrateCommand(
            ParseResult parseResult,
            IReporter reporter = null)
            : base(parseResult)
        {
            _slnFileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);
            _reporter = reporter ?? Reporter.Output;
        }

        public override int Execute()
        {
            string slnFileFullPath = SlnCommandParser.GetSlnFileFullPath(_slnFileOrDirectory);
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
            ISolutionSerializer serializer = SlnCommandParser.GetSolutionSerializer(filePath);
            SolutionModel solution = await serializer.OpenAsync(filePath, cancellationToken);
            await SolutionSerializers.SlnXml.SaveAsync(slnxFilePath, solution, cancellationToken);
            _reporter.WriteLine(LocalizableStrings.SlnxGenerated, slnxFilePath);
        }
    }
}
