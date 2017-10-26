using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Xunit.Performance.Api;
using Xunit;

//  Don't run any perf tests in parallel
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

namespace Microsoft.NET.Perf.Tests
{
    public class PerfTest
    {
        public string ScenarioName { get; set; }
        public string TestName { get; set; }
        public int NumberOfIterations { get; set; } = 10;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(120);
        public ProcessStartInfo ProcessToMeasure { get; set; }
        public string TestFolder { get; set; }

        public bool GetPerformanceSummary { get; set; } = true;

        public void Run([CallerMemberName] string callerName = null)
        {
            TestName = TestName ?? callerName;

            Stopwatch stopwatch = new Stopwatch();
            TimeSpan[] executionTimes = new TimeSpan[NumberOfIterations];
            int currentIteration = 0;

            var durationTestModel = new ScenarioTestModel(TestName);
            
            durationTestModel.Performance.Metrics.Add(new MetricModel
            {
                Name = "ExecutionTime",
                DisplayName = "Execution Time",
                Unit = "ms"
            });

            using (FolderSnapshot snapshot = FolderSnapshot.Create(TestFolder))
            {
                void PreIteration(ScenarioTest scenarioTest)
                {
                    if (currentIteration > 0)
                    {
                        snapshot.Restore();
                    }
                    stopwatch.Restart();
                }

                void PostIteration(ScenarioExecutionResult scenarioExecutionResult)
                {
                    stopwatch.Stop();
                    var elapsed = scenarioExecutionResult.ProcessExitInfo.ExitTime - scenarioExecutionResult.ProcessExitInfo.StartTime;

                    var durationIteration = new IterationModel
                    {
                        Iteration = new Dictionary<string, double>()
                    };
                    durationIteration.Iteration.Add(durationTestModel.Performance.Metrics[0].Name, elapsed.TotalMilliseconds);
                    durationTestModel.Performance.IterationModels.Add(durationIteration);

                    string performanceSummaryFileDestination = Path.ChangeExtension(scenarioExecutionResult.EventLogFileName, ".txt");
                    File.Move(Path.Combine(TestFolder, "PerformanceSummary.txt"), performanceSummaryFileDestination);

                    currentIteration++;
                }

                void PostRun(ScenarioBenchmark scenario)
                {

                }

                if (GetPerformanceSummary)
                {
                    ProcessToMeasure.Arguments += " /flp9:PerformanceSummary;v=q;logfile=\"" + Path.Combine(TestFolder, "PerformanceSummary.txt") +  "\"";
                }

                var scenarioTestConfiguration = new ScenarioTestConfiguration(TimeSpan.FromMilliseconds(Timeout.TotalMilliseconds), ProcessToMeasure);
                scenarioTestConfiguration.Iterations = NumberOfIterations;
                scenarioTestConfiguration.PreIterationDelegate = PreIteration;
                scenarioTestConfiguration.PostIterationDelegate = PostIteration;
                scenarioTestConfiguration.SaveResults = false;
                scenarioTestConfiguration.Scenario = GetScenarioBenchmark(ScenarioName ?? TestName);
                scenarioTestConfiguration.Scenario.Tests.Add(durationTestModel);
                scenarioTestConfiguration.TestName = TestName;                

                _performanceHarness.RunScenario(scenarioTestConfiguration, PostRun);
            }
        }

        static XunitPerformanceHarness _performanceHarness;
        static Dictionary<string, ScenarioBenchmark> _scenarios;

        public static void InitializeHarness(params string [] args)
        {
            _performanceHarness = new XunitPerformanceHarness(args);
            _scenarios = new Dictionary<string, ScenarioBenchmark>();
        }

        public static void DisposeHarness()
        {
            foreach (var kvp in _scenarios)
            {
                var scenarioFileNameWithoutExtension = Path.Combine(_performanceHarness.OutputDirectory, $"{_performanceHarness.Configuration.RunId}-{kvp.Key}");
                _performanceHarness.WriteXmlResults(kvp.Value, scenarioFileNameWithoutExtension);
            }

            var aggregateFileNameWithoutExtension = Path.Combine(_performanceHarness.OutputDirectory, _performanceHarness.Configuration.RunId);
            _performanceHarness.WriteTableResults(_scenarios.Values, aggregateFileNameWithoutExtension, true);

            _performanceHarness.Dispose();
        }

        static ScenarioBenchmark GetScenarioBenchmark(string name)
        {
            ScenarioBenchmark scenario;
            if (_scenarios.TryGetValue(name, out scenario))
            {
                return scenario;
            }

            scenario = new ScenarioBenchmark(name);
            _scenarios[name] = scenario;
            return scenario;
        }

 
    }
}
