﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class SdkRazorGenerate : DotNetToolTask
    {
        private static readonly string[] SourceRequiredMetadata = new string[]
        {
            FullPath,
            GeneratedOutput,
            TargetPath,
        };

        private const string GeneratedOutput = "GeneratedOutput";
        private const string DocumentKind = "DocumentKind";
        private const string TargetPath = "TargetPath";
        private const string FullPath = "FullPath";
        private const string Identity = "Identity";
        private const string AssemblyName = "AssemblyName";
        private const string AssemblyFilePath = "AssemblyFilePath";
        private const string CssScope = "CssScope";

        public string RootNamespace { get; set; }

        public string CSharpLanguageVersion { get; set; }

        [Required]
        public string Version { get; set; }

        [Required]
        public ITaskItem[] Configuration { get; set; }

        [Required]
        public ITaskItem[] Extensions { get; set; }

        [Required]
        public ITaskItem[] Sources { get; set; }

        [Required]
        public string ProjectRoot { get; set; }

        [Required]
        public string TagHelperManifest { get; set; }

        public bool GenerateDeclaration { get; set; }

        public bool SupportLocalizedComponentNames { get; set; }

        internal override string Command => "generate";

        protected override bool ValidateParameters()
        {
            if (!Directory.Exists(ProjectRoot))
            {
                Log.LogError("The specified project root directory {0} doesn't exist.", ProjectRoot);
                return false;
            }

            if (Configuration.Length == 0)
            {
                Log.LogError("The project {0} must provide a value for {1}.", ProjectRoot, nameof(Configuration));
                return false;
            }

            for (var i = 0; i < Sources.Length; i++)
            {
                if (!EnsureRequiredMetadata(Sources[i], FullPath) ||
                    !EnsureRequiredMetadata(Sources[i], GeneratedOutput) ||
                    !EnsureRequiredMetadata(Sources[i], TargetPath))
                {
                    Log.LogError("The Razor source item '{0}' is missing a required metadata entry. Required metadata are: '{1}'", Sources[i], SourceRequiredMetadata);
                    return false;
                }
            }

            for (var i = 0; i < Extensions.Length; i++)
            {
                if (!EnsureRequiredMetadata(Extensions[i], Identity) ||
                    !EnsureRequiredMetadata(Extensions[i], AssemblyName) ||
                    !EnsureRequiredMetadata(Extensions[i], AssemblyFilePath))
                {
                    return false;
                }
            }

            return base.ValidateParameters();
        }

        protected override string GenerateResponseFileCommands()
        {
            var builder = new StringBuilder();

            builder.AppendLine(Command);

            // We might be talking to a downlevel version of the command line tool, which doesn't
            // understand certain parameters. Assume 2.1 if we can't parse the version because 2.1
            // 2.2 are the releases that have command line tool delivered by a package.
            if (!System.Version.TryParse(Version, out var parsedVersion))
            {
                parsedVersion = new System.Version(2, 1);
            }

            for (var i = 0; i < Sources.Length; i++)
            {
                var input = Sources[i];
                builder.AppendLine("-s");
                builder.AppendLine(input.GetMetadata(FullPath));

                builder.AppendLine("-r");
                builder.AppendLine(input.GetMetadata(TargetPath));

                builder.AppendLine("-o");
                var outputPath = Path.Combine(ProjectRoot, input.GetMetadata(GeneratedOutput));
                builder.AppendLine(outputPath);

                // Added in 3.0
                if (parsedVersion.Major >= 3)
                {
                    var kind = input.GetMetadata(DocumentKind);
                    if (!string.IsNullOrEmpty(kind))
                    {
                        builder.AppendLine("-k");
                        builder.AppendLine(kind);
                    }
                }
            }

            // Added in 5.0: CSS scopes
            if (parsedVersion.Major >= 5)
            {
                for (var i = 0; i < Sources.Length; i++)
                {
                    // Most inputs won't have an associated CSS scope, so we only want to generate
                    // a scope parameter for those that do. Hence we need to specify in the parameter
                    // which one we're talking about.
                    var input = Sources[i];
                    var cssScope = input.GetMetadata(CssScope);
                    if (!string.IsNullOrEmpty(cssScope))
                    {
                        builder.AppendLine("-cssscopedinput");
                        builder.AppendLine(input.GetMetadata(FullPath));
                        builder.AppendLine("-cssscopevalue");
                        builder.AppendLine(cssScope);
                    }
                }
            }

            builder.AppendLine("-p");
            builder.AppendLine(ProjectRoot);

            builder.AppendLine("-t");
            builder.AppendLine(TagHelperManifest);

            builder.AppendLine("-v");
            builder.AppendLine(Version);

            builder.AppendLine("-c");
            builder.AppendLine(Configuration[0].GetMetadata(Identity));

            // Added in 3.0
            if (parsedVersion.Major >= 3)
            {
                if (!string.IsNullOrEmpty(RootNamespace))
                {
                    builder.AppendLine("--root-namespace");
                    builder.AppendLine(RootNamespace);
                }

                if (GenerateDeclaration)
                {
                    builder.AppendLine("--generate-declaration");
                }
            }

            if (SupportLocalizedComponentNames)
            {
                builder.AppendLine("--support-localized-component-names");
            }

            if (!string.IsNullOrEmpty(CSharpLanguageVersion))
            {
                builder.AppendLine("--csharp-language-version");
                builder.AppendLine(CSharpLanguageVersion);
            }

            for (var i = 0; i < Extensions.Length; i++)
            {
                builder.AppendLine("-n");
                builder.AppendLine(Extensions[i].GetMetadata(Identity));

                builder.AppendLine("-e");
                builder.AppendLine(Path.GetFullPath(Extensions[i].GetMetadata(AssemblyFilePath)));
            }

            return builder.ToString();
        }

        private bool EnsureRequiredMetadata(ITaskItem item, string metadataName)
        {
            var value = item.GetMetadata(metadataName);
            if (string.IsNullOrEmpty(value))
            {
                Log.LogError($"Missing required metadata '{metadataName}' for '{item.ItemSpec}.");
                return false;
            }

            return true;
        }
    }
}
