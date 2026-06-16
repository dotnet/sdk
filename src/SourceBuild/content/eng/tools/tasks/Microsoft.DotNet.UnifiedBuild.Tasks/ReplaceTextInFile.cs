// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.UnifiedBuild.Tasks
{
    public class ReplaceTextInFile : Task
    {
        [Required]
        public string InputFile { get; set; }

        [Required]
        public string OldText { get; set; }

        [Required]
        public string NewText { get; set; }


        public override bool Execute()
        {
            string fileContents = File.ReadAllText(InputFile);
            string newLineChars = FileUtilities.DetectNewLineChars(fileContents);

            fileContents = fileContents.Replace(OldText, NewText);

            File.WriteAllText(InputFile, FileUtilities.NormalizeNewLineChars(fileContents, newLineChars));

            return true;
        }
    }
}
