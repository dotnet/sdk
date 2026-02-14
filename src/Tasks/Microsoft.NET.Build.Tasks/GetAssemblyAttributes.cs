// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    [MSBuildMultiThreadableTask]
    public class GetAssemblyAttributes : TaskBase, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        [Required]
        public string PathToTemplateFile { get; set; }

        [Output]
        public ITaskItem[] AssemblyAttributes { get; private set; }

        protected override void ExecuteCore()
        {
            AbsolutePath templatePath = TaskEnvironment.GetAbsolutePath(PathToTemplateFile);
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(templatePath);
            Version assemblyVersion = FileUtilities.TryGetAssemblyVersion(templatePath);

            AssemblyAttributes = FormatToAttributes(AssemblyAttributesNameByFieldInFileVersionInfo: new Dictionary<string, string>
            {
                ["System.Reflection.AssemblyCompanyAttribute"] = fileVersionInfo.CompanyName,
                ["System.Reflection.AssemblyCopyrightAttribute"] = fileVersionInfo.LegalCopyright,
                ["System.Reflection.AssemblyDescriptionAttribute"] = fileVersionInfo.Comments,
                ["System.Reflection.AssemblyFileVersionAttribute"] = fileVersionInfo.FileVersion,
                ["System.Reflection.AssemblyInformationalVersionAttribute"] = fileVersionInfo.ProductVersion,
                ["System.Reflection.AssemblyProductAttribute"] = fileVersionInfo.ProductName,
                ["System.Reflection.AssemblyTitleAttribute"] = fileVersionInfo.FileDescription,
                ["System.Reflection.AssemblyVersionAttribute"] = assemblyVersion != null ? assemblyVersion.ToString() : string.Empty
            });
        }

        private ITaskItem[] FormatToAttributes(IDictionary<string, string> AssemblyAttributesNameByFieldInFileVersionInfo)
        {
            if (AssemblyAttributesNameByFieldInFileVersionInfo == null)
            {
                AssemblyAttributesNameByFieldInFileVersionInfo = new Dictionary<string, string>();
            }

            return AssemblyAttributesNameByFieldInFileVersionInfo
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv =>
                {
                    var item = new TaskItem(kv.Key);
                    item.SetMetadata("_Parameter1", kv.Value);
                    return item;
                })
                .ToArray();
        }
    }
}
