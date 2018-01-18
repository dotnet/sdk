using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BaselineComparer
{
    // The master baseline file from a set of invocation units
    public class BaselineMasterReport
    {
        [JsonProperty]
        public string NewCommand { get; set; }

        [JsonProperty]
        public IReadOnlyList<InvocationBaselineUnit> Invocations { get; set; }

        public static BaselineMasterReport FromFile(string filename)
        {
            string fileContent;

            try
            {
                fileContent = File.ReadAllText(filename);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to read the specified baseline file: {filename}");
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }

            BaselineMasterReport baselineMasterReport;

            try
            {
                JObject baselineJObject = JObject.Parse(fileContent);
                baselineMasterReport = FromJObject(baselineJObject);
                return baselineMasterReport;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to deserialize the existing baseline data file: {filename}.");
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        public static BaselineMasterReport FromJObject(JObject source)
        {
            string newCommand = source.GetValue(nameof(NewCommand)).ToString();

            List<InvocationBaselineUnit> invocationUnitList = new List<InvocationBaselineUnit>();

            foreach (JToken unitJObject in source.GetValue(nameof(Invocations)))
            {
                InvocationBaselineUnit invocationUnit = InvocationBaselineUnit.FromJObject((JObject)unitJObject);
                invocationUnitList.Add(invocationUnit);
            }

            return new BaselineMasterReport()
            {
                NewCommand = newCommand,
                Invocations = invocationUnitList
            };
        }
    }
}
