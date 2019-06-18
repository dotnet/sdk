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
    }
}
