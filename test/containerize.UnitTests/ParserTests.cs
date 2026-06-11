// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.NET.Build.Containers;

namespace containerize.UnitTests;

[TestClass]
public class ParserTests
{
    [TestMethod]
    public void CanParseLabels()
    {
        ContainerizeCommand command = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff"), nameof(CanParseLabels)));
        List<string> baseArgs = new()
        {
            publishDir.FullName,
            command.BaseRegistryOption.Name,
            "MyBaseRegistry",
            command.BaseImageNameOption.Name,
            "MyBaseImageName",
            command.RepositoryOption.Name,
            "MyImageName",
            command.WorkingDirectoryOption.Name,
            "MyWorkingDirectory",
            command.EntrypointOption.Name,
            "MyEntryPoint"
        };

        baseArgs.Add(command.LabelsOption.Name);
        baseArgs.Add("NoValue=");
        baseArgs.Add("Valid2=Val2");
        baseArgs.Add("Valid3=Val 3");
        baseArgs.Add("Valid4=\"Val4\"");
        baseArgs.Add("Unbalanced1=\"Un1");
        baseArgs.Add("Unbalanced2=Un2\"");


        ParseResult parseResult = command.Parse(baseArgs.ToArray());

        Dictionary<string, string>? labels = parseResult.GetValue(command.LabelsOption);

        Assert.IsNotNull(labels);
        Assert.AreEqual(6, labels.Count);
        Assert.IsEmpty(labels["NoValue"]);
        Assert.AreEqual("Val2", labels["Valid2"]);
        Assert.AreEqual("Val 3", labels["Valid3"]);
        Assert.AreEqual("\"Val4\"", labels["Valid4"]);
        Assert.AreEqual("\"Un1", labels["Unbalanced1"]);
        Assert.AreEqual("Un2\"", labels["Unbalanced2"]);
    }

    [TestMethod]
    public void CanParseLabels2()
    {
        ContainerizeCommand command = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff"), nameof(CanParseLabels)));
        List<string> baseArgs = new()
        {
            publishDir.FullName,
            command.BaseRegistryOption.Name,
            "MyBaseRegistry",
            command.BaseImageNameOption.Name,
            "MyBaseImageName",
            command.RepositoryOption.Name,
            "MyImageName",
            command.WorkingDirectoryOption.Name,
            "MyWorkingDirectory",
            command.EntrypointOption.Name,
            "MyEntryPoint"
        };

        baseArgs.Add(command.LabelsOption.Name);
        baseArgs.Add("NoValue=");
        baseArgs.Add("Valid2=Val2");

        ParseResult parseResult = command.Parse(string.Join(" ", baseArgs));

        Dictionary<string, string>? labels = parseResult.GetValue(command.LabelsOption);

        Assert.IsNotNull(labels);
        Assert.AreEqual(2, labels.Count);
        Assert.IsEmpty(labels["NoValue"]);
        Assert.AreEqual("Val2", labels["Valid2"]);
    }

    [TestMethod]
    [DataRow("not-a-label")]
    [DataRow("not", "a", "label")]
    public void CanHandleInvalidLabels(params string[] labelStr)
    {
        ContainerizeCommand command = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff"), nameof(CanParseLabels)));
        List<string> baseArgs = new()
        {
            publishDir.FullName,
            command.BaseRegistryOption.Name,
            "MyBaseRegistry",
            command.BaseImageNameOption.Name,
            "MyBaseImageName",
            command.RepositoryOption.Name,
            "MyImageName",
            command.WorkingDirectoryOption.Name,
            "MyWorkingDirectory",
            command.EntrypointOption.Name,
            "MyEntryPoint"
        };

        baseArgs.Add(command.LabelsOption.Name);
        foreach (var label in labelStr)
        {
            baseArgs.Add(label);
        }

        ParseResult parseResult = command.Parse(baseArgs.ToArray());
        Assert.ContainsSingle(parseResult.Errors);

        Assert.AreEqual($"Incorrectly formatted labels: {string.Join(";", labelStr)}", parseResult.Errors[0].Message);
    }

