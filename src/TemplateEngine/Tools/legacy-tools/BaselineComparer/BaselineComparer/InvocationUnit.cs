using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BaselineComparer
{
    public class InvocationUnit
    {
        public InvocationUnit(string name, IReadOnlyList<string> installRequirements, IReadOnlyList<string> invocationCommands)
        {
            Name = name;
            InstallRequirements = installRequirements;
            InvocationCommands = invocationCommands;
        }

        public string Name { get; }
        public IReadOnlyList<string> InstallRequirements { get; }
        public IReadOnlyList<string> InvocationCommands { get; }

        public static InvocationUnit FromJObject(JObject source)
        {
            string name = source.GetValue(nameof(Name)).ToString();

            List<string> installRequirements = new List<string>();
            JToken installToken = source.GetValue(nameof(InstallRequirements));
            foreach (string toInstall in installToken.Values<string>())
            {
                installRequirements.Add(toInstall);
            }

            List<string> invocationCommands = new List<string>();
            JToken invocationsToken = source.GetValue(nameof(InvocationCommands));
            foreach (string toInvoke in invocationsToken.Values<string>())
            {
                invocationCommands.Add(toInvoke);
            }

            return new InvocationUnit(name, installRequirements, invocationCommands);
        }

        public static InvocationUnit FromInvocationBaselineUnit(InvocationBaselineUnit baselinUnit)
        {
            return new InvocationUnit(baselinUnit.Name, baselinUnit.InstallRequirements, baselinUnit.Invocations.Select(x => x.Command).ToList());
        }
    }
}
