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
using Reporting;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

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
            var settings = new JsonSerializerSettings();
            var resolver = new DefaultContractResolver();
            resolver.NamingStrategy = new CamelCaseNamingStrategy() { ProcessDictionaryKeys = false };
            settings.ContractResolver = resolver;
            JsonConvert.SerializeObject(GenerateTestsFromXml(), Formatting.Indented, settings).Should().Be("3");

        }

        public static List<Test> GenerateTestsFromXml()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ScenarioBenchmark));

            StreamReader reader = new StreamReader(Path.Combine("PerformanceResultSample", "20190618003138-WPF hello world.xml"));
            var scenarioBenchmark = (ScenarioBenchmark)serializer.Deserialize(reader);
            Console.WriteLine(scenarioBenchmark.Name);
            var tests = new List<Test>();
            foreach (ScenarioBenchmarkTest scenarioBenchmarkTest in scenarioBenchmark.Tests)
            {
                var test = new Test();
                test.Categories.Add("DotnetCoreSdk");
                test.Name = scenarioBenchmark.Name + "." + scenarioBenchmarkTest.Name;
                test.Counters.Add(new Counter() {
                    Name = scenarioBenchmarkTest.Performance.metrics.ExecutionTime.displayName,
                    TopCounter = true,
                    DefaultCounter = true,
                    HigherIsBetter = false,
                    MetricName = scenarioBenchmarkTest.Performance.metrics.ExecutionTime.unit,
                    Results = scenarioBenchmarkTest
                        .Performance
                        .iterations
                        .Select(i => decimal.ToDouble(i.ExecutionTime))
                        .ToList()
                });

                tests.Add(test);
            }

            return tests;
        }


        [Fact]
        public void It_can_get_commit_timestamp()
        {
            Reporting.Reporter.GetCommitTimestamp("29bf0a82d9d9e20b6da067b3bf2cb05bf0504e47", TestContext.GetRepoRoot()).Should().Be(DateTime.Parse("2019-04-23T13:59:53+00:00"));
        }
    }
}
