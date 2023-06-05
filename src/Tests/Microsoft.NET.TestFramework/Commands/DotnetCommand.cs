﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public class DotnetCommand : TestCommand
    {
        public DotnetCommand(ITestOutputHelper log, params string[] args) : base(log)
        {
            Arguments.AddRange(args);
        }

        protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            var sdkCommandSpec = new SdkCommandSpec()
            {
                FileName = TestContext.Current.ToolsetUnderTest.DotNetHostPath,
                Arguments = args.ToList(),
                WorkingDirectory = WorkingDirectory
            };
            TestContext.Current.AddTestEnvironmentVariables(sdkCommandSpec.Environment);
            return sdkCommandSpec;
        }
    }
}
