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
            _slnFileOrDirectory = Path.GetFullPath(parseResult.GetValue(SlnCommandParser.SlnArgument));
            _reporter = reporter ?? Reporter.Output;
        }

        public override int Execute()
        {
            string slnFileFullPath = GetSlnFileFullPath();
            string slnxFileFullPath = Path.ChangeExtension(slnFileFullPath, "slnx");
            Task task =  ConvertToSlnxAsync(slnFileFullPath, slnxFileFullPath, CancellationToken.None);
            if (task.IsCompletedSuccessfully)
            {
                return 0;
            }
            throw new GracefulException(task.Exception.Message, task.Exception);
        }

        private string GetSlnFileFullPath()
        {
            if (File.Exists(_slnFileOrDirectory))
            {
                return _slnFileOrDirectory;
            }
            else if (Directory.Exists(_slnFileOrDirectory))
            {
                var files = Directory.GetFiles(_slnFileOrDirectory, "*.sln", SearchOption.TopDirectoryOnly);
                if (files.Length == 0)
                {
                    throw new GracefulException(CommonLocalizableStrings.CouldNotFindSolutionIn, _slnFileOrDirectory);
                }
                if (files.Length > 1)
                {
                    throw new GracefulException(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, _slnFileOrDirectory);
                }
                return files.Single().ToString();
            }
            else
            {
                throw new GracefulException(CommonLocalizableStrings.CouldNotFindSolutionOrDirectory, _slnFileOrDirectory);
            }
        }

        private async Task ConvertToSlnxAsync(string filePath, string slnxFilePath, CancellationToken cancellationToken)
        {
            // See if the file is a known solution file.
            ISolutionSerializer? serializer = SolutionSerializers.GetSerializerByMoniker(filePath);
            if (serializer is null)
            {
                throw new GracefulException("Could not find serializer for file {0}", filePath);
            }
            SolutionModel solution = await serializer.OpenAsync(filePath, cancellationToken);
            await SolutionSerializers.SlnXml.SaveAsync(slnxFilePath, solution, cancellationToken);
            _reporter.WriteLine(LocalizableStrings.SlnxGenerated, slnxFilePath);
        }
    }
}
