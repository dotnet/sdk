// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class GetAssemblyAttributes : TaskBase
#if NET10_0_OR_GREATER
    , IMultiThreadableTask
#endif
    {
        [Required]
        public string PathToTemplateFile { get; set; }

        [Output]
        public ITaskItem[] AssemblyAttributes { get; private set; }

#if NET10_0_OR_GREATER
        public TaskEnvironment TaskEnvironment { get; set; }
#endif

        protected override void ExecuteCore()
        {
#if NET10_0_OR_GREATER
            string fullPath = TaskEnvironment.GetAbsolutePath(PathToTemplateFile);
#else
            string fullPath = Path.GetFullPath(PathToTemplateFile);
#endif
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(fullPath);
            Version assemblyVersion = FileUtilities.TryGetAssemblyVersion(fullPath);

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
