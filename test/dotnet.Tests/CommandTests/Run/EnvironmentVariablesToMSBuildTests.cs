// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Run.Tests;

public sealed class EnvironmentVariablesToMSBuildTests
{
    [Fact]
    public void HasRuntimeEnvironmentVariableSupport_ReturnsTrue_WhenCapabilityIsPresent()
    {
        var project = CreateProjectInstance($"""
            <Project>
              <ItemGroup>
                <{Constants.ProjectCapability} Include="{Constants.RuntimeEnvironmentVariableSupport}" />
              </ItemGroup>
            </Project>
            """);

        EnvironmentVariablesToMSBuild.HasRuntimeEnvironmentVariableSupport(project).Should().BeTrue();
    }

    [Fact]
    public void HasRuntimeEnvironmentVariableSupport_ReturnsFalse_WhenNoCapabilities()
    {
        var project = CreateProjectInstance("<Project />");

        EnvironmentVariablesToMSBuild.HasRuntimeEnvironmentVariableSupport(project).Should().BeFalse();
    }

    [Fact]
    public void HasRuntimeEnvironmentVariableSupport_ReturnsFalse_WhenOnlyOtherCapabilities()
    {
        var project = CreateProjectInstance($"""
            <Project>
              <ItemGroup>
                <{Constants.ProjectCapability} Include="SomeOtherCapability" />
              </ItemGroup>
            </Project>
            """);

        EnvironmentVariablesToMSBuild.HasRuntimeEnvironmentVariableSupport(project).Should().BeFalse();
    }

    [Fact]
    public void HasRuntimeEnvironmentVariableSupport_IsCaseInsensitive()
    {
        var project = CreateProjectInstance($"""
            <Project>
              <ItemGroup>
                <{Constants.ProjectCapability} Include="runtimeenvironmentvariablesupport" />
              </ItemGroup>
            </Project>
            """);

        EnvironmentVariablesToMSBuild.HasRuntimeEnvironmentVariableSupport(project).Should().BeTrue();
    }

    [Fact]
    public void ReadFromItems_ReturnsEmpty_WhenNoItems()
    {
        var project = CreateProjectInstance("<Project />");

        EnvironmentVariablesToMSBuild.ReadFromItems(project).Should().BeEmpty();
    }

    [Fact]
    public void ReadFromItems_ReadsIncludeAsNameAndValueMetadataAsValue()
    {
        var project = CreateProjectInstance($"""
            <Project>
              <ItemGroup>
                <{Constants.RuntimeEnvironmentVariable} Include="FOO" Value="BAR" />
              </ItemGroup>
            </Project>
            """);

        EnvironmentVariablesToMSBuild.ReadFromItems(project).Should()
            .ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, string>("FOO", "BAR"));
    }

    [Fact]
    public void ReadFromItems_ReadsMultipleItems()
    {
        var project = CreateProjectInstance($"""
            <Project>
              <ItemGroup>
                <{Constants.RuntimeEnvironmentVariable} Include="FOO" Value="BAR" />
                <{Constants.RuntimeEnvironmentVariable} Include="ANOTHER" Value="VALUE" />
              </ItemGroup>
            </Project>
            """);

        EnvironmentVariablesToMSBuild.ReadFromItems(project).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["FOO"] = "BAR",
            ["ANOTHER"] = "VALUE",
        });
    }

    [Fact]
    public void ReadFromItems_ReturnsEmptyStringValue_WhenValueMetadataIsMissing()
    {
        var project = CreateProjectInstance($"""
            <Project>
              <ItemGroup>
                <{Constants.RuntimeEnvironmentVariable} Include="FOO" />
              </ItemGroup>
            </Project>
            """);

        EnvironmentVariablesToMSBuild.ReadFromItems(project).Should()
            .ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, string>("FOO", string.Empty));
    }

    [Fact]
    public void AddAsItems_ThenReadFromItems_RoundTripsNamesAndValues()
    {
        var project = CreateProjectInstance("<Project />");

        var environmentVariables = new Dictionary<string, string>
        {
            ["FOO"] = "BAR",
            ["ANOTHER"] = "VALUE",
        };

        EnvironmentVariablesToMSBuild.AddAsItems(project, environmentVariables);

        EnvironmentVariablesToMSBuild.ReadFromItems(project).Should().BeEquivalentTo(environmentVariables);
    }

    private static ProjectInstance CreateProjectInstance(string projectXml)
    {
        var collection = new ProjectCollection();
        using var reader = XmlReader.Create(new StringReader(projectXml));
        var root = ProjectRootElement.Create(reader, collection);
        return new Microsoft.Build.Evaluation.Project(root, globalProperties: null, toolsVersion: null, collection).CreateProjectInstance();
    }
}
