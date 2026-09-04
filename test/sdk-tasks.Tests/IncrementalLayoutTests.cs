// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

[TestClass]
public class IncrementalLayoutTests : SdkTest
{
    [TestMethod]
    public void PrepareIncrementalLayoutValidatesMappingsAndFindsStaleOutputs()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string source = CreateFile(root, "inputs", "template.nupkg", "original");
        string destination = Path.Combine(root, "layout", "template.nupkg");
        string staleOutput = CreateFile(root, "layout", "stale.nupkg", "stale");

        var prepare = new PrepareIncrementalLayout
        {
            SourceFiles = [new TaskItem(source)],
            DestinationFiles = [new TaskItem(destination)],
            ExistingOutputs = [new TaskItem(staleOutput)],
            BuildEngine = new MockBuildEngine()
        };
        prepare.Execute().Should().BeTrue();
        prepare.ExpectedOutputs.Select(item => item.ItemSpec).Should().Equal(destination);
        prepare.StaleOutputs.Select(item => item.ItemSpec).Should().Equal(staleOutput);

        if (OperatingSystem.IsWindows())
        {
            var caseOnlyExistingOutput = new PrepareIncrementalLayout
            {
                SourceFiles = [new TaskItem(source)],
                DestinationFiles = [new TaskItem(destination)],
                ExistingOutputs = [new TaskItem(destination.ToUpperInvariant())],
                BuildEngine = new MockBuildEngine()
            };
            caseOnlyExistingOutput.Execute().Should().BeTrue();
            caseOnlyExistingOutput.StaleOutputs.Should().BeEmpty();

            var caseOnlyDuplicateDestination = new PrepareIncrementalLayout
            {
                SourceFiles = [new TaskItem(source), new TaskItem(source)],
                DestinationFiles = [new TaskItem(destination), new TaskItem(destination.ToUpperInvariant())],
                BuildEngine = new MockBuildEngine()
            };
            caseOnlyDuplicateDestination.Execute().Should().BeFalse();
        }

        var mismatchedMappings = new PrepareIncrementalLayout
        {
            SourceFiles = [new TaskItem(source)],
            DestinationFiles = [],
            BuildEngine = new MockBuildEngine()
        };
        mismatchedMappings.Execute().Should().BeFalse();

