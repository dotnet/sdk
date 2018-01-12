using System.Collections.Generic;
using System.IO;

namespace BaselineComparer.TemplateComparison
{
    public class DirectoryComparer
    {
        private string _baselineDir;
        private string _secondaryDir;

        // TODO (future enhancement): make this platform independent. 
        public DirectoryComparer(string baselineDir, string secondaryDir)
        {
            if (!baselineDir.EndsWith("\\"))
            {
                _baselineDir = baselineDir + "\\";
            }
            else
            {
                _baselineDir = baselineDir;
            }

            if (!secondaryDir.EndsWith("\\"))
            {
                _secondaryDir = secondaryDir + "\\";
            }
            else
            {
                _secondaryDir = secondaryDir;
            }
        }

        public DirectoryDifference Compare()
        {
            DirectoryDifference allResults = new DirectoryDifference(_baselineDir, _secondaryDir);

            HashSet<string> baselineFilenameLookup = new HashSet<string>();
            List<string> baselineOnly = new List<string>();

            foreach (string baselineFilename in Directory.EnumerateFiles(_baselineDir, "*.*", SearchOption.AllDirectories))
            {
                string relativeFilename = baselineFilename.Substring(_baselineDir.Length);
                baselineFilenameLookup.Add(relativeFilename);
                string secondaryFilename = Path.Combine(_secondaryDir, relativeFilename);

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

            foreach (string secondaryFilename in Directory.EnumerateFiles(_secondaryDir, "*.*", SearchOption.AllDirectories))
            {
                string relativeFilename = secondaryFilename.Substring(_secondaryDir.Length);
                if (!baselineFilenameLookup.Contains(relativeFilename))
                {
                    string baselineFilename = Path.Combine(_baselineDir, relativeFilename);
                    FileDifference fileDifference = new FileDifference(relativeFilename);
                    fileDifference.MissingBaselineFile = true;
                    allResults.AddFileResult(fileDifference);
                }
            }

            return allResults;
        }
    }
}
