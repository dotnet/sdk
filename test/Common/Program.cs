// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework;

#pragma warning disable SA1205 // Partial elements should declare access
partial class Program
#pragma warning restore SA1205 // Partial elements should declare access
{
    public static int Main(string[] args)
    {
        var newArgs = new List<string>(args);
        newArgs.Insert(0, typeof(Program).Assembly.Location);

        var xunitReturnCode = Xunit.ConsoleClient.Program.Main(newArgs.ToArray());

        int returnCode;
        if (Environment.GetEnvironmentVariable("HELIX_WORKITEM_PAYLOAD") != null)
        {
            // If we are running in Helix, we want the test work item to return 0 unless there's a crash
            Console.WriteLine($"Xunit return code: {xunitReturnCode}");
            returnCode = 0;
        }
        else
        {
            // If we are running locally, we to return the xunit return code
            returnCode = xunitReturnCode;
        }

        return returnCode;
    }
}
