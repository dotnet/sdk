// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class GlobalJsonChannelResolverTests
{
    [TestMethod]
    [DataRow(null, "10.0.105")]
    [DataRow("disable", "10.0.103")]
    [DataRow("patch", "10.0.105")]
    [DataRow("feature", "10.0.202")]
    [DataRow("minor", "10.1.102")]
    [DataRow("major", "11.0.101")]
    [DataRow("latestPatch", "10.0.105")]
    [DataRow("latestFeature", "10.0.202")]
    [DataRow("latestMinor", "10.1.102")]
    [DataRow("latestMajor", "11.0.101")]
    public void Resolve_HonorsEachPolicy(string? policy, string expected)
    {
        var policyProperty = policy is null ? "" : $""","rollForward":"{policy}" """;
        var contents = $$$"""{"sdk":{"version":"10.0.103"{{{policyProperty}}}}}""";

        var channel = ReadChannel(contents);

        Resolve(channel, Installations("10.0.103", "10.0.105", "10.0.202", "10.1.102", "11.0.101"))!
            .Version.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("patch", "10.0.103", "10.0.105")]
    [DataRow("patch", "10.0.199", null)]
    [DataRow("feature", "10.0.199", "10.0.301")]
    [DataRow("minor", "10.0.399", "10.2.101")]
    [DataRow("major", "10.9.100", "11.1.101")]
    [DataRow("disable", "10.0.104", null)]
    [DataRow("latestPatch", "10.0.199", null)]
    [DataRow("latestFeature", "10.0.399", null)]
    [DataRow("latestMinor", "10.9.100", null)]
    [DataRow("latestMajor", "12.0.100", null)]
    public void Resolve_RollsForwardWithoutDowngrading(string policy, string version, string? expected)
    {
        var channel = ReadChannel($$$"""{"sdk":{"version":"{{{version}}}","rollForward":"{{{policy}}}"}}""");

        var selected = Resolve(channel, Installations(
            "10.0.105", "10.0.202", "10.0.301", "10.1.101", "10.1.105", "10.2.101", "11.0.105", "11.1.101"));
        (selected?.Version).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("", "11.0.100-preview.2")]
    [DataRow(""","allowPrerelease":true""", "11.0.100-preview.2")]
    [DataRow(""","allowPrerelease":false""", "10.0.105")]
    public void Resolve_HonorsAllowPrerelease(string property, string expected)
    {
        var channel = ReadChannel($$$"""{"sdk":{"version":"10.0.100","rollForward":"latestMajor"{{{property}}}}}""");

        Resolve(channel, Installations("10.0.105", "11.0.100-preview.2"))!.Version.Should().Be(expected);
    }

    [TestMethod]
    public void Resolve_PrereleaseRequestOverridesAllowPrereleaseFalseLikeTheHost()
    {
        var channel = ReadChannel("""{"sdk":{"version":"11.0.100-preview.1","allowPrerelease":false}}""");

        Resolve(channel, Installations("11.0.100-preview.1", "11.0.100-preview.2"))!
            .Version.Should().Be("11.0.100-preview.2");
    }

    [TestMethod]
    [DataRow("""{"sdk":{"version":"not-a-version"}}""")]
    [DataRow("""{"sdk":{"version":"10.0.100","rollForward":"not-a-policy"}}""")]
    [DataRow("""{"sdk":{"version":"10.0.100","allowPrerelease":"false"}}""")]
    [DataRow("""{"sdk":[]}""")]
    [DataRow("null")]
    [DataRow("{")]
    public void Read_RejectsMalformedRequirements(string contents)
    {
        var action = () => ReadChannel(contents);
        action.Should().Throw<JsonException>();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Read_SupportsCommentsTrailingCommasAndEncoding(bool utf16)
    {
        var channel = ReadChannel("""
            {
              // SDK policy
              "sdk": {
                "version": "10.0.103",
                "rollForward": "latestPatch",
              },
            }
            """, utf16 ? Encoding.Unicode : new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        channel.MinimumVersion!.ToString().Should().Be("10.0.103");
        channel.Name.Should().Be("10.0.1xx");
        channel.AllowPrerelease.Should().BeTrue();
        Resolve(channel, Installations("10.0.105"))!.Version.Should().Be("10.0.105");
    }

    private static List<Installation> Installations(params string[] versions) =>
        versions.Select(v => new Installation { Component = InstallComponent.SDK, Version = v }).ToList();

    private static Installation? Resolve(UpdateChannel channel, IEnumerable<Installation> installations) =>
        installations.Where(i => channel.Matches(new ReleaseVersion(i.Version)))
            .OrderByDescending(i => new ReleaseVersion(i.Version)).FirstOrDefault();

    private static UpdateChannel ReadChannel(string contents, Encoding? encoding = null)
    {
        var directory = Directory.CreateTempSubdirectory("dotnetup-global-json-");
        try
        {
            var path = Path.Combine(directory.FullName, "global.json");
            File.WriteAllText(path, contents, encoding ?? Encoding.UTF8);
            return GlobalJsonChannelResolver.CreateChannel(GlobalJsonChannelResolver.ReadSdkSection(path)!);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
