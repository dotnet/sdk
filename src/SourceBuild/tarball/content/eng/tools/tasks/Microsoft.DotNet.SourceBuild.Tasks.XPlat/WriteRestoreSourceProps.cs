// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks
{
    public class WriteRestoreSourceProps : Task
    {
        [Required]
        public ITaskItem[] RestoreSources { get; set; }

        [Required]
        public string OutputPath { get; set; }

        public override bool Execute()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            using (var outStream = File.Open(OutputPath, FileMode.Create))
            using (var sw = new StreamWriter(outStream, new UTF8Encoding(false)))
            {
                sw.WriteLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
                sw.WriteLine(@"<Project ToolsVersion=""14.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">");
                sw.WriteLine(@"  <PropertyGroup>");
                sw.WriteLine(@"    <DotNetRestoreSources>");
                foreach (ITaskItem restoreSourceItem in RestoreSources)
                {
                    sw.WriteLine($"      {restoreSourceItem.ItemSpec};");
                }
                sw.WriteLine(@"    </DotNetRestoreSources>");
                sw.WriteLine(@"  </PropertyGroup>");
                sw.WriteLine(@"</Project>");
            }

            return true;
        }
    }
}
