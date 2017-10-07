using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Xunit.Performance.Api;

namespace Microsoft.NET.Perf.Tests
{
    public class PerfTest
    {
        public string TestName { get; set; }
        public int NumberOfIterations { get; set; } = 10;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);
        public ProcessStartInfo ProcessToMeasure { get; set; }
        public string TestFolder { get; set; }

        public void Run()
        {
            var scenarioConfiguration = new ScenarioConfiguration(TimeSpan.FromMilliseconds(Timeout.TotalMilliseconds));
            scenarioConfiguration.Iterations = NumberOfIterations;

            Stopwatch stopwatch = new Stopwatch();
            TimeSpan[] executionTimes = new TimeSpan[NumberOfIterations];
            int currentIteration = 0;

            using (FolderSnapshot snapshot = FolderSnapshot.Create(TestFolder))
            {
                void PreIteration()
                {
                    if (currentIteration > 0)
                    {
                        snapshot.Restore();
                    }
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
                    var ret = new ScenarioBenchmark("ScenarioBenchmarkName");

                    var duration = new ScenarioTestModel(TestName);
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

                _performanceHarness.RunScenario(ProcessToMeasure,
                    PreIteration,
                    PostIteration,
                    PostRun,
                    scenarioConfiguration);
            }
        }

        static XunitPerformanceHarness _performanceHarness;

        public static void InitializeHarness(params string [] args)
        {
            _performanceHarness = new XunitPerformanceHarness(args);
        }

        public static void DisposeHarness()
        {
            _performanceHarness.Dispose();
        }

 
    }
}
