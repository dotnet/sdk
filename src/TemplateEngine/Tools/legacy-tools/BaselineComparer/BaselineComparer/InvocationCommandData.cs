using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace BaselineComparer
{
    // For reading a command file for initial baseline creation.
    public class InvocationCommandData
    {
        public InvocationCommandData(IReadOnlyList<InvocationUnit> invocations)
        {
            Invocations = invocations;
        }

        public IReadOnlyList<InvocationUnit> Invocations { get; }

        public static InvocationCommandData FromFile(string filename)
        {
            string fileContents;

            try
            {
                fileContents = File.ReadAllText(filename);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to read the specified command file: {filename}");
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }

            InvocationCommandData commandData;

            try
            {
                JObject source = JObject.Parse(fileContents);
                commandData = FromJObject(source);
                return commandData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to deserialize the invocation command file: {filename}");
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        public static InvocationCommandData FromJObject(JObject source)
        {
            List<InvocationUnit> invocationList = new List<InvocationUnit>();
            JToken invocationListToken = source.GetValue(nameof(Invocations));

            foreach (JObject invocationUnit in (JArray)invocationListToken)
            {
                InvocationUnit unit = InvocationUnit.FromJObject(invocationUnit);
                invocationList.Add(unit);
            }

            return new InvocationCommandData(invocationList);
        }
    }
}
