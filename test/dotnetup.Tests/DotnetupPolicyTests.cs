// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class DotnetupPolicyTests
{
    private FakePolicyStore _store = null!;

    [TestInitialize]
    public void Initialize()
    {
        _store = new FakePolicyStore();
        DotnetupPolicy.Store = _store;
        UnsignedSourcePolicy.OverrideForTesting = null;
    }

    [TestCleanup]
    public void Cleanup()
    {
        DotnetupPolicy.ResetStoreForTesting();
        UnsignedSourcePolicy.OverrideForTesting = null;
    }

    [TestMethod]
    public void EnableAdminMode_WritesAdminModeAndBlockUnsignedDownloads()
    {
        DotnetupPolicy.EnableAdminMode();

        _store.Writes.Should().ContainSingle().Which.Should().BeEquivalentTo(new Dictionary<string, bool>
        {
            [DotnetupPolicy.AdminModeValueName] = true,
            [DotnetupPolicy.BlockUnsignedDownloadsValueName] = true,
        });
    }

    [TestMethod]
    public void DisableAdminMode_WritesOnlyAdminModeFalse()
    {
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = true;

        DotnetupPolicy.DisableAdminMode();

        _store.Writes.Should().ContainSingle().Which.Should().BeEquivalentTo(new Dictionary<string, bool>
        {
            [DotnetupPolicy.AdminModeValueName] = false,
        });
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName].Should().BeTrue();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void IsAdminModeEnabled_ReadsStoreOnWindows()
    {
        DotnetupPolicy.IsAdminModeEnabled().Should().BeFalse();

        _store.Values[DotnetupPolicy.AdminModeValueName] = true;

        DotnetupPolicy.IsAdminModeEnabled().Should().BeTrue();
    }

    [TestMethod, OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsAdminModeEnabled_IsFalseOffWindows()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;

        DotnetupPolicy.IsAdminModeEnabled().Should().BeFalse();
    }

    [TestMethod]
    public void IsUnsignedDownloadBlockedByRegistry_ReadsStore()
    {
        DotnetupPolicy.IsUnsignedDownloadBlockedByRegistry().Should().BeFalse();

        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = true;

        DotnetupPolicy.IsUnsignedDownloadBlockedByRegistry().Should().BeTrue();
    }

    [TestMethod]
    public void UnsignedSourcePolicy_ConstantsAliasDotnetupPolicy()
    {
        UnsignedSourcePolicy.WindowsPolicyKey.Should().Be(DotnetupPolicy.WindowsPolicyKey);
        UnsignedSourcePolicy.WindowsPolicyValueName.Should().Be(DotnetupPolicy.BlockUnsignedDownloadsValueName);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void UnsignedSourcePolicy_ReadsDotnetupPolicyStoreOnWindows()
    {
        UnsignedSourcePolicy.IsUnsignedDownloadBlocked().Should().BeFalse();

        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = true;

        UnsignedSourcePolicy.IsUnsignedDownloadBlocked().Should().BeTrue();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void UnsignedSourcePolicy_OverrideForTestingWins(bool overrideValue)
    {
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = !overrideValue;
        UnsignedSourcePolicy.OverrideForTesting = () => overrideValue;

        UnsignedSourcePolicy.IsUnsignedDownloadBlocked().Should().Be(overrideValue);
    }

    private sealed class FakePolicyStore : IDotnetupPolicyStore
    {
        public Dictionary<string, bool> Values { get; } = new(StringComparer.Ordinal);

        public List<Dictionary<string, bool>> Writes { get; } = [];

        public bool ReadFlag(string valueName) => Values.TryGetValue(valueName, out bool value) && value;

        public void WriteFlags(IReadOnlyDictionary<string, bool> values)
        {
            Writes.Add(new Dictionary<string, bool>(values));
            foreach (var (name, value) in values)
            {
                Values[name] = value;
            }
        }
    }
}