        string secondSource = CreateFile(root, "inputs", "second.nupkg", "second");
        var overlappingMappings = new PrepareIncrementalLayout
        {
            SourceFiles = [new TaskItem(source), new TaskItem(secondSource)],
            DestinationFiles = [new TaskItem(secondSource), new TaskItem(source)],
            BuildEngine = new MockBuildEngine()
        };
        overlappingMappings.Execute().Should().BeFalse();
    }

    [TestMethod]
    public void CompleteIncrementalLayoutWritesCompletionLast()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string output = Path.Combine(root, "layout", "template.nupkg");
        string completion = CreateFile(root, "state", "layout.complete", "stale");
        var complete = new CompleteIncrementalLayout
        {
            ExpectedOutputs = [new TaskItem(output)],
            CompletionStampFile = completion,
            BuildEngine = new MockBuildEngine()
        };

        complete.Execute().Should().BeFalse();
        File.Exists(completion).Should().BeFalse();

        CreateFile(root, "layout", "template.nupkg", "content");
        complete = new CompleteIncrementalLayout
        {
            ExpectedOutputs = [new TaskItem(output)],
            CompletionStampFile = completion,
            BuildEngine = new MockBuildEngine()
        };
        complete.Execute().Should().BeTrue();
        File.ReadAllText(completion).Should().Be($"complete{Environment.NewLine}");
    }

    [TestMethod]
    public void IncrementalLayoutTasksRejectRelativePaths()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string source = CreateFile(root, "inputs", "template.nupkg", "content");
        string output = CreateFile(root, "layout", "template.nupkg", "content");
        string completion = Path.Combine(root, "state", "layout.complete");
        var relativePath = new TaskItem(Path.Combine("relative", "template.nupkg"));

        new PrepareIncrementalLayout
        {
            SourceFiles = [relativePath],
            DestinationFiles = [new TaskItem(output)],
            BuildEngine = new MockBuildEngine()
        }.Execute().Should().BeFalse();

        new PrepareIncrementalLayout
        {
            SourceFiles = [new TaskItem(source)],
            DestinationFiles = [relativePath],
            BuildEngine = new MockBuildEngine()
        }.Execute().Should().BeFalse();

        new PrepareIncrementalLayout
        {
            SourceFiles = [new TaskItem(source)],
            DestinationFiles = [new TaskItem(output)],
            ExistingOutputs = [relativePath],
            BuildEngine = new MockBuildEngine()
        }.Execute().Should().BeFalse();

        new CompleteIncrementalLayout
        {
            ExpectedOutputs = [relativePath],
            CompletionStampFile = completion,
            BuildEngine = new MockBuildEngine()
        }.Execute().Should().BeFalse();

        new CompleteIncrementalLayout
        {
            ExpectedOutputs = [new TaskItem(output)],
            CompletionStampFile = relativePath.ItemSpec,
            BuildEngine = new MockBuildEngine()
        }.Execute().Should().BeFalse();
    }

    [TestMethod]
    public void LayoutTargetSkipsWarmBuildAndTracksInputChanges()
    {
        LayoutProject project = CreateLayoutProject();
        string source = CreateFile(project.Root, "inputs", "template.nupkg", "original");

        string firstInvocation = project.Build();
        DateTime completionTimestamp = File.GetLastWriteTimeUtc(project.CompletionFile);
        DateTime unchangedTimestamp = File.GetLastWriteTimeUtc(source);
        File.SetLastWriteTimeUtc(project.Output("template.nupkg"), unchangedTimestamp);
        File.ReadAllText(project.Output("template.nupkg")).Should().Be("original");

        project.Build().Should().Be(firstInvocation);
        File.GetLastWriteTimeUtc(project.CompletionFile).Should().Be(completionTimestamp);

        File.WriteAllText(source, "modified");
        SetLastWriteTimeAfter(source, completionTimestamp);
        string changedInvocation = project.Build();
        changedInvocation.Should().NotBe(firstInvocation);
        File.ReadAllText(project.Output("template.nupkg")).Should().Be("modified");

        File.WriteAllText(source, "original");
        SetLastWriteTimeAfter(source, File.GetLastWriteTimeUtc(project.CompletionFile));
        project.Build().Should().NotBe(changedInvocation);
        File.ReadAllText(project.Output("template.nupkg")).Should().Be("original");
    }

    [TestMethod]
    public void LayoutTargetTracksAddedRemovedRenamedAndMissingOutputs()
    {
        LayoutProject project = CreateLayoutProject();
        string firstSource = CreateFile(project.Root, "inputs", "first.nupkg", "first");
        project.Build();

        string secondSource = CreateFile(project.Root, "inputs", "second.nupkg", "second");
        string addedInvocation = project.Build();
        File.Exists(project.Output("second.nupkg")).Should().BeTrue();

        File.Delete(firstSource);
        string removedInvocation = project.Build();
        removedInvocation.Should().NotBe(addedInvocation);
        File.Exists(project.Output("first.nupkg")).Should().BeFalse();

        string renamedSource = Path.Combine(Path.GetDirectoryName(secondSource)!, "renamed.nupkg");
        File.Move(secondSource, renamedSource);
        string renamedInvocation = project.Build();
        renamedInvocation.Should().NotBe(removedInvocation);
        File.Exists(project.Output("second.nupkg")).Should().BeFalse();
        File.ReadAllText(project.Output("renamed.nupkg")).Should().Be("second");

        File.Delete(project.Output("renamed.nupkg"));
        project.Build().Should().NotBe(renamedInvocation);
        File.ReadAllText(project.Output("renamed.nupkg")).Should().Be("second");
    }

    [TestMethod]
    public void LayoutTargetTracksDestinationRemaps()
    {
        LayoutProject project = CreateLayoutProject();
        CreateFile(project.Root, "inputs", "template.nupkg", "content");

        string initialInvocation = project.Build();
        File.Exists(project.Output("template.nupkg")).Should().BeTrue();

        string remappedInvocation = project.BuildWithDestinationSubdirectory("remapped");
        remappedInvocation.Should().NotBe(initialInvocation);
        File.Exists(project.Output("template.nupkg")).Should().BeFalse();
        File.ReadAllText(project.Output("template.nupkg", "remapped")).Should().Be("content");

        if (OperatingSystem.IsWindows())
        {
            DateTime completionTimestamp = File.GetLastWriteTimeUtc(project.CompletionFile);
            project.BuildWithDestinationSubdirectory("REMAPPED").Should().Be(remappedInvocation);
            File.GetLastWriteTimeUtc(project.CompletionFile).Should().Be(completionTimestamp);
        }
    }

    [TestMethod]
    public void LayoutTargetRecoversStateAndPreservesUnownedFiles()
    {
        LayoutProject project = CreateLayoutProject();
        CreateFile(project.Root, "inputs", "template.nupkg", "content");
        string initialInvocation = project.Build();
        string expectedInputManifest = File.ReadAllText(project.InputManifestFile);
        string expectedOutputInventory = File.ReadAllText(project.InventoryFile);

        string staleOutput = CreateFile(project.Root, "layout", "stale.nupkg", "stale");
        string unownedOutput = CreateFile(project.Root, "layout", "keep.txt", "unowned");
        string staleInvocation = project.Build();
        staleInvocation.Should().NotBe(initialInvocation);
        File.Exists(staleOutput).Should().BeFalse();
        File.ReadAllText(unownedOutput).Should().Be("unowned");

        foreach (string stateFile in project.StateFiles)
        {
            DateTime completionTimestamp = File.GetLastWriteTimeUtc(project.CompletionFile);
            File.WriteAllText(stateFile, Path.GetFullPath(unownedOutput));
            if (stateFile != project.CompletionFile)
            {
                SetLastWriteTimeAfter(stateFile, completionTimestamp);
            }

            string recoveredInvocation = project.Build();
            recoveredInvocation.Should().NotBe(staleInvocation);
            staleInvocation = recoveredInvocation;
            File.ReadAllText(project.InputManifestFile).Should().Be(expectedInputManifest);
            File.ReadAllText(project.InventoryFile).Should().Be(expectedOutputInventory);
            File.ReadAllText(unownedOutput).Should().Be("unowned");

            DateTime inputManifestTimestamp = File.GetLastWriteTimeUtc(project.InputManifestFile);
            DateTime outputInventoryTimestamp = File.GetLastWriteTimeUtc(project.InventoryFile);
            DateTime completionTimestampAfterRecovery = File.GetLastWriteTimeUtc(project.CompletionFile);
            project.Build().Should().Be(recoveredInvocation);
            File.GetLastWriteTimeUtc(project.InputManifestFile).Should().Be(inputManifestTimestamp);
            File.GetLastWriteTimeUtc(project.InventoryFile).Should().Be(outputInventoryTimestamp);
            File.GetLastWriteTimeUtc(project.CompletionFile).Should().Be(completionTimestampAfterRecovery);
        }

        foreach (string stateFile in project.StateFiles)
        {
            File.Delete(stateFile);
            string recoveredInvocation = project.Build();
            recoveredInvocation.Should().NotBe(staleInvocation);
            staleInvocation = recoveredInvocation;
            File.ReadAllText(project.InputManifestFile).Should().Be(expectedInputManifest);
            File.ReadAllText(project.InventoryFile).Should().Be(expectedOutputInventory);
            File.ReadAllText(unownedOutput).Should().Be("unowned");

            DateTime inputManifestTimestamp = File.GetLastWriteTimeUtc(project.InputManifestFile);
            DateTime outputInventoryTimestamp = File.GetLastWriteTimeUtc(project.InventoryFile);
            DateTime completionTimestamp = File.GetLastWriteTimeUtc(project.CompletionFile);
            project.Build().Should().Be(recoveredInvocation);
            File.GetLastWriteTimeUtc(project.InputManifestFile).Should().Be(inputManifestTimestamp);
            File.GetLastWriteTimeUtc(project.InventoryFile).Should().Be(outputInventoryTimestamp);
            File.GetLastWriteTimeUtc(project.CompletionFile).Should().Be(completionTimestamp);
        }

        File.Delete(project.CompletionFile);
        project.Build().Should().NotBe(staleInvocation);
        File.ReadAllText(unownedOutput).Should().Be("unowned");
    }

    [TestMethod]
    public void LayoutTargetRecoversFromInterruptedUpdates()
    {
        LayoutProject project = CreateLayoutProject();
        string source = CreateFile(project.Root, "inputs", "template.nupkg", "original");
        project.Build();

        string staleOutput = CreateFile(project.Root, "layout", "stale.nupkg", "stale");
        File.WriteAllText(source, "changed-before-copy");
        project.BuildShouldFail("AfterDelete");

        File.Exists(staleOutput).Should().BeFalse();
        File.ReadAllText(project.Output("template.nupkg")).Should().Be("original");
        File.Exists(project.CompletionFile).Should().BeFalse();

        project.Build();
        File.ReadAllText(project.Output("template.nupkg")).Should().Be("changed-before-copy");

        File.WriteAllText(source, "changed-before-completion");
        project.BuildShouldFail("AfterCopy");

        File.ReadAllText(project.Output("template.nupkg")).Should().Be("changed-before-completion");
        File.Exists(project.CompletionFile).Should().BeFalse();

        project.Build();
        File.Exists(project.CompletionFile).Should().BeTrue();
        File.ReadAllText(project.Output("template.nupkg")).Should().Be("changed-before-completion");
    }

    [TestMethod]
    public void TemplateLayoutTargetsOwnOnlyTemplatePackages()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string bundledTemplate = CreateFile(root, "bundled-inputs", "bundled.nupkg", "bundled");
        string removedBundledTemplate = CreateFile(root, "bundled-inputs", "removed.nupkg", "removed");
        string repoTemplate = CreateFile(root, "repo-inputs", "Repo.Template.nupkg", "repo");
        string targetsPath = GetBundledTemplatesTargetsPath();
        string projectPath = Path.Combine(root, "template-layout.proj");
        string taskAssembly = typeof(PrepareIncrementalLayout).Assembly.Location;
        string msbuildTasksAssembly = GetMSBuildTasksAssemblyPath();

        File.WriteAllText(
            projectPath,
            $$"""
                <Project>
                  <UsingTask TaskName="Microsoft.DotNet.Build.Tasks.PrepareIncrementalLayout" AssemblyFile="{{Escape(taskAssembly)}}" />
                  <UsingTask TaskName="Microsoft.DotNet.Build.Tasks.CompleteIncrementalLayout" AssemblyFile="{{Escape(taskAssembly)}}" />
                  <UsingTask TaskName="Microsoft.Build.Tasks.Copy" AssemblyFile="{{Escape(msbuildTasksAssembly)}}" />
                  <UsingTask TaskName="Microsoft.Build.Tasks.Delete" AssemblyFile="{{Escape(msbuildTasksAssembly)}}" />
                  <UsingTask TaskName="Microsoft.Build.Tasks.ReadLinesFromFile" AssemblyFile="{{Escape(msbuildTasksAssembly)}}" />
                  <UsingTask TaskName="Microsoft.Build.Tasks.WriteLinesToFile" AssemblyFile="{{Escape(msbuildTasksAssembly)}}" />

                  <PropertyGroup>
                    <MajorMinorVersion>11.0</MajorMinorVersion>
                    <ProductMonikerRid>win-x64</ProductMonikerRid>
                    <BundledInputRoot>{{Escape(Path.GetDirectoryName(bundledTemplate)!)}}</BundledInputRoot>
                    <IntermediateOutputPath>{{Escape(Path.Combine(root, "obj"))}}/</IntermediateOutputPath>
                    <RedistInstallerLayoutPath>{{Escape(Path.Combine(root, "sdk"))}}/</RedistInstallerLayoutPath>
                  </PropertyGroup>
                  <ItemGroup>
                    <BundledTemplate Include="Test.Template">
                      <TemplateFrameworkVersion>11.0</TemplateFrameworkVersion>
                    </BundledTemplate>
                  </ItemGroup>

                  <Import Project="{{Escape(targetsPath)}}" />

                  <Target Name="CalculateTemplatesVersions">
                    <ItemGroup>
                      <BundledTemplatesWithInstallPaths Include="$(BundledInputRoot)/*.nupkg">
                        <BundledTemplateInstallPath>11.0</BundledTemplateInstallPath>
                        <TemplateFrameworkVersion>11.0</TemplateFrameworkVersion>
                      </BundledTemplatesWithInstallPaths>
                      <BundledTemplatesWithInstallPaths Update="@(BundledTemplatesWithInstallPaths)">
                        <RestoredNupkgPath>%(FullPath)</RestoredNupkgPath>
                      </BundledTemplatesWithInstallPaths>
                      <TemplatesComponents Include="Test.Templates">
                        <TemplatesMajorMinorVersion>11.0</TemplatesMajorMinorVersion>
                      </TemplatesComponents>
                    </ItemGroup>
                  </Target>
                  <Target Name="GetRepoTemplates">
                    <ItemGroup>
                      <RepoTemplate Include="{{Escape(repoTemplate)}}" />
                    </ItemGroup>
                  </Target>
                </Project>
                """);

        BuildProject(projectPath, "LayoutTemplates");

        string sdkTemplates = Path.Combine(root, "sdk", "templates", "11.0");
        string msiTemplates = Path.Combine(root, "obj", "templates-11.0", "templates", "11.0");
        File.ReadAllText(Path.Combine(sdkTemplates, "bundled.nupkg")).Should().Be("bundled");
        File.ReadAllText(Path.Combine(sdkTemplates, "repo.template.nupkg")).Should().Be("repo");
        File.ReadAllText(Path.Combine(msiTemplates, "bundled.nupkg")).Should().Be("bundled");
        File.ReadAllText(Path.Combine(msiTemplates, "repo.template.nupkg")).Should().Be("repo");
        File.ReadAllText(Path.Combine(sdkTemplates, "removed.nupkg")).Should().Be("removed");
        File.ReadAllText(Path.Combine(msiTemplates, "removed.nupkg")).Should().Be("removed");

        string sdkCompletionStamp = Path.Combine(root, "obj", "incremental-layout", "templates-sdk-win-x64.complete");
        string msiCompletionStamp = Path.Combine(root, "obj", "incremental-layout", "templates-msi-win-x64.complete");
        DateTime sdkCompletionTimestamp = File.GetLastWriteTimeUtc(sdkCompletionStamp);
        DateTime msiCompletionTimestamp = File.GetLastWriteTimeUtc(msiCompletionStamp);

        BuildProject(projectPath, "LayoutTemplates");

        File.GetLastWriteTimeUtc(sdkCompletionStamp).Should().Be(sdkCompletionTimestamp);
        File.GetLastWriteTimeUtc(msiCompletionStamp).Should().Be(msiCompletionTimestamp);

        File.Delete(removedBundledTemplate);
        BuildProject(projectPath, "LayoutTemplates");

        File.Exists(Path.Combine(sdkTemplates, "removed.nupkg")).Should().BeFalse();
        File.Exists(Path.Combine(msiTemplates, "removed.nupkg")).Should().BeFalse();

        string staleSdkPackage = CreateFile(root, Path.Combine("sdk", "templates"), "stale.nupkg", "stale");
        string staleMsiPackage = CreateFile(root, Path.Combine("obj", "templates-10.0", "templates"), "stale.nupkg", "stale");
        string unownedSdkFile = CreateFile(root, Path.Combine("sdk", "templates"), "keep.txt", "sdk");
        string unownedMsiFile = CreateFile(root, Path.Combine("obj", "templates-10.0", "templates"), "keep.txt", "msi");
        string unownedMsiPackage = CreateFile(root, Path.Combine("obj", "templates-backup", "templates"), "keep.nupkg", "msi package");
        string differentlyCasedStaleMsiPackage = CreateFile(root, Path.Combine("obj", "TEMPLATES-9.0", "TEMPLATES"), "stale.nupkg", "stale");

        BuildProject(projectPath, "LayoutTemplates");

        File.Exists(staleSdkPackage).Should().BeFalse();
        File.Exists(staleMsiPackage).Should().BeFalse();
        File.ReadAllText(unownedSdkFile).Should().Be("sdk");
        File.ReadAllText(unownedMsiFile).Should().Be("msi");
        File.ReadAllText(unownedMsiPackage).Should().Be("msi package");
        File.Exists(differentlyCasedStaleMsiPackage).Should().Be(!OperatingSystem.IsWindows());
    }

    [TestMethod]
    public void GenerateTemplatesMsisTracksStagedLayoutAndCompletion()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string repoRoot = Path.Combine(root, "repo");
        string stagedTemplate = CreateFile(
            root,
            Path.Combine("obj", "templates-11.0", "templates", "11.0"),
            "template.nupkg",
            "content");
        string secondStagedTemplate = CreateFile(
            root,
            Path.Combine("obj", "templates-10.0", "templates", "10.0"),
            "template.nupkg",
            "second");
        string completionStamp = CreateFile(root, Path.Combine("obj", "incremental-layout"), "templates-msi-win-x64.complete", "complete");
        string templateMsi = Path.Combine(root, "templates.msi");
        string secondTemplateMsi = Path.Combine(root, "templates-10.msi");
        string templatesProject = Path.Combine(repoRoot, "src", "Layout", "pkg", "windows", "msis", "templates", "templates.wixproj");
        string windowsDirectoryBuildProps = Path.Combine(repoRoot, "src", "Layout", "pkg", "windows", "Directory.Build.props");
        string sharedAuthoringInput = CreateFile(
            repoRoot,
            Path.Combine("src", "Layout", "pkg", "windows"),
            "StableFileIdForApphostTransform.xslt",
            "original");
        string msbuildTasksAssembly = GetMSBuildTasksAssemblyPath();
        Directory.CreateDirectory(Path.GetDirectoryName(templatesProject)!);
        File.WriteAllText(
            windowsDirectoryBuildProps,
            $$"""
            <Project>
              <PropertyGroup>
                <TemplateMsiAuthoringInput>{{Escape(windowsDirectoryBuildProps)}};{{Escape(sharedAuthoringInput)}}</TemplateMsiAuthoringInput>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(
            templatesProject,
            $$"""
            <Project DefaultTargets="Build">
              <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
              <UsingTask TaskName="Microsoft.Build.Tasks.Error" AssemblyFile="{{Escape(msbuildTasksAssembly)}}" />
              <UsingTask TaskName="Microsoft.Build.Tasks.Delete" AssemblyFile="{{Escape(msbuildTasksAssembly)}}" />
              <UsingTask TaskName="Microsoft.Build.Tasks.MakeDir" AssemblyFile="{{Escape(msbuildTasksAssembly)}}" />
              <UsingTask TaskName="Microsoft.Build.Tasks.WriteLinesToFile" AssemblyFile="{{Escape(msbuildTasksAssembly)}}" />
              <ItemGroup>
                <TemplateLayoutFile Include="$(TemplateLayoutDirectoryToHarvest)\**\*" />
              </ItemGroup>
              <PropertyGroup>
                <BuildPropertiesFile>$(IntermediateOutputPath)build.properties</BuildPropertiesFile>
              </PropertyGroup>
              <Target Name="PrepareBuildProperties">
                <MakeDir Directories="$(IntermediateOutputPath)" />
                <WriteLinesToFile File="$(BuildPropertiesFile)"
                                  Lines="ProductVersion=$(ProductVersion);BundleVersion=$(BundleVersion);Version=$(Version);BrandName=$(BrandName);UpgradeCode=$(UpgradeCode)"
                                  Overwrite="true"
                                  WriteOnlyWhenDifferent="true" />
              </Target>
              <Target Name="Build"
                      DependsOnTargets="PrepareBuildProperties"
                      Inputs="@(TemplateLayoutFile);$(TemplateLayoutCompletionStamp);$(BuildPropertiesFile);$(TemplateMsiAuthoringInput)"
                      Outputs="$(InstallerPath)">
                <MakeDir Directories="$([System.IO.Path]::GetDirectoryName('$(InstallerPath)'))" />
                <WriteLinesToFile File="$(IntermediateOutputPath)build.marker"
                                  Lines="$(TemplateLayoutDirectoryToHarvest)"
                                  Overwrite="true" />
                <WriteLinesToFile File="$(InstallerPath)"
                                  Lines="$([System.Guid]::NewGuid())"
                                  Overwrite="true" />
                <Error Condition="'$(FailAfterWritingMsi)' == 'true'"
                       Text="Simulated failure after writing the MSI." />
              </Target>
              <Target Name="Clean">
                <Delete Files="$(InstallerPath)" />
              </Target>
              <Target Name="Rebuild" DependsOnTargets="Clean;Build" />
            </Project>
            """);

        string targetsPath = GetLayoutTargetsPath("GenerateMSIs.targets");
        string projectPath = Path.Combine(root, "generate-msis.proj");

        File.WriteAllText(
            projectPath,
            $$"""
            <Project>
              <UsingTask TaskName="Microsoft.Build.Tasks.Error" AssemblyFile="{{Escape(msbuildTasksAssembly)}}" />
              <UsingTask TaskName="Microsoft.Build.Tasks.Delete" AssemblyFile="{{Escape(msbuildTasksAssembly)}}" />
              <UsingTask TaskName="Microsoft.Build.Tasks.MSBuild" AssemblyFile="{{Escape(msbuildTasksAssembly)}}" />
              <UsingTask TaskName="Microsoft.Build.Tasks.WriteLinesToFile" AssemblyFile="{{Escape(msbuildTasksAssembly)}}" />

              <PropertyGroup>
                <RepoRoot>{{Escape(repoRoot)}}/</RepoRoot>
                <IntermediateOutputPath>{{Escape(Path.Combine(root, "obj"))}}/</IntermediateOutputPath>
                <_TemplatesMsiLayoutCompletionStamp>{{Escape(completionStamp)}}</_TemplatesMsiLayoutCompletionStamp>
                <ProductMonikerRid>win-x64</ProductMonikerRid>
                <ProductVersion>11.0.0</ProductVersion>
                <BundleVersion>11.0.0.0</BundleVersion>
                <Version>11.0.0</Version>
                <CliProductBandVersion>11.0.</CliProductBandVersion>
              </PropertyGroup>
              <ItemGroup>
                <TemplatesComponents Include="Test.Templates">
                  <TemplatesMajorMinorVersion>11.0</TemplatesMajorMinorVersion>
                  <MSIInstallerFile>{{Escape(templateMsi)}}</MSIInstallerFile>
                  <BrandNameWithoutVersion>Test Templates</BrandNameWithoutVersion>
                  <InstallerUpgradeCode>upgrade-code</InstallerUpgradeCode>
                </TemplatesComponents>
                <TemplatesComponents Include="Test.Templates.10">
                  <TemplatesMajorMinorVersion>10.0</TemplatesMajorMinorVersion>
                  <MSIInstallerFile>{{Escape(secondTemplateMsi)}}</MSIInstallerFile>
                  <BrandNameWithoutVersion>Test Templates 10</BrandNameWithoutVersion>
                  <InstallerUpgradeCode>upgrade-code-10</InstallerUpgradeCode>
                </TemplatesComponents>
              </ItemGroup>

              <Import Project="{{Escape(targetsPath)}}" />

              <Target Name="GenerateInstallerLayout" />
              <Target Name="MsiTargetsSetupInputOutputs" />
              <Target Name="CalculateTemplatesVersions" />
            </Project>
            """);

        using var collection = new ProjectCollection();
        Project project = collection.LoadProject(projectPath);
        ProjectInstance instance = project.CreateProjectInstance();
        instance.Targets["GenerateTemplatesMsis"].Inputs.Should().BeEmpty();
        instance.Targets["GenerateTemplatesMsis"].Outputs.Should().BeEmpty();
        File.Exists(stagedTemplate).Should().BeTrue();
        File.Exists(secondStagedTemplate).Should().BeTrue();

        ProjectRootElement templatesWixProject = ProjectRootElement.Open(GetTemplatesWixProjectPath());
        ProjectTargetElement incrementalInputsTarget = templatesWixProject.Targets
            .Single(target => target.Name == "IncludeTemplateMsiIncrementalInputs");
        incrementalInputsTarget.AfterTargets.Should().Be("GetHarvestDirectoryContent");
        string[] nestedIncrementalInputs = incrementalInputsTarget.Children
            .OfType<ProjectItemGroupElement>()
            .SelectMany(group => group.Items)
            .Select(item => item.Include)
            .ToArray();
        nestedIncrementalInputs.Should().Equal("$(TemplateLayoutCompletionStamp)");
        templatesWixProject.Targets.Should().NotContain(target => target.Name == "GetTemplateMsiIncrementalInputs");
        ProjectTargetElement buildPropertiesTarget = templatesWixProject.Targets
            .Single(target => target.Name == "IncludeTemplateMsiBuildProperties");
        buildPropertiesTarget.BeforeTargets.Should().Be("CoreCompile");
        buildPropertiesTarget.Children
            .OfType<ProjectItemGroupElement>()
            .SelectMany(group => group.Items)
            .Should()
            .ContainSingle(item => item.ItemType == "_BindInputs"
                && item.Include == "$(IntermediateOutputPath)template-msi-build.properties");

        BuildProject(projectPath, "GenerateTemplatesMsis");
        string firstMsiContents = File.ReadAllText(templateMsi);
        File.Exists(secondTemplateMsi).Should().BeTrue();
        string firstIntermediateMarker = Path.Combine(root, "obj", "templates-msi-build", "win-x64", "11.0", "build.marker");
        string secondIntermediateMarker = Path.Combine(root, "obj", "templates-msi-build", "win-x64", "10.0", "build.marker");
        File.ReadAllText(firstIntermediateMarker).Should().Contain("templates-11.0");
        File.ReadAllText(secondIntermediateMarker).Should().Contain("templates-10.0");
        string firstBuildPropertiesFile = Path.Combine(root, "obj", "templates-msi-build", "win-x64", "11.0", "build.properties");
        string generationInProgressFile = Path.Combine(root, "obj", "incremental-layout", "templates-msi-generation-win-x64.inprogress");
        DateTime buildPropertiesTimestamp = File.GetLastWriteTimeUtc(firstBuildPropertiesFile);
        File.ReadAllText(firstBuildPropertiesFile).Should().Contain("ProductVersion=11.0.0");

        BuildProject(projectPath, "GenerateTemplatesMsis");
        File.ReadAllText(templateMsi).Should().Be(firstMsiContents);
        File.GetLastWriteTimeUtc(firstBuildPropertiesFile).Should().Be(buildPropertiesTimestamp);
        File.Exists(generationInProgressFile).Should().BeFalse();

        File.AppendAllText(windowsDirectoryBuildProps, $"{Environment.NewLine}<!-- changed -->");
        SetLastWriteTimeAfter(windowsDirectoryBuildProps, File.GetLastWriteTimeUtc(templateMsi));
        BuildProject(projectPath, "GenerateTemplatesMsis");
        string authoringChangedMsiContents = File.ReadAllText(templateMsi);
        authoringChangedMsiContents.Should().NotBe(firstMsiContents);

        BuildProject(projectPath, "GenerateTemplatesMsis");
        File.ReadAllText(templateMsi).Should().Be(authoringChangedMsiContents);

        File.WriteAllText(sharedAuthoringInput, "modified");
        SetLastWriteTimeAfter(sharedAuthoringInput, File.GetLastWriteTimeUtc(templateMsi));
        BuildProject(projectPath, "GenerateTemplatesMsis");
        string sharedAuthoringChangedMsiContents = File.ReadAllText(templateMsi);
        sharedAuthoringChangedMsiContents.Should().NotBe(authoringChangedMsiContents);

        BuildProject(projectPath, "GenerateTemplatesMsis");
        File.ReadAllText(templateMsi).Should().Be(sharedAuthoringChangedMsiContents);

        BuildProject(
            projectPath,
            "GenerateTemplatesMsis",
            new Dictionary<string, string> { ["ProductVersion"] = "11.0.1" });
        string productVersionChangedMsiContents = File.ReadAllText(templateMsi);
        productVersionChangedMsiContents.Should().NotBe(sharedAuthoringChangedMsiContents);
        DateTime productVersionChangedPropertiesTimestamp = File.GetLastWriteTimeUtc(firstBuildPropertiesFile);
        productVersionChangedPropertiesTimestamp.Should().BeAfter(buildPropertiesTimestamp);

        BuildProject(
            projectPath,
            "GenerateTemplatesMsis",
            new Dictionary<string, string> { ["ProductVersion"] = "11.0.1" });
        File.ReadAllText(templateMsi).Should().Be(productVersionChangedMsiContents);
        File.GetLastWriteTimeUtc(firstBuildPropertiesFile).Should().Be(productVersionChangedPropertiesTimestamp);

        BuildProject(projectPath, "GenerateTemplatesMsis");
        string recoveredMsiContents = File.ReadAllText(templateMsi);
        recoveredMsiContents.Should().NotBe(productVersionChangedMsiContents);
        File.ReadAllText(firstBuildPropertiesFile).Should().Contain("ProductVersion=11.0.0");

        File.WriteAllText(stagedTemplate, "changed");
        SetLastWriteTimeAfter(stagedTemplate, File.GetLastWriteTimeUtc(templateMsi));
        BuildProject(projectPath, "GenerateTemplatesMsis");
        string changedMsiContents = File.ReadAllText(templateMsi);
        changedMsiContents.Should().NotBe(recoveredMsiContents);

        File.WriteAllText(completionStamp, "changed");
        SetLastWriteTimeAfter(completionStamp, File.GetLastWriteTimeUtc(templateMsi));
        BuildProject(projectPath, "GenerateTemplatesMsis");
        string completionChangedMsiContents = File.ReadAllText(templateMsi);
        completionChangedMsiContents.Should().NotBe(changedMsiContents);

        File.WriteAllText(stagedTemplate, "content");
        File.SetLastWriteTimeUtc(stagedTemplate, DateTime.UnixEpoch);
        File.WriteAllText(completionStamp, "reverted");
        SetLastWriteTimeAfter(completionStamp, File.GetLastWriteTimeUtc(templateMsi));
        BuildProject(projectPath, "GenerateTemplatesMsis");
        File.ReadAllText(templateMsi).Should().NotBe(completionChangedMsiContents);

        File.Delete(templateMsi);
        BuildProject(projectPath, "GenerateTemplatesMsis");
        File.Exists(templateMsi).Should().BeTrue();

        File.WriteAllText(templateMsi, "stale");
        File.SetLastWriteTimeUtc(templateMsi, DateTime.UnixEpoch);
        BuildProject(projectPath, "GenerateTemplatesMsis");
        File.ReadAllText(templateMsi).Should().NotBe("stale");

        File.SetLastWriteTimeUtc(templateMsi, DateTime.UnixEpoch);
        BuildProjectShouldFail(
            projectPath,
            "GenerateTemplatesMsis",
            new Dictionary<string, string> { ["FailAfterWritingMsi"] = "true" });
        File.Exists(generationInProgressFile).Should().BeTrue();
        string failedFirstMsiContents = File.ReadAllText(templateMsi);
        string failedSecondMsiContents = File.ReadAllText(secondTemplateMsi);

        BuildProject(projectPath, "GenerateTemplatesMsis");
        File.Exists(generationInProgressFile).Should().BeFalse();
        File.ReadAllText(templateMsi).Should().NotBe(failedFirstMsiContents);
        File.ReadAllText(secondTemplateMsi).Should().NotBe(failedSecondMsiContents);
    }

    private LayoutProject CreateLayoutProject()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        Directory.CreateDirectory(Path.Combine(root, "inputs"));
        Directory.CreateDirectory(Path.Combine(root, "layout"));
        Directory.CreateDirectory(Path.Combine(root, "state"));

        return new LayoutProject(root, typeof(PrepareIncrementalLayout).Assembly.Location);
    }

    private static string CreateFile(
        string root,
        string relativeDirectory,
        string fileName,
        string contents)
    {
        string directory = Path.Combine(root, relativeDirectory);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    private static void BuildProject(
        string projectPath,
        string target,
        Dictionary<string, string>? globalProperties = null)
    {
        using var collection = new ProjectCollection(globalProperties ?? []);
        Project project = collection.LoadProject(projectPath);
        project.Build(target).Should().BeTrue();
    }

    private static void BuildProjectShouldFail(
        string projectPath,
        string target,
        Dictionary<string, string>? globalProperties = null)
    {
        using var collection = new ProjectCollection(globalProperties ?? []);
        Project project = collection.LoadProject(projectPath);
        project.Build(target).Should().BeFalse();
    }

    private static void SetLastWriteTimeAfter(string path, DateTime timestamp)
    {
        while (File.GetLastWriteTimeUtc(path) <= timestamp)
        {
            WaitForUtcNowToAdvance();
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
    }

    private static string GetBundledTemplatesTargetsPath() =>
        GetLayoutTargetsPath("BundledTemplates.targets");

    private static string GetTemplatesWixProjectPath() =>
        GetLayoutFilePath(
            "templates.wixproj",
            Path.Combine("src", "Layout", "pkg", "windows", "msis", "templates", "templates.wixproj"));

    private static string GetLayoutTargetsPath(string fileName) =>
        GetLayoutFilePath(
            fileName,
            Path.Combine("src", "Layout", "redist", "targets", fileName));

    private static string GetLayoutFilePath(string deployedFileName, string repositoryRelativePath)
    {
        string deployedPath = Path.Combine(
            SdkTestContext.Current.TestExecutionDirectory,
            "Layout",
            deployedFileName);
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_EXECUTION_DIRECTORY"))
            && File.Exists(deployedPath))
        {
            return deployedPath;
        }

        string? repoRoot = SdkTestContext.Current.ToolsetUnderTest.RepoRoot
            ?? SdkTestContext.GetRepoRoot();
        if (repoRoot is not null)
        {
            string repoPath = Path.Combine(repoRoot, repositoryRelativePath);
            if (File.Exists(repoPath))
            {
                return repoPath;
            }
        }

        if (File.Exists(deployedPath))
        {
            return deployedPath;
        }

        throw new InvalidOperationException($"Could not find {deployedFileName}.");
    }

    private static string Escape(string value) => SecurityElement.Escape(value)!;

    private static string GetMSBuildTasksAssemblyPath() =>
        Path.Combine(Path.GetDirectoryName(typeof(Project).Assembly.Location)!, "Microsoft.Build.Tasks.Core.dll");

    private sealed class LayoutProject
    {
        private readonly string _projectFile;

        public LayoutProject(string root, string taskAssembly)
        {
            Root = root;
            string sourceRoot = Path.Combine(root, "inputs");
            string outputRoot = Path.Combine(root, "layout");
            string stateRoot = Path.Combine(root, "state");
            _projectFile = Path.Combine(root, "layout.proj");
            InputManifestFile = Path.Combine(stateRoot, "layout.inputs");
            InventoryFile = Path.Combine(stateRoot, "layout.outputs");
            CompletionFile = Path.Combine(stateRoot, "layout.complete");
            InvocationFile = Path.Combine(stateRoot, "invocation");

            File.WriteAllText(
                _projectFile,
                $$"""
                <Project>
                  <UsingTask TaskName="Microsoft.DotNet.Build.Tasks.PrepareIncrementalLayout" AssemblyFile="{{Escape(taskAssembly)}}" />
                  <UsingTask TaskName="Microsoft.DotNet.Build.Tasks.CompleteIncrementalLayout" AssemblyFile="{{Escape(taskAssembly)}}" />
                  <UsingTask TaskName="Microsoft.Build.Tasks.Copy" AssemblyFile="{{Escape(GetMSBuildTasksAssemblyPath())}}" />
                  <UsingTask TaskName="Microsoft.Build.Tasks.Delete" AssemblyFile="{{Escape(GetMSBuildTasksAssemblyPath())}}" />
                  <UsingTask TaskName="Microsoft.Build.Tasks.Error" AssemblyFile="{{Escape(GetMSBuildTasksAssemblyPath())}}" />
                  <UsingTask TaskName="Microsoft.Build.Tasks.ReadLinesFromFile" AssemblyFile="{{Escape(GetMSBuildTasksAssemblyPath())}}" />
                  <UsingTask TaskName="Microsoft.Build.Tasks.WriteLinesToFile" AssemblyFile="{{Escape(GetMSBuildTasksAssemblyPath())}}" />

                  <PropertyGroup>
                    <SourceRoot>{{Escape(sourceRoot)}}</SourceRoot>
                    <OutputRoot>{{Escape(outputRoot)}}</OutputRoot>
                    <InputManifestFile>{{Escape(InputManifestFile)}}</InputManifestFile>
                    <InventoryFile>{{Escape(InventoryFile)}}</InventoryFile>
                    <CompletionFile>{{Escape(CompletionFile)}}</CompletionFile>
                    <InvocationFile>{{Escape(InvocationFile)}}</InvocationFile>
                  </PropertyGroup>

                  <ItemGroup>
                    <LayoutInput Include="$(SourceRoot)/*.nupkg">
                      <DestinationPath>$([MSBuild]::NormalizePath('$(OutputRoot)', '$(DestinationSubdirectory)', '%(Filename)%(Extension)'))</DestinationPath>
                    </LayoutInput>
                  </ItemGroup>

                  <Target Name="PrepareLayout">
                    <ItemGroup>
                      <LayoutInput Update="@(LayoutInput)">
                        <NormalizedSourcePath>%(FullPath)</NormalizedSourcePath>
                        <NormalizedDestinationPath>%(DestinationPath)</NormalizedDestinationPath>
                      </LayoutInput>
                      <LayoutInput Update="@(LayoutInput)"
                                   Condition="$([MSBuild]::IsOSPlatform('Windows'))">
                        <NormalizedSourcePath>$([System.String]::Copy('%(NormalizedSourcePath)').ToUpperInvariant())</NormalizedSourcePath>
                        <NormalizedDestinationPath>$([System.String]::Copy('%(NormalizedDestinationPath)').ToUpperInvariant())</NormalizedDestinationPath>
                      </LayoutInput>
                      <LayoutInput Update="@(LayoutInput)">
                        <InputManifestLine>%(NormalizedSourcePath)|%(NormalizedDestinationPath)</InputManifestLine>
                      </LayoutInput>
                      <ExpectedOutput Include="@(LayoutInput->'%(DestinationPath)')" />
                      <MissingOutput Include="@(ExpectedOutput)"
                                     Condition="!Exists('%(Identity)')" />
                      <ExistingOwnedOutput Include="$(OutputRoot)/**/*.nupkg" />
                      <StaleOutput Include="@(ExistingOwnedOutput)"
                                   Exclude="@(ExpectedOutput)" />
                    </ItemGroup>
                    <WriteLinesToFile File="$(InputManifestFile)"
                                      Lines="@(LayoutInput->'%(InputManifestLine)')"
                                      Overwrite="true"
                                      WriteOnlyWhenDifferent="true" />
                    <WriteLinesToFile File="$(InventoryFile)"
                                      Lines="@(LayoutInput->'%(NormalizedDestinationPath)')"
                                      Overwrite="true"
                                      WriteOnlyWhenDifferent="true" />
                    <ReadLinesFromFile File="$(CompletionFile)"
                                       Condition="Exists('$(CompletionFile)')">
                      <Output TaskParameter="Lines" PropertyName="CompletionValue" />
                    </ReadLinesFromFile>
                    <Delete Files="$(CompletionFile)"
                            Condition="'@(MissingOutput)' != '' or '@(StaleOutput)' != '' or (Exists('$(CompletionFile)') and '$(CompletionValue)' != 'complete')" />
                  </Target>

                  <Target Name="Layout"
                          DependsOnTargets="PrepareLayout"
                          Inputs="@(LayoutInput);$(InputManifestFile);$(InventoryFile)"
                          Outputs="$(CompletionFile)">
                    <Delete Files="$(CompletionFile)" />
                    <PrepareIncrementalLayout
                        SourceFiles="@(LayoutInput)"
                        DestinationFiles="@(ExpectedOutput)"
                        ExistingOutputs="@(ExistingOwnedOutput)">
                      <Output TaskParameter="ExpectedOutputs" ItemName="PreparedOutput" />
                      <Output TaskParameter="StaleOutputs" ItemName="PreparedStaleOutput" />
                    </PrepareIncrementalLayout>
                    <Delete Files="@(PreparedStaleOutput)" />
                    <Error Condition="'$(InterruptionPoint)' == 'AfterDelete'"
                           Text="Simulated interruption after stale-output deletion." />
                    <Copy SourceFiles="@(LayoutInput)"
                          DestinationFiles="@(PreparedOutput)"
                          SkipUnchangedFiles="true" />
                    <Error Condition="'$(InterruptionPoint)' == 'AfterCopy'"
                           Text="Simulated interruption after copying outputs." />
                    <WriteLinesToFile File="$(InvocationFile)"
                                      Lines="$([System.Guid]::NewGuid())"
                                      Overwrite="true" />
                    <CompleteIncrementalLayout
                        ExpectedOutputs="@(PreparedOutput)"
                        CompletionStampFile="$(CompletionFile)" />
                  </Target>
                </Project>
                """);
        }

        public string Root { get; }
        public string InputManifestFile { get; }
        public string InventoryFile { get; }
        public string CompletionFile { get; }
        public string InvocationFile { get; }
        public string[] StateFiles => [InputManifestFile, InventoryFile, CompletionFile];

        public string Output(string fileName, string? subdirectory = null) =>
            Path.Combine(Root, "layout", subdirectory ?? string.Empty, fileName);

        public string Build()
        {
            return Build(interruptionPoint: null, destinationSubdirectory: null, expectedSuccess: true);
        }

        public string BuildWithDestinationSubdirectory(string destinationSubdirectory)
        {
            return Build(
                interruptionPoint: null,
                destinationSubdirectory: destinationSubdirectory,
                expectedSuccess: true);
        }

        public void BuildShouldFail(string interruptionPoint)
        {
            Build(interruptionPoint, destinationSubdirectory: null, expectedSuccess: false);
        }

        private string Build(string? interruptionPoint, string? destinationSubdirectory, bool expectedSuccess)
        {
            var globalProperties = new Dictionary<string, string>();
            if (interruptionPoint is not null)
            {
                globalProperties["InterruptionPoint"] = interruptionPoint;
            }
            if (destinationSubdirectory is not null)
            {
                globalProperties["DestinationSubdirectory"] = destinationSubdirectory;
            }

            using var collection = new ProjectCollection(globalProperties);
            Project project = collection.LoadProject(_projectFile);
            project.Build("Layout").Should().Be(expectedSuccess);
            return File.Exists(InvocationFile) ? File.ReadAllText(InvocationFile) : string.Empty;
        }

    }
}
