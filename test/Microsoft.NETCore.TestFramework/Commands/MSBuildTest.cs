// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NETCore.TestFramework.Commands
{
    public class MSBuildTest
    {
        public static readonly MSBuildTest Stage0MSBuild = new MSBuildTest(RepoInfo.DotNetHostPath);

        private string DotNetHostPath { get; }

        public MSBuildTest(string dotNetHostPath)
        {
            DotNetHostPath = dotNetHostPath;
        }

        public Command CreateCommandForTarget(string target, params string[] args)
        {
            var newArgs = args.ToList();
            newArgs.Insert(0, $"/t:{target}");

            return CreateCommand(newArgs.ToArray());
        }

        private Command CreateCommand(params string[] args)
        {
            var newArgs = args.ToList();
            newArgs.Insert(0, $"msbuild");

            return Command.Create(DotNetHostPath, newArgs);
        }
    }
}