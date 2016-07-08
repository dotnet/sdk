using System.Collections.Generic;
using System.IO;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Engine;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class ConfigModel : IRunnableProjectConfig
    {
        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public string ShortName { get; set; }

        [JsonProperty]
        public string DefaultName { get; set; }

        [JsonProperty]
        public FileSource[] Sources { get; set; }

        [JsonProperty]
        public Dictionary<string, string> Macros { get; set; }

        [JsonProperty]
        public Dictionary<string, Parameter> Parameters { get; set; }

        [JsonProperty]
        public Dictionary<string, JObject> Config { get; set; }

        [JsonProperty]
        public Dictionary<string, Dictionary<string, JObject>> Special { get; set; }

        IReadOnlyDictionary<string, Parameter> IRunnableProjectConfig.Parameters => Parameters;

        IReadOnlyDictionary<string, Dictionary<string, JObject>> IRunnableProjectConfig.Special => Special;

        IReadOnlyDictionary<string, JObject> IRunnableProjectConfig.Config => Config;

        IReadOnlyList<FileSource> IRunnableProjectConfig.Sources => Sources;

        [JsonProperty]
        public string Author { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, string> Tags { get; set; }

        [JsonProperty]
        public IReadOnlyList<string> Classifications { get; set; }

        [JsonProperty]
        public string GroupIdentity { get; set; }

        [JsonIgnore]
        public IFile SourceFile { get; set; }

        [JsonProperty]
        public string Identity { get; set; }

        public IRunnableProjectConfig ReprocessWithParameters(IParameterSet parameters, IVariableCollection rootVariableCollection, IFile configFile, IOperationProvider[] operations)
        {
            IProcessor processor = Processor.Create(new EngineConfig(rootVariableCollection), operations);
            IRunnableProjectConfig m;

            using (Stream configStream = configFile.OpenRead())
            using (Stream targetStream = new MemoryStream())
            {
                processor.Run(configStream, targetStream);
                targetStream.Position = 0;

                using (TextReader tr = new StreamReader(targetStream, true))
                using (JsonReader r = new JsonTextReader(tr))
                {
                    JObject model = JObject.Load(r);
                    m = model.ToObject<ConfigModel>();
                }
            }

            return m;
        }
    }
}
