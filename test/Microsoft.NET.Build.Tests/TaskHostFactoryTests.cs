// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class TaskHostFactoryTests : SdkTest
    {
        public TaskHostFactoryTests(ITestOutputHelper log) : base(log) { }

        [Theory(Skip = "https://github.com/dotnet/sdk/issues/53787")]
        [InlineData("NET")]
        // dotnet.exe doesn't support launching .Net Framework nodes
#if NETFRAMEWORK
        [InlineData("CLR4")]
#endif
        public void TaskHostFactory_Communication_Works(string runtime)
        {
            var testProject = new TestProject
            {
                Name = "TaskHostFactoryRuntimeTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: runtime);
            var projectDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);

            var targetsContent = $@"
<Project>
  <UsingTask
    TaskName=""Microsoft.NET.Build.Tasks.NETSdkInformation""
    AssemblyFile=""$(MicrosoftNETBuildTasksAssembly)""
    TaskFactory=""TaskHostFactory""
    Runtime=""{runtime}"" />
  <Target Name=""TestTaskHostFactoryWithRuntime"" AfterTargets=""Build"">
    <Microsoft.NET.Build.Tasks.NETSdkInformation
      FormattedText=""TaskHostFactory with Runtime={runtime} executed successfully"" />
  </Target>
</Project>";

            File.WriteAllText(Path.Combine(projectDirectory, "TaskHostFactoryRuntimeTest.targets"), targetsContent);

            var projectPath = Path.Combine(projectDirectory, $"{testProject.Name}.csproj");
            var projectXml = System.Xml.Linq.XDocument.Load(projectPath);
            var ns = projectXml.Root!.Name.Namespace;
            var importElement = new System.Xml.Linq.XElement(ns + "Import",
                new System.Xml.Linq.XAttribute("Project", "TaskHostFactoryRuntimeTest.targets"));
            projectXml.Root!.Add(importElement);
            projectXml.Save(projectPath);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute("/t:TestTaskHostFactoryWithRuntime", "/v:n")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining($"TaskHostFactory with Runtime={runtime} executed successfully");
        }

        /// <summary>
        /// End-to-end regression for dotnet/msbuild#13695: when MSBuild routes a .NET Framework SDK task
        /// that implements <c>IMultiThreadableTask</c> to a CLR4 TaskHost (out-of-process net472 node),
        /// the task DLL must load without TypeLoadException for the new MSBuild types and execute correctly.
        /// Before this PR, the SDK shipped polyfilled <c>IMultiThreadableTask</c> / <c>TaskEnvironment</c>
        /// types that MSBuild did not recognize at runtime; after the PR the SDK references the real
        /// 18.6+ types from <c>Microsoft.Build.Framework</c>, so the TaskHost (which loads its own copy
        /// of <c>Microsoft.Build.Framework.dll</c>) must also have those types available.
        /// Only runs when full MSBuild is configured (the <c>DOTNET_SDK_TEST_MSBUILD_PATH</c> env var
        /// points at <c>MSBuild.exe</c>); only full MSBuild can spawn a CLR4 TaskHost. The
        /// <c>dotnet build</c> launcher cannot launch .NET Framework nodes.
        /// </summary>
        [FullMSBuildOnlyFact]
        public void IMultiThreadableSdkTask_LoadsAndExecutes_InCLR4TaskHost()
        {
            var testProject = new TestProject
            {
                Name = "MultiThreadableTaskInTaskHost",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var testAsset = TestAssetsManager.CreateTestProject(testProject);
            var projectDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);

            // Pick GenerateToolsSettingsFile because it is small, implements IMultiThreadableTask,
            // uses TaskEnvironment.GetAbsolutePath for its single output path, and produces a file
            // we can assert on.
            var settingsRelativePath = "out\\settings.xml";
            var settingsAbsolutePath = Path.Combine(projectDirectory, settingsRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(settingsAbsolutePath)!);

            var targetsContent = $@"
<Project>
  <UsingTask
    TaskName=""Microsoft.NET.Build.Tasks.GenerateToolsSettingsFile""
    AssemblyFile=""$(MicrosoftNETBuildTasksAssembly)""
    TaskFactory=""TaskHostFactory""
    Runtime=""CLR4"" />
  <Target Name=""ExerciseMultiThreadableTaskInTaskHost"" AfterTargets=""Build"">
    <Microsoft.NET.Build.Tasks.GenerateToolsSettingsFile
      EntryPointRelativePath=""tool.dll""
      CommandName=""mytool""
      ToolsSettingsFilePath=""{settingsRelativePath}"" />
  </Target>
</Project>";

            File.WriteAllText(Path.Combine(projectDirectory, $"{testProject.Name}.targets"), targetsContent);

            var projectPath = Path.Combine(projectDirectory, $"{testProject.Name}.csproj");
            var projectXml = System.Xml.Linq.XDocument.Load(projectPath);
            var ns = projectXml.Root!.Name.Namespace;
            projectXml.Root!.Add(new System.Xml.Linq.XElement(ns + "Import",
                new System.Xml.Linq.XAttribute("Project", $"{testProject.Name}.targets")));
            projectXml.Save(projectPath);

            new BuildCommand(testAsset)
                .Execute("/t:ExerciseMultiThreadableTaskInTaskHost", "/v:n")
                .Should()
                .Pass();

            File.Exists(settingsAbsolutePath).Should().BeTrue(
                "GenerateToolsSettingsFile should have written its output via the CLR4 TaskHost; " +
                "if the SDK task DLL failed to load (e.g. missing IMultiThreadableTask / TaskEnvironment types " +
                "in the TaskHost's Microsoft.Build.Framework), no file would be created");
        }
    }
}
