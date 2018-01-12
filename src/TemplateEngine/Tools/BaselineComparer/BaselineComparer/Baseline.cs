using System;
using System.Collections.Generic;
using System.IO;
using BaselineComparer.TemplateComparison;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BaselineComparer
{
    public class Baseline
    {
        public Baseline(string baselineDirectory, string secondaryDirectory, string newCommand, IReadOnlyList<string> templateCommands, DirectoryDifference differences)
        {
            BaselineDirectory = baselineDirectory;
            SecondaryDirectory = secondaryDirectory;
            NewCommand = newCommand;
            TemplateCommands = templateCommands;
            FileResults = differences;
        }

        [JsonProperty]
        public string BaselineDirectory { get; }

        [JsonProperty]
        public string SecondaryDirectory { get; }

        [JsonProperty]
        public string NewCommand { get; }

        [JsonProperty]
        public IReadOnlyList<string> TemplateCommands { get; }

        [JsonProperty]
        public DirectoryDifference FileResults { get; }

        public static Baseline FromFile(string baselineFilename)
        {
            string baselineContent;

            try
            {
                baselineContent = File.ReadAllText(baselineFilename);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to read the specified baseline file: {baselineFilename}");
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }

            Baseline baseline;

            try
            {
                JObject baselineJObject = JObject.Parse(baselineContent);
                baseline = FromJObject(baselineJObject);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to deserialize the existing baseline data.");
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }

            return baseline;
        }

        public static Baseline FromJObject(JObject source)
        {
            string baselineDirectory = source.GetValue(nameof(BaselineDirectory)).ToString();
            string secondaryDirectory = source.GetValue(nameof(SecondaryDirectory)).ToString();
            string newCommand = source.GetValue(nameof(NewCommand)).ToString();

            List<string> templateCommandList = new List<string>();
            JToken commandsToken = source.GetValue(nameof(TemplateCommands));
            foreach (string templateCommand in commandsToken.Values<string>())
            {
                templateCommandList.Add(templateCommand);
            }

            JToken differenceToken = source.GetValue(nameof(FileResults));
            DirectoryDifference directoryDifference = DirectoryDifference.FromJObject((JObject)differenceToken);

            return new Baseline(baselineDirectory, secondaryDirectory, newCommand, templateCommandList, directoryDifference);
        }
    }
}
