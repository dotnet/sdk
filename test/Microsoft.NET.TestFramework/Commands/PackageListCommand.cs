// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.TestFramework.Commands
{
    public class PackageListCommand : DotnetCommand
    {
        private string? _projectName = null;

        public PackageListCommand(ITestOutputHelper log, params string[] args) : base(log, args)
        {
        }

        public override CommandResult Execute(IEnumerable<string> args)
        {
            List<string> newArgs = ["package", "list"];
            if (!string.IsNullOrEmpty(_projectName))
            {
                newArgs.Add("--project");
                newArgs.Add(_projectName);
            }
            newArgs.AddRange(args);

            return base.Execute(newArgs);
        }

        public PackageListCommand WithProject(string projectName)
        {
            _projectName = projectName;
            return this;
        }
    }
}
