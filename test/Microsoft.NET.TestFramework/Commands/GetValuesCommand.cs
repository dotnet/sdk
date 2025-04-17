// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class GetValuesCommand : MSBuildCommand
    {
        public enum ValueType
        {
            Property,
            Item
        }

        string _targetFramework;

        string _valueName;
        ValueType _valueType;

        public bool ShouldCompile { get; set; } = true;

        public string DependsOnTargets { get; set; } = "Compile";

        public string TargetName { get; set; } = "WriteValuesToFile";

        public string? Configuration { get; set; }

        public List<string> MetadataNames { get; set; } = new List<string>();
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        public bool ShouldRestore { get; set; } = true;

        protected override bool ExecuteWithRestoreByDefault => ShouldRestore;

        public GetValuesCommand(ITestOutputHelper log, string projectPath, string targetFramework,
            string valueName, ValueType valueType = ValueType.Property)
            : base(log, "WriteValuesToFile", projectPath, relativePathToProject: null)
        {
            _targetFramework = targetFramework;

            _valueName = valueName;
            _valueType = valueType;
        }

        public GetValuesCommand(TestAsset testAsset,
            string valueName, ValueType valueType = ValueType.Property,
            string? targetFramework = null)
            : base(testAsset, "WriteValuesToFile", relativePathToProject: null)
        {
            _targetFramework = targetFramework ?? OutputPathCalculator.FromProject(ProjectFile, testAsset).TargetFramework ?? string.Empty;

            _valueName = valueName;
            _valueType = valueType;
        }

        protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            var newArgs = new List<string>();
            newArgs.Add(FullPathProjectFile);

            newArgs.Add($"/p:ValueName={_valueName}");
            newArgs.AddRange(args);

            Directory.CreateDirectory(GetBaseIntermediateDirectory().FullName);
            string customAfterDirectoryBuildTargetsPath = Path.Combine(
                GetBaseIntermediateDirectory().FullName,
                "Custom.After.Directory.Build.targets");

            var project = XDocument.Load(ProjectFile);

            if(project.Root is null)
            {
                throw new InvalidOperationException($"The project file '{ProjectFile}' does not have a root element.");
            }

            var ns = project.Root.Name.Namespace;

            string linesAttribute;
            if (_valueType == ValueType.Property)
            {
                linesAttribute = $"$({_valueName})";
            }
            else
            {
                linesAttribute = $"%({_valueName}.Identity)";
                foreach (var metadataName in MetadataNames)
                {
                    linesAttribute += $"%09%({_valueName}.{metadataName})";
                }
            }

            var propertyGroup = project.Root.Elements(ns + "PropertyGroup").FirstOrDefault();

            if (propertyGroup == null)
            {
                propertyGroup = new XElement(ns + "PropertyGroup");
                project.Root.AddAfterSelf(propertyGroup);
            }
            
            propertyGroup.Add(new XElement(ns + "CustomAfterDirectoryBuildTargets", $"$(CustomAfterDirectoryBuildTargets);{customAfterDirectoryBuildTargetsPath}"));
            propertyGroup.Add(new XElement(ns + "CustomAfterMicrosoftCommonCrossTargetingTargets", $"$(CustomAfterMicrosoftCommonCrossTargetingTargets);{customAfterDirectoryBuildTargetsPath}"));

            project.Save(ProjectFile);

            var customAfterDirectoryBuildTargets = new XDocument(new XElement(ns + "Project"));

            var target = new XElement(ns + "Target",
                new XAttribute("Name", TargetName),
                ShouldCompile ? new XAttribute("DependsOnTargets", DependsOnTargets) : null);

            customAfterDirectoryBuildTargets.Root?.Add(target);

            if (Properties.Count != 0)
            {
                propertyGroup = new XElement(ns + "PropertyGroup");
                customAfterDirectoryBuildTargets.Root?.Add(propertyGroup);

                foreach (var pair in Properties)
                {
                    propertyGroup.Add(new XElement(ns + pair.Key, pair.Value));
                }
            }

            var itemGroup = new XElement(ns + "ItemGroup");
            target.Add(itemGroup);

            itemGroup.Add(
                new XElement(ns + "LinesToWrite",
                    new XAttribute("Include", linesAttribute)));

            target.Add(
                new XElement(ns + "WriteLinesToFile",
                    new XAttribute("File", $@"bin\$(Configuration)\$(TargetFramework)\{_valueName}Values.txt"),
                    new XAttribute("Lines", "@(LinesToWrite)"),
                    new XAttribute("Overwrite", bool.TrueString),
                    new XAttribute("Encoding", "Unicode")));

            customAfterDirectoryBuildTargets.Save(customAfterDirectoryBuildTargetsPath);

            var outputDirectory = GetValuesOutputDirectory(_targetFramework);
            outputDirectory.Create();

            return TestContext.Current.ToolsetUnderTest.CreateCommandForTarget(TargetName, newArgs);
        }

        public List<string> GetValues()
        {
            return GetValuesWithMetadata().Select(t => t.value).ToList();
        }

        public List<(string value, Dictionary<string, string> metadata)> GetValuesWithMetadata()
        {
            string outputFilename = $"{_valueName}Values.txt";
            var outputDirectory = GetValuesOutputDirectory(_targetFramework, Configuration ?? "Debug");
            string fullFileName = Path.Combine(outputDirectory.FullName, outputFilename);

            if (File.Exists(fullFileName))
            {
                return File.ReadAllLines(fullFileName)
                   .Where(line => !string.IsNullOrWhiteSpace(line))
                   .Select(line =>
                   {
                       if (!MetadataNames.Any())
                       {
                           return (value: line, metadata: new Dictionary<string, string>());
                       }
                       else
                       {
                           var fields = line.Split('\t');

                           var dict = new Dictionary<string, string>();
                           for (int i = 0; i < MetadataNames.Count; i++)
                           {
                               dict[MetadataNames[i]] = fields[i + 1];
                           }

                           return (value: fields[0], metadata: dict);
                       }
                   })
                   .ToList();
            }
            else
            {
                return new List<(string value, Dictionary<string, string> metadata)>();
            }
        }

        DirectoryInfo GetValuesOutputDirectory(string targetFramework = "", string configuration = "Debug")
        {
            //  Use a consistent directory format to put the values text file in, so we don't have to worry about
            //  whether the project uses the standard output path format or not

            targetFramework = targetFramework ?? string.Empty;
            configuration = configuration ?? string.Empty;

            string output = Path.Combine(ProjectRootPath, "bin", configuration, targetFramework);
            return new DirectoryInfo(output);
        }
    }
}
