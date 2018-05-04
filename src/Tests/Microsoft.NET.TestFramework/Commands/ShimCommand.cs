// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public class ShimCommand : TestCommand
    {
        private readonly string _commandPath;

        public ShimCommand(ITestOutputHelper log, string commandPath, params string[] args) : base(log)
        {
            if (!File.Exists(commandPath))
            {
                throw new ArgumentException($"Cannot find command {commandPath}");
            }

            _commandPath = commandPath;
            Arguments.AddRange(args);
        }

        protected override SdkCommandSpec CreateCommand(string[] args)
        {
            var sdkCommandSpec = new SdkCommandSpec()
            {
                FileName = _commandPath,
                Arguments = args.ToList(),
                WorkingDirectory = WorkingDirectory,
            };


            string dotnetRoot = Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath);
            if (Environment.Is64BitProcess)
            {
                sdkCommandSpec.Environment.Add("DOTNET_ROOT", dotnetRoot);
            }
            else
            {
                sdkCommandSpec.Environment.Add("DOTNET_ROOT(x86)", dotnetRoot);
            }

            return sdkCommandSpec;
        }
    }
}
