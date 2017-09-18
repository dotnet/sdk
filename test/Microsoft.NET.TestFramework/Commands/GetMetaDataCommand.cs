using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{

    public sealed class GetMetaDataCommand : MSBuildCommand
    {
        public bool ShouldCompile { get; set; } = true;

        public string DependsOnTargets { get; set; } = "Compile";

        public string Configuration { get; set; }

        private const string WriteMetaDataToFile = "WriteMetaDataToFile";

        private readonly IEnumerable<MetaDataIdentifier> metaDataToGet;

        private string _targetFramework;

        public GetMetaDataCommand(ITestOutputHelper log,
            IEnumerable<MetaDataIdentifier> metaDataToGet,
            string projectPath,
            string targetFramework,
            MSBuildTest msbuild = null) :
            base(log,
            WriteMetaDataToFile,
            projectPath,
            relativePathToProject : null,
            msbuild : msbuild)
        {
            this.metaDataToGet = metaDataToGet;
            this._targetFramework = targetFramework;
        }

        public string GetMetaDataValue(MetaDataIdentifier metaDataIdentifier)
        {
            string outputFilename = $"{metaDataIdentifier.GetReadableUniqueName()}Values.txt";
            var outputDirectory = GetOutputDirectory(_targetFramework, Configuration ?? "Debug");
            var filePath = Path.Combine(outputDirectory.FullName, outputFilename);
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath).Trim();
            }
            else
            {
                return string.Empty;
            }
        }

        protected override ICommand CreateCommand(params string[] args)
        {
            var newArgs = new List<string>(args.Length + 2);
            newArgs.Add(FullPathProjectFile);
            newArgs.AddRange(args);

            OverrideBuildTargetToWriteOutValue();

            var outputDirectory = GetOutputDirectory(_targetFramework);
            outputDirectory.Create();

            return MSBuild.CreateCommandForTarget(WriteMetaDataToFile, newArgs.ToArray());
        }

        private void OverrideBuildTargetToWriteOutValue()
        {
            Directory.CreateDirectory(GetBaseIntermediateDirectory().FullName);
            string injectTargetPath = Path.Combine(
                GetBaseIntermediateDirectory().FullName,
                Path.GetFileName(ProjectFile) + $".{WriteMetaDataToFile}.g.targets");

            string writeLineTasks = "";
            foreach (var m in metaDataToGet)
            {
                writeLineTasks = writeLineTasks + $@"
    <WriteLinesToFile
      File=`bin\$(Configuration)\$(TargetFramework)\{m.GetReadableUniqueName()}Values.txt`
      Lines=`%({m.ItemGroupName}.{m.MetaDataName})`
      Overwrite=`true`
      Encoding=`Unicode`
      Condition=`'%({m.ItemGroupName}.Identity)' == '{m.ItemName}'`
      />" + "\n";
            }

            string injectTargetContents =
$@"<Project ToolsVersion=`14.0` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
  <PropertyGroup>
    <MSBuildAllProjects>$(MSBuildAllProjects);$(MSBuildThisFileFullPath)</MSBuildAllProjects>
  </PropertyGroup>
  <Target Name=`{WriteMetaDataToFile}` " + (ShouldCompile ? $"DependsOnTargets=`{DependsOnTargets}`" : "") + $@">
    {writeLineTasks}
  </Target>
</Project>";
            injectTargetContents = injectTargetContents.Replace('`', '"');

            File.WriteAllText(injectTargetPath, injectTargetContents);
        }
    }

    public class MetaDataIdentifier
    {
        public MetaDataIdentifier(string itemGroupName, string itemName, string metaDataName)
        {
            ItemGroupName = itemGroupName;
            ItemName = itemName;
            MetaDataName = metaDataName;
        }

        public string ItemGroupName { get; }
        public string ItemName { get; }
        public string MetaDataName { get; }

        public string GetReadableUniqueName()
        {
            return MakeSafeForFileName($"{ItemGroupName}_{ItemName}_{MetaDataName}");
        }

        public string MakeSafeForFileName(string s)
        {
            var invalids = System.IO.Path.GetInvalidFileNameChars();
            return String.Join("_", s.Split(invalids, StringSplitOptions.RemoveEmptyEntries) ).TrimEnd('.');
        }
    }
}
