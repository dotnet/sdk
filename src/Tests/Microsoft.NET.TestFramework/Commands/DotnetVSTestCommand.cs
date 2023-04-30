// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public class DotnetVSTestCommand : DotnetCommand
    {
        public DotnetVSTestCommand(ITestOutputHelper log, params string[] args) : base(log)
        {
            Arguments.Add("vstest");
            Arguments.AddRange(args);
        }
    }
}
