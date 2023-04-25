// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    // Takes a path to a path to a json file and a
    // string that represents a dotted path to an attribute
    // and updates that attribute with the new value provided. 
    public class UpdateJson : Task
    {
        [Required]
        public string JsonFilePath { get; set; }

        [Required]
        public string PathToAttribute { get; set; }

        [Required]
        public string NewAttributeValue { get; set; }

        public bool SkipUpdateIfMissingKey { get; set; }

        public override bool Execute()
        {
            string json = File.ReadAllText(JsonFilePath);
            string newLineChars = FileUtilities.DetectNewLineChars(json);
            JObject jsonObj = JObject.Parse(json);

            string[] escapedPathToAttributeParts = PathToAttribute.Replace("\\.", "\x1F").Split('.');
            for (int i = 0; i < escapedPathToAttributeParts.Length; ++i)
            {
                escapedPathToAttributeParts[i] = escapedPathToAttributeParts[i].Replace("\x1F", ".");
            }
            UpdateAttribute(jsonObj, escapedPathToAttributeParts, NewAttributeValue);

            File.WriteAllText(JsonFilePath, FileUtilities.NormalizeNewLineChars(jsonObj.ToString(), newLineChars));
            return true;
        }

        private void UpdateAttribute(JToken jsonObj, string[] path, string newValue)
        {
            string pathItem = path[0];
            if (jsonObj[pathItem] == null)
            {
                string message = $"Path item [{nameof(PathToAttribute)}] not found in json file.";
                if (SkipUpdateIfMissingKey)
                {
                    Log.LogMessage(MessageImportance.Low, $"Skipping update: {message} {pathItem}");
                    return;
                }
                throw new ArgumentException(message, pathItem);
            }

            if (path.Length == 1) 
            {
                jsonObj[pathItem] = newValue;
                return;
            }

            UpdateAttribute(jsonObj[pathItem], path.Skip(1).ToArray(), newValue);
        }
    }
}
