// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Add.FileReference
{
    internal class AddFileReferenceCommand : CommandBase
    {
        private readonly AppliedOption _appliedCommand;
        private readonly string _fileOrDirectory;

        public AddFileReferenceCommand(
            AppliedOption appliedCommand,
            string fileOrDirectory,
            ParseResult parseResult) : base(parseResult)
        {
            if (appliedCommand == null)
            {
                throw new ArgumentNullException(nameof(appliedCommand));
            }
            if (fileOrDirectory == null)
            {
                throw new ArgumentNullException(nameof(fileOrDirectory));
            }

            _appliedCommand = appliedCommand;
            _fileOrDirectory = fileOrDirectory;
        }

        public override int Execute()
        {
            var projects = new ProjectCollection();
            bool interactive = CommonOptionResult.GetInteractive(_appliedCommand);
            MsbuildProject msbuildProj = MsbuildProject.FromFileOrDirectory(
                projects,
                _fileOrDirectory,
                interactive);

            var frameworkString = _appliedCommand.ValueOrDefault<string>("framework");
            var refs = _appliedCommand.Arguments;

            if (frameworkString != null)
            {
                var framework = NuGetFramework.Parse(frameworkString);
                if (!msbuildProj.IsTargetingFramework(framework)) {
                    Reporter.Error.WriteLine(string.Format(
                                                    CommonLocalizableStrings.ProjectDoesNotTargetFramework,
                                                    msbuildProj.ProjectRootElement.FullPath,
                                                    frameworkString));
                    return 1;
                }
            }

            PathUtility.EnsureAllPathsExist(refs, CommonLocalizableStrings.CouldNotFindProjectOrDirectory, true);

            var relativePathReferences = refs.Select((r) =>
                                                            Path.GetRelativePath(
                                                                msbuildProj.ProjectDirectory,
                                                                System.IO.Path.GetFullPath(r))).ToList();

            int numberOfAddedReferences = msbuildProj.AddFileReferences(
                    frameworkString,
                    relativePathReferences);

            if (numberOfAddedReferences != 0)
            {
                msbuildProj.ProjectRootElement.Save();
            }

            return 0;
        }
    }
}
