// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal static class ReportWriter
    {
        public static void Write(string reportPath, IEnumerable<FormattedFile> formattedFiles, ILogger logger)
        {
            var reportFilePath = GetReportFilePath(reportPath);
            var reportFolderPath = Path.GetDirectoryName(reportFilePath);

            if (!string.IsNullOrEmpty(reportFolderPath) && !Directory.Exists(reportFolderPath))
            {
                Directory.CreateDirectory(reportFolderPath);
            }

            logger.LogInformation(Resources.Writing_formatting_report_to_0, reportFilePath);

            var seralizerOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var formattedFilesJson = JsonSerializer.Serialize(formattedFiles, seralizerOptions);

            File.WriteAllText(reportFilePath, formattedFilesJson);
        }

        private static string GetReportFilePath(string reportPath)
        {
            var defaultReportName = "format-report.json";
            if (reportPath.EndsWith(".json"))
            {
                return reportPath;
            }
            else if (reportPath == ".")
            {
                return Path.Combine(Environment.CurrentDirectory, defaultReportName);
            }
            else
            {
                return Path.Combine(reportPath, defaultReportName);
            }
        }
    }
}
