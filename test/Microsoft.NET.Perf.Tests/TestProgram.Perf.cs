using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.NET.Perf.Tests;

partial class Program
{
    static partial void BeforeTestRun()
    {
        PerfTest.InitializeHarness("--perf:outputdir", "PerfResults");
    }
    static partial void AfterTestRun()
    {
        PerfTest.DisposeHarness();
    }
}
