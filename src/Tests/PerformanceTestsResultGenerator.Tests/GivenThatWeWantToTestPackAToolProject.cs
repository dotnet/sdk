// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using NuGet.Packaging;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using System;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenPerformanceTestsResultGenerator : SdkTest
    {
        public GivenPerformanceTestsResultGenerator(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_can_convert_xmls()
        {

        }

        [Fact]
        public void It_can_get_commit_timestamp()
        {
            Reporting.Reporter.GetCommitTimestamp("29bf0a82d9d9e20b6da067b3bf2cb05bf0504e47", TestContext.GetRepoRoot()).Should().Be(DateTime.Parse("2019-04-23T13:59:53+00:00"));
        }
    }
}
