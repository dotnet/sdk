// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.FileReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class AddFileParser
    {
        public static Command AddFile()
        {
            return Create.Command(
                "file",
                LocalizableStrings.AppFullName, 
                Accept.OneOrMoreArguments()
                      .With(name: LocalizableStrings.FilePathArgumentName,
                            description: LocalizableStrings.FilePathArgumentDescription),
                CommonOptions.HelpOption(),
                Create.Option("-f|--framework", LocalizableStrings.CmdFrameworkDescription,
                              Accept.ExactlyOneArgument()
                                    .WithSuggestionsFrom(_ => Suggest.TargetFrameworksFromProjectFile())
                                    .With(name: Tools.Add.PackageReference.LocalizableStrings.CmdFramework)),
                CommonOptions.InteractiveOption());
        }
    } 
}
