﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System;
using Microsoft.Build.Utilities;
using System.Linq;

namespace Microsoft.NET.Build.Tasks
{
    public class GetAssemblyAttributes : TaskBase
    {
        [Required]
        public string PathToTemplateFile { get; set; }

        [Output]
        public ITaskItem[] AssemblyAttributes { get; private set; }

        protected override void ExecuteCore()
        {
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(Path.GetFullPath(PathToTemplateFile));
            Version assemblyVersion = FileUtilities.TryGetAssemblyVersion(Path.GetFullPath(PathToTemplateFile));

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
