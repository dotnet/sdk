// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    public class ReplaceRegexInFiles : Task
    {
        [Required]
        public string[] InputFiles { get; set; }

        [Required]
        public string OldTextRegex { get; set; }

        [Required]
        public string NewText { get; set; }

        public override bool Execute()
        {
            Log.LogMessage($"Replacing '{OldTextRegex}' with '{NewText}'");
            foreach (string file in InputFiles)
            {
                string fileContents = File.ReadAllText(file);

                fileContents = Regex.Replace(fileContents, OldTextRegex, NewText);

                File.WriteAllText(file, fileContents);
            }

            return true;
        }
    }
}
