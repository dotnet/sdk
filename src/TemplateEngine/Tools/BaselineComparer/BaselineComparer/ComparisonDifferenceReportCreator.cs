using System;
using System.Linq;
using System.Text;

namespace BaselineComparer
{
    public class ComparisonDifferenceReportCreator
    {
        public ComparisonDifferenceReportCreator(DirectoryComparisonDifference diffResult)
        {
            _diffResult = diffResult;
            _reportText = null;
            _anyProblems = false;
        }

        private DirectoryComparisonDifference _diffResult;
        private string _reportText;
        private bool _anyProblems;

        public string ReportText
        {
            get
            {
                EnsureReport();
                return _reportText;
            }
        }

        public bool AnyProblems
        {
            get
            {
                EnsureReport();
                return _anyProblems;
            }
        }

        private void EnsureReport()
        {
            if (_reportText == null)
            {
                StringBuilder reportBuilder = new StringBuilder(1024);

                if (_diffResult.InvalidBaselineData)
                {
                    reportBuilder.AppendLine("Baseline data is invalid.");
                    _anyProblems = true;
                    return;
                }

                if (_diffResult.InvalidCheckData)
                {
                    reportBuilder.AppendLine("Check data wasn't correctly created.");
                    _anyProblems = true;
                    return;
                }

                _anyProblems |= ReportMissingCheckFiles(reportBuilder);
                _anyProblems |= ReportExtraCheckFiles(reportBuilder);
                _anyProblems |= ReportInvalidDifferences(reportBuilder);

                reportBuilder.AppendLine();

                if (_anyProblems)
                {
                    reportBuilder.AppendLine("Differences were found which should be looked at.");
                }
                else
                {
                    reportBuilder.AppendLine("Differences are within tolerance.");
                }

                _reportText = reportBuilder.ToString();
            }
        }

        private bool ReportMissingCheckFiles(StringBuilder reportBuilder)
        {
            bool foundMissingCheckFile = false;
            foreach (FileComparisonDifference missingCheckFileInfo in _diffResult.FileResults.Where(x => x.MissingCheckComparison))
            {
                if (!foundMissingCheckFile)
                {
                    reportBuilder.AppendLine();
                    reportBuilder.AppendLine("*** Missing files from the new check data ***");
                    foundMissingCheckFile = true;
                }

                reportBuilder.AppendLine($"\t{missingCheckFileInfo.Filename}");
            }

            return foundMissingCheckFile;
        }

        private bool ReportExtraCheckFiles(StringBuilder reportBuilder)
        {
            bool foundExtraCheckFile = false;
            foreach (FileComparisonDifference extraCheckFileInfo in _diffResult.FileResults.Where(x => x.MissingBaselineComparison))
            {
                if (!foundExtraCheckFile)
                {
                    reportBuilder.AppendLine();
                    reportBuilder.AppendLine("*** Files in the check data but not in the baseline ***");
                    foundExtraCheckFile = true;
                }

                reportBuilder.AppendLine($"\t{extraCheckFileInfo.Filename}");
            }

            return foundExtraCheckFile;
        }

        private bool ReportInvalidDifferences(StringBuilder reportBuilder)
        {
            bool foundInvalidDiffFile = false;
            foreach (FileComparisonDifference fileDiff in _diffResult.FileResults.Where(f => f.AnyInvalidDifferences))
            {
                if (!foundInvalidDiffFile)
                {
                    reportBuilder.AppendLine();
                    reportBuilder.AppendLine("*** Files with invalid differences ***");
                    foundInvalidDiffFile = true;
                }

                reportBuilder.AppendLine($"File: {fileDiff.Filename}");

                if (fileDiff.BaselineOnlyDifferences.Count > 0)
                {
                    reportBuilder.AppendLine("\tDifferences found only in the baseline check data:");
                    foreach (PositionalDifference positionalDiff in fileDiff.BaselineOnlyDifferences)
                    {
                        reportBuilder.AppendLine($"\t\tBaseline master file (position = {positionalDiff.BaselineStartPosition}) {positionalDiff.BaselineData}");
                        reportBuilder.AppendLine($"\t\tBaseline check file (position = {positionalDiff.TargetStartPosition}) {positionalDiff.TargetData}");
                        reportBuilder.AppendLine($"\t\tDatatype: {positionalDiff.ClassificationString}");
                    }
                }

                if (fileDiff.CheckOnlyDifferences.Count > 0)
                {
                    reportBuilder.AppendLine("\tDifferences found only in the current check data:");
                    foreach (PositionalDifference positionalDiff in fileDiff.CheckOnlyDifferences)
                    {
                        reportBuilder.AppendLine($"\t\tBaseline master file (position = {positionalDiff.BaselineStartPosition}) {positionalDiff.BaselineData}");
                        reportBuilder.AppendLine($"\t\tCurrent check file (position = {positionalDiff.TargetStartPosition}) {positionalDiff.TargetData}");
                        reportBuilder.AppendLine($"\t\tDatatype: {positionalDiff.ClassificationString}");
                    }
                }

                bool firstPositionalDiffMismatch = true;
                foreach (PositionalComparisonDifference difference in fileDiff.PositionallyMatchedDifferences.Where(d => d.Disposition != PositionalComparisonDisposition.Match))
                {
                    if (firstPositionalDiffMismatch)
                    {
                        reportBuilder.AppendLine($"\tDifferent differences in similar locations:");
                        firstPositionalDiffMismatch = false;
                    }

                    reportBuilder.AppendLine($"\t\tBaseline master file (position = {difference.BaselineDifference.BaselineStartPosition}) {difference.BaselineDifference.BaselineData}");
                    reportBuilder.AppendLine($"\t\tBaseline check file (position = {difference.BaselineDifference.TargetStartPosition}) {difference.BaselineDifference.TargetData}");
                    reportBuilder.AppendLine($"\t\tCurrent check file (position = {difference.CheckDifference.TargetStartPosition}) {difference.CheckDifference.TargetData}");

                    if (difference.Disposition == PositionalComparisonDisposition.DatatypeMismatch)
                    {
                        reportBuilder.AppendLine($"\t\t\t* Datatype mismatch. Baseline datatype = {difference.BaselineDifference.ClassificationString} --- Check datatype = {difference.CheckDifference.ClassificationString}");
                    }
                    else if (difference.Disposition == PositionalComparisonDisposition.LengthMismatch)
                    {
                        reportBuilder.AppendLine($"\t\t\t* Data length mismatch.");
                    }
                    else
                    {
                        // in case additional dispositions are added - they need to be dealt with in this report.
                        throw new Exception($"Unhandled difference disposition = {difference.Disposition.ToString()}");
                    }
                }
            }

            return foundInvalidDiffFile;
        }
    }
}
