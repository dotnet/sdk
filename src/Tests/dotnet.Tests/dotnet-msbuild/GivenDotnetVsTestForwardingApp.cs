// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Tools.VSTest;
using Xunit;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetVsTestForwardingApp
    {
        [Fact]
        public void ItRunsVsTestApp()
        {
            new VSTestForwardingApp(new string[0])
                .GetProcessStartInfo().Arguments.Should().EndWith("vstest.console.dll");
        }
    }
}
