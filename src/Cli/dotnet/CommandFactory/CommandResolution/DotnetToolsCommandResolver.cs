// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.DotNet.CommandFactory
{
    /// <summary>
    /// A <see cref="ICommandResolver" /> for tools built-in with the .NET SDK.
    /// This includes <c>dotnet-watch</c>, <c>dotnet-dev-certs<c>, <c>dotnet-sql-cache<c>, and <c>dotnet-user-secrets</c>.
    /// </summary>
    public class DotnetToolsCommandResolver : ICommandResolver
    {
        private string _dotnetToolPath;

        public DotnetToolsCommandResolver(string dotnetToolPath = null)
        {
            if (dotnetToolPath == null)
            {
                _dotnetToolPath = Path.Combine(AppContext.BaseDirectory,
                    "DotnetTools");
            }
            else
            {
                _dotnetToolPath = dotnetToolPath;
            }
        }

        public CommandSpec Resolve(CommandResolverArguments arguments)
        {
            if (string.IsNullOrEmpty(arguments.CommandName))
            {
                return null;
            }

            var packagePath = Path.Combine(_dotnetToolPath, arguments.CommandName);
            var toolAtRootPath = Path.Combine(packagePath, arguments.CommandName + ".dll");
            if (File.Exists(toolAtRootPath))
            {
                return MuxerCommandSpecMaker.CreatePackageCommandSpecUsingMuxer(
                    toolAtRootPath,
                    arguments.CommandArguments);
            }

            return null;
        }
    }
}
