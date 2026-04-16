// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class GenerateToolsSettingsFile : TaskBase
    {
        [Required]
        public string EntryPointRelativePath { get; set; }

        [Required]
        public string CommandName { get; set; }

        public string CommandRunner { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string ToolPackageId { get; set; }

        public string ToolPackageVersion { get; set; }

        public ITaskItem[] ToolPackageRuntimeIdentifiers { get; set; }

        [Required]
        public string ToolsSettingsFilePath { get; set; }

        protected override void ExecuteCore()
        {
            GenerateDocument(EntryPointRelativePath, CommandName, CommandRunner, RuntimeIdentifier, ToolPackageId, ToolPackageVersion, ToolPackageRuntimeIdentifiers)
                .Save(ToolsSettingsFilePath);
        }

        internal static XDocument GenerateDocument(string entryPointRelativePath, string commandName, string commandRunner, string runtimeIdentifier,
            string toolPackageId, string toolPackageVersion, ITaskItem[] toolPackageRuntimeIdentifiers)
        {
            // Format version should bump whenever the format changes such that it will break old consumers
            int formatVersion = 1;

            if (string.IsNullOrEmpty(commandRunner))
            {
                commandRunner = "dotnet";
            }

            if (commandRunner != "dotnet")
            {
                if (commandRunner == "executable")
                {
                    formatVersion = 2;
                }
                else
                {
                    throw new BuildErrorException(
                        string.Format(
                            Strings.UnsupportedToolCommandRunner,
                            commandRunner));
                }
            }

            XElement runtimeIdentifierPackagesNode = null;
            XElement commandNode = new XElement("Command",
                          new XAttribute("Name", commandName));

            //  Only generate RuntimeIdentifierPackages node for the primary package, when RuntimeIdentifier isn't set
            if (string.IsNullOrEmpty(runtimeIdentifier) && (toolPackageRuntimeIdentifiers?.Any() ?? false))
            {
                formatVersion = 2;
                runtimeIdentifierPackagesNode = new XElement("RuntimeIdentifierPackages");
                foreach (var runtimeIdentifierPackage in toolPackageRuntimeIdentifiers)
                {
                    string toolPackageRuntimeIdentifier = runtimeIdentifierPackage.ItemSpec;

                    var packageNode = new XElement("RuntimeIdentifierPackage");
                    packageNode.Add(new XAttribute("RuntimeIdentifier", toolPackageRuntimeIdentifier));

                    string ridPackageId = toolPackageId + "." + toolPackageRuntimeIdentifier;
                    packageNode.Add(new XAttribute("Id", ridPackageId));

                    runtimeIdentifierPackagesNode.Add(packageNode);
                }
            }
            else
            {
                //  EntryPoint and Runner are only set in packages with tool implementation, not in primary packages
                //  when there are RID-specific tool packages
                commandNode.Add(new XAttribute("EntryPoint", entryPointRelativePath),
                                new XAttribute("Runner", commandRunner));
            }

            var dotnetCliToolNode = new XElement("DotNetCliTool",
                        new XAttribute("Version", formatVersion),
                        new XElement("Commands", commandNode));

            if (runtimeIdentifierPackagesNode != null)
            {
                dotnetCliToolNode.Add(runtimeIdentifierPackagesNode);
            }

            return new XDocument(
                new XDeclaration(version: null, encoding: null, standalone: null),
                dotnetCliToolNode);
        }
    }
}
