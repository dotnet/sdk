// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Reflection;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Tool.Store;
using Microsoft.DotNet.Cli.Utils;
using BuildCommand = Microsoft.DotNet.Cli.Commands.Build.BuildCommand;
using CleanCommand = Microsoft.DotNet.Cli.Commands.Clean.CleanCommand;
using MSBuildCommand = Microsoft.DotNet.Cli.Commands.MSBuild.MSBuildCommand;
using PackCommand = Microsoft.DotNet.Cli.Commands.Pack.PackCommand;
using PublishCommand = Microsoft.DotNet.Cli.Commands.Publish.PublishCommand;
using RestoreCommand = Microsoft.DotNet.Cli.Commands.Restore.RestoreCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests;

[TestClass]
public class CommandApiCompatibilityTests : SdkTest
{
    [TestMethod]
    [DataRow(typeof(BuildCommand), typeof(CommandBase))]
    [DataRow(typeof(CleanCommand), typeof(CommandBase))]
    [DataRow(typeof(MSBuildCommand), typeof(MSBuildCommand))]
    [DataRow(typeof(PackCommand), typeof(CommandBase))]
    [DataRow(typeof(PublishCommand), typeof(CommandBase))]
    [DataRow(typeof(RestoreCommand), typeof(CommandBase))]
    [DataRow(typeof(StoreCommand), typeof(StoreCommand))]
    public void ExistingFactorySignaturesArePreserved(Type commandType, Type returnType)
    {
        AssertFactorySignature(commandType, "FromArgs", typeof(string[]), returnType);
        AssertFactorySignature(commandType, "FromParseResult", typeof(ParseResult), returnType);
    }

    [TestMethod]
    public void ExistingRestoreForwardingSignatureIsPreserved()
    {
        AssertFactorySignature(typeof(RestoreCommand), "CreateForwarding", typeof(MSBuildArgs), typeof(MSBuildForwardingApp));
    }

    [TestMethod]
    [DataRow(typeof(CommandBase), new Type[] { }, false)]
    [DataRow(typeof(CommandBase), new Type[] { typeof(ParseResult) }, false)]
    [DataRow(typeof(CommandBase<System.CommandLine.Command>), new Type[] { typeof(ParseResult) }, true)]
    [DataRow(typeof(CleanCommand), new Type[] { typeof(MSBuildArgs), typeof(string) }, true)]
    [DataRow(typeof(MSBuildCommand), new Type[] { typeof(IEnumerable<string>), typeof(string) }, true)]
    [DataRow(typeof(MSBuildForwardingApp), new Type[] { typeof(IEnumerable<string>), typeof(string) }, true)]
    [DataRow(typeof(MSBuildForwardingApp), new Type[] { typeof(MSBuildArgs), typeof(string) }, true)]
    [DataRow(typeof(PackCommand), new Type[] { typeof(MSBuildArgs), typeof(bool), typeof(string) }, true)]
    [DataRow(typeof(RestoringCommand), new Type[] { typeof(MSBuildArgs), typeof(bool), typeof(string), typeof(string), typeof(bool?) }, true)]
    public void ExistingConstructorSignaturesArePreserved(Type commandType, Type[] parameterTypes, bool isPublic)
    {
        // Source calls with omitted optional arguments would not detect a removed CLR signature.
        var constructor = commandType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            parameterTypes,
            modifiers: null);

        constructor.Should().NotBeNull();
        constructor!.IsPublic.Should().Be(isPublic);
        constructor.IsFamily.Should().Be(!isPublic);
    }

    private static void AssertFactorySignature(Type commandType, string methodName, Type inputType, Type returnType)
    {
        var method = commandType.GetMethod(methodName, [inputType, typeof(string)]);

        method.Should().NotBeNull();
        method!.IsPublic.Should().BeTrue();
        method.IsStatic.Should().BeTrue();
        method.ReturnType.Should().Be(returnType);
        method.GetParameters()[1].IsOptional.Should().BeTrue();
        method.GetParameters()[1].DefaultValue.Should().BeNull();
    }
}
