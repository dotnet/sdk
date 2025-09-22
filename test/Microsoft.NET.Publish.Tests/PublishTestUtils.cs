﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Publish.Tests
{
    internal static class PublishTestUtils
    {
#if NET10_0

        /// <summary>
        /// This list should contain the TFMs that we're interested in validating publishing support for
        /// </summary>
        public static IEnumerable<object[]> SupportedTfms { get; } = new List<object[]>
        {
            // Some tests started failing on net3.1 so disabling since this has been out of support for a while
            //new object[] { "netcoreapp3.1" },
            new object[] { "net5.0" },
            new object[] { "net6.0" },
            new object[] { "net7.0" },
            new object[] { "net8.0" },
            new object[] { "net9.0" },
            new object[] { ToolsetInfo.CurrentTargetFramework },
            // new object[] { ToolsetInfo.NextTargetFramework },
        };

        /// <summary>
        /// This list should contain all supported TFMs after net5.0
        /// </summary>
        public static IEnumerable<object[]> Net5Plus { get; } = new List<object[]>
        {
            new object[] { "net5.0" },
            new object[] { "net6.0" },
            new object[] { "net7.0" },
            new object[] { "net8.0" },
            new object[] { "net9.0" },
            new object[] { ToolsetInfo.CurrentTargetFramework },
            // new object[] { ToolsetInfo.NextTargetFramework },
        };

        /// <summary>
        /// This list should contain all supported TFMs after net6.0
        /// </summary>
        public static IEnumerable<object[]> Net6Plus { get; } = new List<object[]>
        {
            new object[] { "net6.0" },
            new object[] { "net7.0" },
            new object[] { "net8.0" },
            new object[] { "net9.0" },
            new object[] { ToolsetInfo.CurrentTargetFramework },
            // new object[] { ToolsetInfo.NextTargetFramework },
        };

        /// <summary>
        /// This list should contain all supported TFMs after net7.0
        /// </summary>
        public static IEnumerable<object[]> Net7Plus { get; } = new List<object[]>
        {
            new object[] { "net7.0" },
            new object[] { "net8.0" },
            new object[] { "net9.0" },
            new object[] { ToolsetInfo.CurrentTargetFramework },
            // new object[] { ToolsetInfo.NextTargetFramework },
        };

        /// <summary>
        /// This list should contain all supported TFMs after net8.0
        /// </summary>
        public static IEnumerable<object[]> Net8Plus { get; } = new List<object[]>
        {
            new object[] { "net8.0" },
            new object[] { "net9.0" },
            new object[] { ToolsetInfo.CurrentTargetFramework },
            // new object[] { ToolsetInfo.NextTargetFramework },
        };

        /// <summary>
        /// This list should contain all supported TFMs after net9.0
        /// </summary>
        public static IEnumerable<object[]> Net9Plus { get; } = new List<object[]>
        {
            new object[] { "net9.0" },
            new object[] { ToolsetInfo.CurrentTargetFramework },
            // new object[] { ToolsetInfo.NextTargetFramework },
        };

        /// <summary>
        /// This list should contain all supported TFMs after net10.0
        /// </summary>
        public static IEnumerable<object[]> Net10Plus { get; } = new List<object[]>
        {
            new object[] { "net10.0" },
            new object[] { ToolsetInfo.CurrentTargetFramework },
            // new object[] { ToolsetInfo.NextTargetFramework },
        };

        /// <summary>
        /// Starting in 8 we introduced made Publish* properties that imply SelfContained actually set SelfContained,
        /// and that means RIDs are inferred when publishing these. This list should contain all TFMs that do not infer SelfContained
        /// when PublishSelfContained or PublishSingleFile are set without an explicit SelfContained value.
        /// </summary>
        /// <remarks>
        /// Tried to be fancy here and compute this by stripping the NET8Plus items from the SupportedTfms list,
        /// but that broke test explorer integration in devkit.
        /// </remarks>
        public static IEnumerable<object[]> TFMsThatDoNotInferPublishSelfContained => [
            ["net5.0"],
            ["net6.0"],
            ["net7.0"],
        ];
#else
#error If building for a newer TFM, please update the values above to include both the old and new TFMs.
#endif

        public static void AddTargetFrameworkAliases(XDocument project)
        {
            var ns = project.Root.Name.Namespace;
            project.Root.Add(new XElement(ns + "PropertyGroup",
                new XAttribute("Condition", "'$(TargetFramework)' == 'alias-ns2'"),
                new XElement(ns + "TargetFrameworkIdentifier", ".NETStandard"),
                new XElement(ns + "TargetFrameworkVersion", "v2.0")));
            project.Root.Add(new XElement(ns + "PropertyGroup",
                new XAttribute("Condition", "'$(TargetFramework)' == 'alias-n6'"),
                new XElement(ns + "TargetFrameworkIdentifier", ".NETCoreApp"),
                new XElement(ns + "TargetFrameworkVersion", "v6.0")));
            project.Root.Add(new XElement(ns + "PropertyGroup",
                new XAttribute("Condition", "'$(TargetFramework)' == 'alias-n7'"),
                new XElement(ns + "TargetFrameworkIdentifier", ".NETCoreApp"),
                new XElement(ns + "TargetFrameworkVersion", "v7.0")));
            project.Root.Add(new XElement(ns + "PropertyGroup",
                new XAttribute("Condition", "'$(TargetFramework)' == 'alias-n8'"),
                new XElement(ns + "TargetFrameworkIdentifier", ".NETCoreApp"),
                new XElement(ns + "TargetFrameworkVersion", "v8.0")));
        }
    }
}
