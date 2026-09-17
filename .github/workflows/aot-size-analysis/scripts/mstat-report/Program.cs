// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MstatReport;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args is ["--self-test"])
            {
                SelfTests.Run();
                Console.WriteLine("All mstat-report self-tests passed.");
                return 0;
            }

            if (args.Length != 5)
            {
                Console.Error.WriteLine(
                    "Usage: mstat-report <input.mstat> <scan.dgml.xml> <output.html> " +
                    "<generic-instantiations.csv> <single-dependency.csv>");
                Console.Error.WriteLine("       mstat-report --self-test");
                return 1;
            }

            string mstatPath = Path.GetFullPath(args[0]);
            string graphPath = Path.GetFullPath(args[1]);
            string htmlPath = Path.GetFullPath(args[2]);
            string genericCsvPath = Path.GetFullPath(args[3]);
            string singleCsvPath = Path.GetFullPath(args[4]);

            MstatModel mstat = MstatReader.Read(mstatPath);
            SizeReport report = ReportBuilder.Build(mstat, mstatPath);
            report.Retention = RetentionGraphReader.Read(graphPath, report.Root);
            ReportWriter.Write(report, htmlPath, genericCsvPath, singleCsvPath);

            Console.WriteLine(
                $"Wrote {htmlPath} ({report.AttributedSize:N0} attributed bytes, " +
                $"{report.Retention.DirectEdges.Count:N0} mapped incoming edges).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(args is ["--self-test"]
                ? exception
                : $"mstat-report: {exception.Message}");
            return 2;
        }
    }
}
