using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BaselineComparer.TemplateComparison
{
    public class DirectoryDifference
    {
        public DirectoryDifference(string dataCommonSubdir)
        {
            DataCommonSubdir = dataCommonSubdir;
            _fileResults = new List<FileDifference>();
        }

        public void AddFileResult(FileDifference fileDiff)
        {
            _fileResults.Add(fileDiff);
        }

        private List<FileDifference> _fileResults;

        [JsonProperty]
        public string DataCommonSubdir { get; set; }

        [JsonProperty]
        public IReadOnlyList<FileDifference> FileResults => _fileResults;

        [JsonIgnore]
        public IReadOnlyList<string> MissingBaselineFiles
        {
            get
            {
                return _fileResults.Where(file => file.MissingBaselineFile).Select(x => x.File).ToList();
            }
        }

        [JsonIgnore]
        public IReadOnlyList<string> MissingSecondaryFiles
        {
            get
            {
                return _fileResults.Where(file => file.MissingSecondaryFile).Select(x => x.File).ToList();
            }
        }

        // The comparison should only be valid for different runs of the same templates.
        // The baseline isn't valid if:
        //      - There are any files in one directory but not the other.
        //      - Any of the files had differences deemed "too long"
        public bool IsValidBaseline
        {
            get
            {
                if (MissingBaselineFiles.Count > 0)
                {
                    return false;
                }

                if (MissingSecondaryFiles.Count > 0)
                {
                    return false;
                }

                if (_fileResults.Any(file => file.Differences.Any(d => d.Classification == DifferenceDatatype.TooLong)))
                {
                    return false;
                }

                return true;
            }
        }

        public static DirectoryDifference FromJObject(JObject source)
        {
            string dataCommonSubdir = source.GetValue(nameof(DataCommonSubdir)).ToString();
            DirectoryDifference deserialized = new DirectoryDifference(dataCommonSubdir);

            foreach (JObject fileInfo in source.GetValue(nameof(FileResults)))
            {
                FileDifference fileDiff = FileDifference.FromJObject(fileInfo);
                deserialized.AddFileResult(fileDiff);
            }

            return deserialized;
        }
    }
}
