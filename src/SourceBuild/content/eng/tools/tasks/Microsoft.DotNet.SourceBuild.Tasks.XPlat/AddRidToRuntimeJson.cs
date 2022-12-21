// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks
{
    public class AddRidToRuntimeJson:Task
    {
        /// <summary>
        /// [OS name].[version]-[architecture]
        /// </summary>
        [Required]
        public string Rid { get; set; }

        [Required]
        public string RuntimeJson { get; set; }

        private string runtimesIdentifier = "runtimes";

        public override bool Execute()
        {
            string[] ridParts = Rid.Split('-');
            string osNameAndVersion = ridParts[0];
            string[] osParts = osNameAndVersion.Split(new char[] { '.' }, 2);

            if (ridParts.Length < 1 || osParts.Length < 2)
            {
                throw new System.InvalidOperationException($"Unknown rid format {Rid}.");
            }

            // Acquire Rid parts:
            //   osName
            //   version
            //   arch
            string arch = ridParts[1];
            string osName = osParts[0];
            string version = osParts[1];

            JObject projectRoot = ReadProject(RuntimeJson);

            if (projectRoot.SelectToken($"{runtimesIdentifier}.{osName}") == null)
            {
                AddRidToRuntimeGraph(projectRoot, osName, "linux");
                AddRidToRuntimeGraph(projectRoot, $"{osName}-{arch}", osName, $"linux-{arch}");
            }
            if(projectRoot.SelectToken($"{runtimesIdentifier}.{osName}.{version}") == null)
            { 
                AddRidToRuntimeGraph(projectRoot, $"{osName}.{version}", osName);
                AddRidToRuntimeGraph(projectRoot, $"{osName}.{version}-{arch}", $"{osName}.{version}", $"{osName}-{arch}");
            }

            WriteProject(projectRoot, RuntimeJson);
            return true;
        }

        private void AddRidToRuntimeGraph(JObject projectRoot, string name, params string[] imports)
        {
            projectRoot[runtimesIdentifier][name] = new JObject(new JProperty("#import", new JArray(imports)));
        }

        private static JObject ReadProject(string projectJsonPath)
        {
            using (TextReader projectFileReader = File.OpenText(projectJsonPath))
            {
                var projectJsonReader = new JsonTextReader(projectFileReader);
                var serializer = new JsonSerializer();
                return serializer.Deserialize<JObject>(projectJsonReader);
            }
        }
        private static void WriteProject(JObject projectRoot, string projectJsonPath)
        {
            string projectJson = JsonConvert.SerializeObject(projectRoot, Formatting.Indented) + Environment.NewLine;

            if (!File.Exists(projectJsonPath) || !projectJson.Equals(File.ReadAllText(projectJsonPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(projectJsonPath));
                File.WriteAllText(projectJsonPath, projectJson);
            }
        }
    }
}
