using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Cli.Build
{
    public class JsonPropertyParser : Task
    {
        [Required]
        public string[] JFilenames
        {
            get;
            set;
        }

        [Required]
        public string[] JPaths
        {
            get;
            set;
        }

        [Output]
        public ITaskItem[] Value
        {
            get;
            private set;
        }

        public override bool Execute()
        {
            Value = new TaskItem[JFilenames.Length];
            for (var i = 0; i < JFilenames.Length; i++)
            {
                Value[i] = new TaskItem(JFilenames[i]);
                try
                {
                    using (var sr = new StreamReader(JFilenames[i]))
                    {
                        var json = sr.ReadToEnd();
                        var o = JObject.Parse(json);
                        foreach (var path in JPaths)
                        {
                            var lastDot = path.LastIndexOf('.');
                            var name = lastDot == -1 ? path : path.Substring(lastDot + 1);
                            Value[i].SetMetadata(name, o.SelectToken(path).Value<string>());
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.LogErrorFromException(e);
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
