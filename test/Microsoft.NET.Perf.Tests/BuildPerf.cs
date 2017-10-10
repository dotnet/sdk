using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Microsoft.Xunit.Performance.Api;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Perf.Tests
{
    public class BuildPerf : SdkTest
    {
        public BuildPerf(ITestOutputHelper log) : base(log)
        {
        }

        private const double TimeoutInMilliseconds = 20000;
        private const int NumberOfIterations = 10;

        [Fact]
        public void BuildNetCoreApp()
        {
            var testProject = new TestProject()
            {
                Name = "NetCoreApp",
                TargetFrameworks = "netcoreapp2.0",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            var cleanCommand = new MSBuildCommand(Log, "clean", buildCommand.ProjectRootPath);

            var scenarioConfiguration = new ScenarioConfiguration(TimeSpan.FromMilliseconds(TimeoutInMilliseconds));
            scenarioConfiguration.Iterations = NumberOfIterations;

            Stopwatch stopwatch = new Stopwatch();
            TimeSpan[] executionTimes = new TimeSpan[NumberOfIterations];
            int currentIteration = 0;

            void PreIteration()
            {
                cleanCommand.Execute()
                    .Should()
                    .Pass();
                stopwatch.Restart();
            }

            void PostIteration()
            {
                stopwatch.Stop();
                executionTimes[currentIteration] = stopwatch.Elapsed;
                currentIteration++;
            }

            ScenarioBenchmark PostRun()
            {
                var ret = new ScenarioBenchmark("BuildNetCoreApp");

                var duration = new ScenarioTestModel("Build .NET Core 2 app");
                ret.Tests.Add(duration);

                duration.Performance.Metrics.Add(new MetricModel
                {
                    Name = "ExecutionTime",
                    DisplayName = "Execution Time",
                    Unit = "ms"
                });

                for (int i = 0; i < NumberOfIterations; i++)
                {
                    var durationIteration = new IterationModel
                    {
                        Iteration = new Dictionary<string, double>()
                    };
                    durationIteration.Iteration.Add("ExecutionTime", executionTimes[i].TotalMilliseconds);
                    duration.Performance.IterationModels.Add(durationIteration);
                }

                return ret;
            }

            using (var h = new XunitPerformanceHarness(Array.Empty<string>()))
            {
                var startInfo = buildCommand.GetProcessStartInfo();
                h.RunScenario(startInfo,
                    PreIteration,
                    PostIteration,
                    PostRun,
                    scenarioConfiguration);
            }
        }
    }
}
