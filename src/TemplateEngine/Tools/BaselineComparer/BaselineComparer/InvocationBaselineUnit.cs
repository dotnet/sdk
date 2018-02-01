using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BaselineComparer
{
    // part of the master baseline file
    public class InvocationBaselineUnit
    {
        public InvocationBaselineUnit(string name, IReadOnlyList<string> installRequirements, IReadOnlyList<BaselineCommandData> invocations)
        {
            Name = name;
            InstallRequirements = installRequirements;
            Invocations = invocations;
        }

        [JsonProperty]
        public string Name { get; }

        [JsonProperty]
        public IReadOnlyList<string> InstallRequirements { get; }

        public IReadOnlyList<BaselineCommandData> Invocations { get; }

        public static InvocationBaselineUnit FromJObject(JObject source)
        {
            string name = source.GetValue(nameof(Name)).ToString();

            List<string> installRequirementList = new List<string>();
            JToken installToken = source.GetValue(nameof(InstallRequirements));
            foreach (string installItem in installToken.Values<string>())
            {
                installRequirementList.Add(installItem);
            }

            List<BaselineCommandData> invocationData = new List<BaselineCommandData>();

            foreach (JToken invocationJObject in source.GetValue(nameof(Invocations)))
            {
                invocationData.Add(BaselineCommandData.FromJObject((JObject)invocationJObject));
            }

            return new InvocationBaselineUnit(name, installRequirementList, invocationData);
        }
    }
}