    [TestMethod]
    public void CanParseEnvironmentVariables()
    {
        ContainerizeCommand command = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff"), nameof(CanParseEnvironmentVariables)));
        List<string> baseArgs = new()
        {
            publishDir.FullName,
            command.BaseRegistryOption.Name,
            "MyBaseRegistry",
            command.BaseImageNameOption.Name,
            "MyBaseImageName",
            command.RepositoryOption.Name,
            "MyImageName",
            command.WorkingDirectoryOption.Name,
            "MyWorkingDirectory",
            command.EntrypointOption.Name,
            "MyEntryPoint"
        };

        baseArgs.Add(command.EnvVarsOption.Name);
        baseArgs.Add("NoValue=");
        baseArgs.Add("Valid2=Val2");
        baseArgs.Add("Valid3=Val 3");
        baseArgs.Add("Valid4=\"Val4\"");
        baseArgs.Add("Unbalanced1=\"Un1");
        baseArgs.Add("Unbalanced2=Un2\"");


        ParseResult parseResult = command.Parse(baseArgs.ToArray());
        Assert.IsEmpty(parseResult.Errors);

        Dictionary<string, string>? envVars = parseResult.GetValue(command.EnvVarsOption);

        Assert.IsNotNull(envVars);
        Assert.AreEqual(6, envVars.Count);
        Assert.IsEmpty(envVars["NoValue"]);
        Assert.AreEqual("Val2", envVars["Valid2"]);
        Assert.AreEqual("Val 3", envVars["Valid3"]);
        Assert.AreEqual("\"Val4\"", envVars["Valid4"]);
        Assert.AreEqual("\"Un1", envVars["Unbalanced1"]);
        Assert.AreEqual("Un2\"", envVars["Unbalanced2"]);
    }

    [TestMethod]
    public void CanParsePorts()
    {
        ContainerizeCommand command = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff"), nameof(CanParsePorts)));
        List<string> baseArgs = new()
        {
            publishDir.FullName,
            command.BaseRegistryOption.Name,
            "MyBaseRegistry",
            command.BaseImageNameOption.Name,
            "MyBaseImageName",
            command.RepositoryOption.Name,
            "MyImageName",
            command.WorkingDirectoryOption.Name,
            "MyWorkingDirectory",
            command.EntrypointOption.Name,
            "MyEntryPoint"
        };

        baseArgs.Add(command.PortsOption.Name);
        baseArgs.Add("1500");
        baseArgs.Add("1501/udp");
        baseArgs.Add("1501/tcp");
        baseArgs.Add("1502");


        ParseResult parseResult = command.Parse(baseArgs.ToArray());
        Assert.IsEmpty(parseResult.Errors);

        Port[]? ports = parseResult.GetValue(command.PortsOption);

        Assert.IsNotNull(ports);
        Assert.AreEqual(4, ports.Length);
        Assert.Contains(new Port(1500, PortType.tcp), ports);
        Assert.Contains(new Port(1501, PortType.udp), ports);
        Assert.Contains(new Port(1501, PortType.tcp), ports);
        Assert.Contains(new Port(1502, PortType.tcp), ports);
    }

    [TestMethod]
    [DataRow("1501/smth", "(InvalidPortType)")]
    [DataRow("1501\\tcp", "(InvalidPortNumber)")]
    [DataRow("not-a-number", "(InvalidPortNumber)")]
    public void CanHandleInvalidPorts(string portStr, string reason)
    {
        string errorMessage = $"Incorrectly formatted ports:{Environment.NewLine}\t{portStr}:\t{reason}{Environment.NewLine}";

        ContainerizeCommand command = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff"), nameof(CanParsePorts)));
        List<string> baseArgs = new()
        {
            publishDir.FullName,
            command.BaseRegistryOption.Name,
            "MyBaseRegistry",
            command.BaseImageNameOption.Name,
            "MyBaseImageName",
            command.RepositoryOption.Name,
            "MyImageName",
            command.WorkingDirectoryOption.Name,
            "MyWorkingDirectory",
            command.EntrypointOption.Name,
            "MyEntryPoint"
        };

        baseArgs.Add(command.PortsOption.Name);
        baseArgs.Add(portStr);

        ParseResult parseResult = command.Parse(baseArgs.ToArray());
        Assert.ContainsSingle(parseResult.Errors);

        Assert.AreEqual(errorMessage, parseResult.Errors[0].Message);
    }
}

