// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class TaskHostFactoryTests : SdkTest
    {
        public TaskHostFactoryTests(ITestOutputHelper log) : base(log) { }

        [Theory]
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
    }
}
