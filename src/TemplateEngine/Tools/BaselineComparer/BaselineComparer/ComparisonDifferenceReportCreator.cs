using System;
using System.Linq;
using System.Text;
using BaselineComparer.DifferenceComparison;
using BaselineComparer.TemplateComparison;

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

                if (_diffResult.InvalidSecondaryData)
                {
                    reportBuilder.AppendLine("Secondary data wasn't correctly created.");
                    _anyProblems = true;
                    return;
                }

                _anyProblems |= ReportMissingSecondaryFiles(reportBuilder);
                _anyProblems |= ReportExtraSecondaryFiles(reportBuilder);
                _anyProblems |= ReportDifferenceIssues(reportBuilder);

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

        private bool ReportMissingSecondaryFiles(StringBuilder reportBuilder)
        {
            bool anyMissingSecondaryFiles = false;
            foreach (FileComparisonDifference missingSecondaryFileInfo in _diffResult.FileResults.Where(x => x.MissingSecondaryComparison))
            {
                if (!anyMissingSecondaryFiles)
                {
                    reportBuilder.AppendLine();
                    reportBuilder.AppendLine("*** Missing files from the new secondary data ***");
                    anyMissingSecondaryFiles = true;
                }

                reportBuilder.AppendLine($"\t{missingSecondaryFileInfo.Filename}");
            }

            return anyMissingSecondaryFiles;
        }

        private bool ReportExtraSecondaryFiles(StringBuilder reportBuilder)
        {
            bool anyExtraSecondaryFiles = false;
            foreach (FileComparisonDifference extraSecondaryFileInfo in _diffResult.FileResults.Where(x => x.MissingBaselineComparison))
            {
                if (!anyExtraSecondaryFiles)
                {
                    reportBuilder.AppendLine();
                    reportBuilder.AppendLine("*** Files in the secondary data but not in the baseline ***");
                    anyExtraSecondaryFiles = true;
                }

                reportBuilder.AppendLine($"\t{extraSecondaryFileInfo.Filename}");
            }

            return anyExtraSecondaryFiles;
        }

        private bool ReportDifferenceIssues(StringBuilder reportBuilder)
        {
            bool foundInvalidDiffFile = false;
            foreach (FileComparisonDifference fileDiff in _diffResult.FileResults.Where(f => f.AnyInvalidDifferences || f.HasDifferenceResolutionError))
            {
                if (!foundInvalidDiffFile)
                {
                    reportBuilder.AppendLine();
                    reportBuilder.AppendLine("*** Files with difference comparison issues ***");
                    foundInvalidDiffFile = true;
                }

                reportBuilder.AppendLine($"File: {fileDiff.Filename}");

                if (fileDiff.HasDifferenceResolutionError)
                {
                    reportBuilder.AppendLine("\t*** Difference resolution error. The number of differences in the original comparisons does not match the classified differences.");
                }

                if (fileDiff.BaselineOnlyDifferences.Count > 0)
                {
                    reportBuilder.AppendLine("\tDifferences found only in the baseline secondary data:");
                    foreach (PositionalDifference positionalDiff in fileDiff.BaselineOnlyDifferences)
                    {
                        reportBuilder.AppendLine($"\t\tBaseline master file (position = {positionalDiff.BaselineStartPosition}) {positionalDiff.BaselineData}");
                        reportBuilder.AppendLine($"\t\tBaseline secondary file (position = {positionalDiff.SecondaryStartPosition}) {positionalDiff.SecondaryData}");
                        reportBuilder.AppendLine($"\t\tDatatype: {positionalDiff.ClassificationString}");
                    }
                }

                if (fileDiff.SecondaryOnlyDifferences.Count > 0)
                {
                    reportBuilder.AppendLine("\tDifferences found only in the current secondary data:");
                    foreach (PositionalDifference positionalDiff in fileDiff.SecondaryOnlyDifferences)
                    {
                        reportBuilder.AppendLine($"\t\tBaseline master file (position = {positionalDiff.BaselineStartPosition}) {positionalDiff.BaselineData}");
                        reportBuilder.AppendLine($"\t\tCurrent secondary file (position = {positionalDiff.SecondaryStartPosition}) {positionalDiff.SecondaryData}");
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
                    reportBuilder.AppendLine($"\t\tBaseline secondary file (position = {difference.BaselineDifference.SecondaryStartPosition}) {difference.BaselineDifference.SecondaryData}");
                    reportBuilder.AppendLine($"\t\tCurrent secondary file (position = {difference.CheckDifference.SecondaryStartPosition}) {difference.CheckDifference.SecondaryData}");

                    if (difference.Disposition == PositionalComparisonDisposition.DatatypeMismatch)
                    {
                        reportBuilder.AppendLine($"\t\t\t* Datatype mismatch. Baseline datatype = {difference.BaselineDifference.ClassificationString} --- Secondary datatype = {difference.CheckDifference.ClassificationString}");
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
