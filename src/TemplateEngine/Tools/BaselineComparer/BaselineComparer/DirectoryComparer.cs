using System.Collections.Generic;
using System.IO;

namespace BaselineComparer
{
    public class DirectoryComparer
    {
        private string _baselineDir;
        private string _checkTargetDir;

        // TODO: make this platform independent. 
        public DirectoryComparer(string baselineDir, string checkTargetDir)
        {
            if (!baselineDir.EndsWith("\\"))
            {
                _baselineDir = baselineDir + "\\";
            }
            else
            {
                _baselineDir = baselineDir;
            }

            if (!checkTargetDir.EndsWith("\\"))
            {
                _checkTargetDir = checkTargetDir + "\\";
            }
            else
            {
                _checkTargetDir = checkTargetDir;
            }
        }

        public DirectoryDifference Compare()
        {
            DirectoryDifference allResults = new DirectoryDifference(_baselineDir, _checkTargetDir);

            HashSet<string> baselineFilenameLookup = new HashSet<string>();
            List<string> baselineOnly = new List<string>();

            foreach (string baselineFilename in Directory.EnumerateFiles(_baselineDir, "*.*", SearchOption.AllDirectories))
            {
                string relativeFilename = baselineFilename.Substring(_baselineDir.Length);
                baselineFilenameLookup.Add(relativeFilename);
                string checkFilename = Path.Combine(_checkTargetDir, relativeFilename);

                FileDifference fileDifference = new FileDifference(relativeFilename);
                allResults.AddFileResult(fileDifference);

                if (!File.Exists(checkFilename))
                {
                    fileDifference.MissingCheckFile = true;
                }
                else
                {
                    FileComparer fileComparer = new FileComparer(baselineFilename, checkFilename);
                    IReadOnlyList<PositionalDifference> differenceList = fileComparer.Compare();

                    if (differenceList.Count > 0)
                    {
                        fileDifference.AddDifferences(differenceList);
                    }
                }
            }

            foreach (string checkFilename in Directory.EnumerateFiles(_checkTargetDir, "*.*", SearchOption.AllDirectories))
            {
                string relativeFilename = checkFilename.Substring(_checkTargetDir.Length);
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
