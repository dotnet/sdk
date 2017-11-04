using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.NET.Perf.Tests;

partial class Program
{
    static partial void BeforeTestRun(List<string> args)
    {
        List<string> newArgs = new List<string>();
        List<string> perfArgs = new List<string>();
        Stack<string> argStack = new Stack<string>(Enumerable.Reverse(args));

        bool needsOutputDir = true;

        while (argStack.Any())
        {
            string arg = argStack.Pop();

            if (arg.StartsWith("--perf:", StringComparison.OrdinalIgnoreCase) && argStack.Any())
            {
                if (arg.Equals("--perf:iterations", StringComparison.OrdinalIgnoreCase))
                {
                    PerfTest.DefaultIterations = int.Parse(argStack.Pop());
                }
                else
                {
                    if (arg.Equals("--perf:outputdir", StringComparison.OrdinalIgnoreCase))
                    {
                        needsOutputDir = false;
                    }

                    perfArgs.Add(arg);
                    perfArgs.Add(argStack.Pop());
                }
            }
            else
            {
                newArgs.Add(arg);
            }
        }

        if (needsOutputDir)
        {
            perfArgs.Add("--perf:outputdir");
            perfArgs.Add("PerfResults");
        }

        perfArgs.Add("--perf:collect");
        //  BranchMispredictions+CacheMisses+InstructionRetired
        perfArgs.Add("InstructionRetired");

        PerfTest.InitializeHarness(perfArgs.ToArray());

        args.Clear();
        args.AddRange(newArgs);
    }
    static partial void AfterTestRun()
    {
        PerfTest.DisposeHarness();
    }
}
