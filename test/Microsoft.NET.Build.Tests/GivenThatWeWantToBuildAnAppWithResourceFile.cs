// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.ProjectModel;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAnAppWithResourceFile : SdkTest
    {
        public GivenThatWeWantToBuildAnAppWithResourceFile(ITestOutputHelper log) : base(log) {}

        [Fact(DisplayName = "Metadata of designer.cs and resx file is added implicitly")]
        public void Added_implicitly()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithResxAndDesignercs")
                .WithSource()
                .Restore(Log);

            var directorySeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"\": @"/";

            var expectedMetaDataAndValuePair = new Dictionary<MetaDataIdentifier, string>() {
                { new MetaDataIdentifier("EmbeddedResource", $"TestWithNestedFolder{directorySeparator}ResourcesImplicateIncludeBySdk.resx", "Generator" ), "ResXFileCodeGenerator"},
                { new MetaDataIdentifier("EmbeddedResource", $"TestWithNestedFolder{directorySeparator}ResourcesImplicateIncludeBySdk.resx", "LastGenOutput" ), "ResourcesImplicateIncludeBySdk.Designer.cs"},
                { new MetaDataIdentifier("Compile", $"TestWithNestedFolder{directorySeparator}ResourcesImplicateIncludeBySdk.Designer.cs", "DesignTime" ), "True"},
                { new MetaDataIdentifier("Compile", $"TestWithNestedFolder{directorySeparator}ResourcesImplicateIncludeBySdk.Designer.cs", "AutoGen" ), "True"},
                { new MetaDataIdentifier("Compile", $"TestWithNestedFolder{directorySeparator}ResourcesImplicateIncludeBySdk.Designer.cs", "DependentUpon" ), "ResourcesImplicateIncludeBySdk.resx"},
            };

            var getValuesCommand = new GetMetaDataCommand(Log,
            expectedMetaDataAndValuePair.Keys,
            testAsset.TestRoot,
            "netcoreapp2.0");

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            foreach(KeyValuePair<MetaDataIdentifier, string> entry in expectedMetaDataAndValuePair)
            {
                var metaDataValue = getValuesCommand.GetMetaDataValue(entry.Key);
                metaDataValue.Should().Be(entry.Value);
            }
        }

        [Fact(DisplayName = "MetaData will not be added if it is explicit set by user")]
        public void Set_by_user()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithResxAndDesignercs")
                .WithSource()
                .Restore(Log);

            var directorySeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"\": @"/";

            var expectedMetaDataAndValuePair = new Dictionary<MetaDataIdentifier, string>() {
                { new MetaDataIdentifier("EmbeddedResource", $"ResourceExplicitSetByUser.resx", "LastGenOutput" ), "ResourceExplicitSetByUser.CustomName.cs"},
                { new MetaDataIdentifier("Compile", $"ResourceExplicitSetByUser.CustomName.cs", "DependentUpon" ), "ResourceExplicitSetByUser.resx"},
            };

            var getValuesCommand = new GetMetaDataCommand(Log,
            expectedMetaDataAndValuePair.Keys,
            testAsset.TestRoot,
            "netcoreapp2.0");

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            foreach(KeyValuePair<MetaDataIdentifier, string> entry in expectedMetaDataAndValuePair)
            {
                var metaDataValue = getValuesCommand.GetMetaDataValue(entry.Key);
                metaDataValue.Should().Be(entry.Value);
            }
        }
    }
}
