using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Cli.Build
{
    public class JsonPropertyParser : Task
    {
        [Required]
        public string Filename
        {
            get;
            set;
        }

        [Required]
        public string Path
        {
            get;
            set;
        }

        [Output]
        public string Value
        {
            get;
            private set;
        }

        public override bool Execute()
        {
            try
            {
                using (var sr = new StreamReader(Filename))
                {
                    var json = sr.ReadToEnd();
                    var o = JObject.Parse(json);
                    Value = o.SelectToken(Path).Value<string>();
                }
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
