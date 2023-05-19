﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public class DotnetTestCommand : DotnetCommand
    {
        public DotnetTestCommand(ITestOutputHelper log, params string[] args) : base(log)
        {
            Arguments.Add("test");
            Arguments.AddRange(args);
        }
    }
}
