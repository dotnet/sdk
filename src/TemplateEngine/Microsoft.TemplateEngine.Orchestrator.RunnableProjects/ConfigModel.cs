using System;
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
        public string Name { get; set; }

        public string ShortName { get; set; }

        public string DefaultName { get; set; }

        public List<FileSource> Sources { get; set; }

        public Dictionary<string, string> Macros { get; set; }

        public Dictionary<string, Parameter> Parameters { get; set; }

        public Dictionary<string, JObject> Config { get; set; }

        public Dictionary<string, Dictionary<string, JObject>> Special { get; set; }

        IReadOnlyDictionary<string, Parameter> IRunnableProjectConfig.Parameters => Parameters;

        IReadOnlyDictionary<string, Dictionary<string, JObject>> IRunnableProjectConfig.Special => Special;

        IReadOnlyDictionary<string, JObject> IRunnableProjectConfig.Config => Config;

        IReadOnlyList<FileSource> IRunnableProjectConfig.Sources => Sources;

        public string Author { get; set; }

        public IReadOnlyDictionary<string, string> Tags { get; set; }

        public IReadOnlyList<string> Classifications { get; set; }

        public string GroupIdentity { get; set; }

        public IFile SourceFile { get; set; }

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
                    m = FromJObject(model);
                }
            }

            return m;
        }

        public static ConfigModel FromJObject(JObject source)
        {
            ConfigModel temp = new ConfigModel();
            temp.Author = source.ToString(nameof(Author));

            List<string> classifications = new List<string>();
            temp.Classifications = classifications;
            foreach (JToken item in source.Get<JArray>(nameof(Classifications)))
            {
                classifications.Add(item.ToString());
            }

            temp.Config = new Dictionary<string, JObject>();
            foreach (JProperty prop in source.PropertiesOf(nameof(Config)))
            {
                temp.Config[prop.Name] = (JObject) prop.Value;
            }

            temp.DefaultName = source.ToString(nameof(DefaultName));
            temp.GroupIdentity = source.ToString(nameof(GroupIdentity));
            temp.Identity = source.ToString(nameof(Identity));
            temp.Macros = new Dictionary<string, string>();

            foreach (JProperty prop in source.PropertiesOf(nameof(Macros)))
            {
                temp.Macros[prop.Name] = prop.Value.ToString();
            }

            temp.Name = source.ToString(nameof(Name));
            temp.ShortName = source.ToString(nameof(ShortName));
            temp.Special = new Dictionary<string, Dictionary<string, JObject>>();

            foreach (JProperty prop in source.PropertiesOf(nameof(Special)))
            {
                Dictionary<string, JObject> current = temp.Special[prop.Name] = new Dictionary<string, JObject>();

                foreach (JProperty child in ((JObject) prop.Value).Properties())
                {
                    current[child.Name] = (JObject) child.Value;
                }
            }

            temp.Sources = new List<FileSource>();

            foreach (JToken item in source.Get<JArray>(nameof(Sources)))
            {
                FileSource src = new FileSource();
                src.Source = item.ToString(nameof(src.Source));
                src.Target = item.ToString(nameof(src.Target));
                src.Include = item.Get<JArray>(nameof(src.Include)).ArrayAsStrings();
                src.Exclude = item.Get<JArray>(nameof(src.Exclude)).ArrayAsStrings();
                src.CopyOnly = item.Get<JArray>(nameof(src.CopyOnly)).ArrayAsStrings();
                src.Rename = item.ToStringDictionary(StringComparer.OrdinalIgnoreCase, nameof(src.Rename));
                temp.Sources.Add(src);
            }

            temp.Parameters = new Dictionary<string, Parameter>();

            foreach (JToken item in source.Get<JArray>(nameof(Parameters)))
            {
                Parameter p = new Parameter();
                p.DefaultValue = item.ToString(nameof(p.DefaultValue));
                p.Description = item.ToString(nameof(p.Description));
                p.IsName = item.ToBool(nameof(p.IsName));
                p.IsVariable = item.ToBool(nameof(p.IsVariable));
                p.Name = item.ToString(nameof(p.Name));
                p.Requirement = item.ToEnum<TemplateParameterPriority>(nameof(p.Requirement));
                p.Type = item.ToString(nameof(p.Type));
                temp.Parameters[p.Name] = p;
            }

            Dictionary<string, string> tags = new Dictionary<string, string>();
            temp.Tags = tags;
            foreach (JProperty item in source.PropertiesOf(nameof(Tags)))
            {
                tags[item.Name] = item.Value.ToString();
            }

            return temp;
        }
    }
}
