using System.Collections.Generic;
using System.IO;

namespace BaselineComparer.TemplateComparison
{
    public class DirectoryComparer
    {
        public DirectoryComparer(string baselineBaseDir, string secondaryBaseDir, string commonSubdir)
        {
            if (commonSubdir.EndsWith("\\"))
            {
                _baselineDataDir = Path.Combine(baselineBaseDir, commonSubdir);
                _secondaryDataDir = Path.Combine(secondaryBaseDir, commonSubdir);
            }
            else
            {
                _baselineDataDir = Path.Combine(baselineBaseDir, commonSubdir) + "\\";
                _secondaryDataDir = Path.Combine(secondaryBaseDir, commonSubdir) + "\\";
            }

            _commonSubdir = commonSubdir;
        }

        private readonly string _baselineDataDir;
        private readonly string _secondaryDataDir;
        private readonly string _commonSubdir;

        public DirectoryDifference Compare()
        {
            DirectoryDifference allResults = new DirectoryDifference(_commonSubdir);

            HashSet<string> baselineFilenameLookup = new HashSet<string>();
            List<string> baselineOnly = new List<string>();

            foreach (string baselineFilename in Directory.EnumerateFiles(_baselineDataDir, "*.*", SearchOption.AllDirectories))
            {
                string relativeFilename = baselineFilename.Substring(_baselineDataDir.Length);
                baselineFilenameLookup.Add(relativeFilename);
                string secondaryFilename = Path.Combine(_secondaryDataDir, relativeFilename);

                FileDifference fileDifference = new FileDifference(relativeFilename);
                allResults.AddFileResult(fileDifference);

                if (!File.Exists(secondaryFilename))
                {
                    fileDifference.MissingSecondaryFile = true;
                }
                else
                {
                    FileComparer fileComparer = new FileComparer(baselineFilename, secondaryFilename);
                    IReadOnlyList<PositionalDifference> differenceList = fileComparer.Compare();

                    if (differenceList.Count > 0)
                    {
                        fileDifference.AddDifferences(differenceList);
                    }
                }
            }

            foreach (string secondaryFilename in Directory.EnumerateFiles(_secondaryDataDir, "*.*", SearchOption.AllDirectories))
            {
                string relativeFilename = secondaryFilename.Substring(_secondaryDataDir.Length);
                if (!baselineFilenameLookup.Contains(relativeFilename))
                {
                    string baselineFilename = Path.Combine(_baselineDataDir, relativeFilename);
                    FileDifference fileDifference = new FileDifference(relativeFilename);
                    fileDifference.MissingBaselineFile = true;
                    allResults.AddFileResult(fileDifference);
                }
            }

            return allResults;
        }
    }
}
