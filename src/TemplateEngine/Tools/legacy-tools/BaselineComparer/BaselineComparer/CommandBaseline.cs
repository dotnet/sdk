using System;
using System.IO;
using BaselineComparer.TemplateComparison;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BaselineComparer
{
    public class CommandBaseline
    {
        [JsonProperty]
        public string InvocationName { get; set; }

        [JsonProperty]
        public string Command { get; set; }

        [JsonProperty]
        public string NewCommand { get; set; }

        [JsonProperty]
        public DirectoryDifference FileResults { get; set; }

        public static CommandBaseline FromFile(string filename)
        {
            string fileContent;

            try
            {
                fileContent = File.ReadAllText(filename);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to read the specified command baseline file: {filename}");
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }

            CommandBaseline differenceReport;

            try
            {
                JObject reportJObject = JObject.Parse(fileContent);
                differenceReport = FromJObject(reportJObject);
                return differenceReport;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to deserialize the existing differce report file: {filename}");
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        public static CommandBaseline FromJObject(JObject source)
        {
            string invocationName = source.GetValue(nameof(InvocationName)).ToString();
            string command = source.GetValue(nameof(Command)).ToString();
            string newCommand = source.GetValue(nameof(NewCommand)).ToString();

            JObject directoryDifferenceJObject = (JObject)source.GetValue(nameof(FileResults));
            DirectoryDifference fileResults = DirectoryDifference.FromJObject(directoryDifferenceJObject);

            return new CommandBaseline()
            {
                InvocationName = invocationName,
                Command = command,
                NewCommand = newCommand,
                FileResults = fileResults
            };
        }
    }
}
