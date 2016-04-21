using System.Collections.Generic;
using Mutant.Chicken.Runner;
using Newtonsoft.Json;

namespace Mutant.Chicken.Demo
{
    public class DemoGlobalRunSpec : IGlobalRunSpec
    {
        [JsonProperty]
        public IReadOnlyList<IPathMatcher> Exclude { get; set; }

        [JsonProperty]
        public IReadOnlyList<IPathMatcher> Include { get; set; }

        [JsonProperty]
        public IReadOnlyList<IOperationProvider> Operations { get; set; }

        [JsonProperty]
        public VariableCollection RootVariableCollection { get; set; }

        [JsonProperty("special"), JsonConverter(typeof(SpecialConverter))]
        public Dictionary<IPathMatcher, IRunSpec> Special { get; set; }

        IReadOnlyDictionary<IPathMatcher, IRunSpec> IGlobalRunSpec.Special => Special;

        public DemoGlobalRunSpec()
        {
            Special = new Dictionary<IPathMatcher, IRunSpec>();
        }
    }
}