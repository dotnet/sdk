// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Microsoft.DotNet.Cli
{
    internal class SlnMigrateCommand : CommandBase
    {
        private readonly string _slnFileFullPath;
        private readonly IReporter _reporter;
        public SlnMigrateCommand(
            ParseResult parseResult,
            IReporter reporter = null)
            : base(parseResult)
        {
            _slnFileFullPath = Path.GetFullPath(parseResult.GetValue(SlnCommandParser.SlnArgument));
            _reporter = reporter ?? Reporter.Output;
        }

        public override int Execute()
        {
            if (!File.Exists(_slnFileFullPath) || !Path.GetExtension(_slnFileFullPath).EndsWith("sln"))
            {
                throw new GracefulException(CommonLocalizableStrings.CouldNotFindSolutionIn);
            }
            string slnxFileFullPath = Path.ChangeExtension(_slnFileFullPath, "slnx");
            Task task = ConvertToSlnxAsync(_slnFileFullPath, slnxFileFullPath, CancellationToken.None);
            // TODO: Localize output...
            _reporter.WriteLine($"Solution file {slnxFileFullPath} generated.");
            return 0;
        }

        private static async Task ConvertToSlnxAsync(string filePath, string slnxFilePath, CancellationToken cancellationToken)
        {
            // See if the file is a known solution file.
            ISolutionSerializer? serializer = SolutionSerializers.GetSerializerByMoniker(filePath);
            if (serializer is null)
            {
                return;
            }
            SolutionModel solution = await serializer.OpenAsync(filePath, cancellationToken);
            await SolutionSerializers.SlnXml.SaveAsync(slnxFilePath, solution, cancellationToken);
        }
    }
}
