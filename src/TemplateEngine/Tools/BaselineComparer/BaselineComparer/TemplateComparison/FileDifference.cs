using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BaselineComparer.TemplateComparison
{
    public class FileDifference
    {
        private List<PositionalDifference> _differences;
        private bool _missingBaselineFile;
        private bool _missingCheckFile;

        public FileDifference(string file)
        {
            File = file;
            _differences = new List<PositionalDifference>();
        }

        [JsonProperty]
        public string File { get; }

        public void AddDifference(PositionalDifference difference)
        {
            if (MissingBaselineFile || MissingSecondaryFile)
            {
                throw new Exception("Cant have differences if a file is missing.");
            }

            _differences.Add(difference);
        }

        public void AddDifferences(IEnumerable<PositionalDifference> differeceList)
        {
            if (MissingBaselineFile || MissingSecondaryFile)
            {
                throw new Exception("Cant have differences if a file is missing.");
            }

            _differences.AddRange(differeceList);
        }

        [JsonProperty]
        public IReadOnlyList<PositionalDifference> Differences => _differences;

        public bool ShouldSerializeDifferences()
        {
            return Differences.Count > 0;
        }

        [JsonProperty]
        public bool MissingBaselineFile
        {
            get
            {
                return _missingBaselineFile;
            }
            set
            {
                if (_differences.Count > 0)
                {
                    throw new Exception("Cant label file as missing - there are registered differences.");
                }

                _missingBaselineFile = value;
            }
        }

        public bool ShouldSerializeMissingBaselineFile()
        {
            return MissingBaselineFile;
        }

        [JsonProperty]
        public bool MissingSecondaryFile
        {
            get
            {
                return _missingCheckFile;
            }
            set
            {
                if (_differences.Count > 0)
                {
                    throw new Exception("Cant label file as missing - there are registered differences.");
                }

                _missingCheckFile = value;
            }
        }

        public bool ShouldSerializeMissingSecondaryFile()
        {
            return MissingSecondaryFile;
        }

        public static FileDifference FromJObject(JObject source)
        {
            string filename = source.GetValue(nameof(File)).ToString();

            FileDifference fileDifference = new FileDifference(filename);

            if (source.TryGetValue(nameof(Differences), out JToken differenceJToken))
            {
                foreach (JObject diffInfo in (JArray)differenceJToken)
                {
                    PositionalDifference positionalDiff = PositionalDifference.FromJObject(diffInfo);
                    fileDifference.AddDifference(positionalDiff);
                }
            }

            return fileDifference;
        }
    }
}
