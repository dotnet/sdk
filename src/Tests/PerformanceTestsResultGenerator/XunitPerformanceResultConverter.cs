using System;
using System.Collections.Generic;
using System.Text;
using Reporting;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;

namespace PerformanceTestsResultGenerator
{
    public static class XunitPerformanceResultConverter
    {
        public static List<Test> GenerateTestsFromXml(StreamReader stream)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ScenarioBenchmark));

            var scenarioBenchmark = (ScenarioBenchmark)serializer.Deserialize(stream);
            Console.WriteLine(scenarioBenchmark.Name);
            var tests = new List<Test>();
            foreach (ScenarioBenchmarkTest scenarioBenchmarkTest in scenarioBenchmark.Tests)
            {
                var test = new Test();
                test.Categories.Add("DotnetCoreSdk");
                test.Name = scenarioBenchmark.Name + "." + scenarioBenchmarkTest.Name;
                test.Counters.Add(new Counter()
                {
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

        public static List<Test> BatchGenerateTests(DirectoryInfo directory)
        {
            return directory.EnumerateFiles()
                .Where(f => string.Equals(Path.GetExtension(f.FullName), ".xml", StringComparison.OrdinalIgnoreCase))
                .SelectMany(xmlFile => GenerateTestsFromXml(new StreamReader(xmlFile.FullName))).ToList();
        }
    }
}
