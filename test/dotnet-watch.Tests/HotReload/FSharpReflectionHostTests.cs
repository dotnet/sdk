// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Reflection;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class FSharpReflectionHostTests
{
    [TestMethod]
    public void TryGetFSharpOptionValue_HandlesStaticIsSomeAccessor()
    {
        var assembly = typeof(MessageDescriptor).Assembly;
        var serviceType = assembly.GetType("Microsoft.DotNet.Watch.FSharpHotReloadService", throwOnError: true)!;
        var reflectionHostType = serviceType.GetNestedType("FSharpReflectionHost", BindingFlags.NonPublic)!;

        var tryGetOptionValue = reflectionHostType.GetMethod(
            "TryGetFSharpOptionValue",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var someValue = StaticAccessorOption.Some("workspace");

        var someArgs = new object?[] { someValue, null };
        var someResult = (bool)tryGetOptionValue.Invoke(null, someArgs)!;
        Assert.IsTrue(someResult);
        Assert.AreEqual("workspace", someArgs[1]);

        var noneValue = StaticAccessorOption.None;
        var noneArgs = new object?[] { noneValue, null };
        var noneResult = (bool)tryGetOptionValue.Invoke(null, noneArgs)!;
        Assert.IsFalse(noneResult);
        Assert.IsNull(noneArgs[1]);
    }

    [TestMethod]
    public void CreateFSharpOptionSome_PreservesCancellationToken()
    {
        var assembly = typeof(MessageDescriptor).Assembly;
        var serviceType = assembly.GetType("Microsoft.DotNet.Watch.FSharpHotReloadService", throwOnError: true)!;
        var reflectionHostType = serviceType.GetNestedType("FSharpReflectionHost", BindingFlags.NonPublic)!;
        var createSome = reflectionHostType.GetMethod("CreateFSharpOptionSome", BindingFlags.NonPublic | BindingFlags.Static)!;
        var tryGetOptionValue = reflectionHostType.GetMethod("TryGetFSharpOptionValue", BindingFlags.NonPublic | BindingFlags.Static)!;

        var fsharpCorePath = Path.Combine(
            SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest,
            "FSharp",
            "FSharp.Core.dll");
        var fsharpCore = Assembly.LoadFrom(fsharpCorePath);
        var optionType = fsharpCore.GetType("Microsoft.FSharp.Core.FSharpOption`1", throwOnError: true)!
            .MakeGenericType(typeof(CancellationToken));
        using var cancellationSource = new CancellationTokenSource();

        var option = createSome.Invoke(null, [optionType, cancellationSource.Token]);
        var arguments = new object?[] { option, null };

        Assert.IsNotNull(option);
        Assert.IsTrue((bool)tryGetOptionValue.Invoke(null, arguments)!);
        Assert.AreEqual(cancellationSource.Token, (CancellationToken)arguments[1]!);
    }

    [TestMethod]
    public void GetRequiredCapabilities_ReadsExactValuesAndFallsBackToBaseline()
    {
        var assembly = typeof(MessageDescriptor).Assembly;
        var serviceType = assembly.GetType("Microsoft.DotNet.Watch.FSharpHotReloadService", throwOnError: true)!;
        var reflectionHostType = serviceType.GetNestedType("FSharpReflectionHost", BindingFlags.NonPublic)!;
        var getRequiredCapabilities = reflectionHostType.GetMethod(
            "GetRequiredCapabilities",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var exactDelta = new DeltaWithRequiredCapabilities(
            ["Baseline", "GenericUpdateMethod", "GenericUpdateMethod"]);
        var exact = (ImmutableArray<string>)getRequiredCapabilities.Invoke(
            null,
            [exactDelta.GetType(), exactDelta])!;
        var legacyDelta = new LegacyDelta();
        var legacy = (ImmutableArray<string>)getRequiredCapabilities.Invoke(
            null,
            [legacyDelta.GetType(), legacyDelta])!;

        Assert.AreSequenceEqual(
            new[] { "Baseline", "GenericUpdateMethod" },
            exact.ToArray());
        Assert.AreSequenceEqual(new[] { "Baseline" }, legacy.ToArray());
    }

    private sealed class StaticAccessorOption
    {
        private StaticAccessorOption(bool isSome, object? value)
        {
            _isSome = isSome;
            _value = value;
        }

        private readonly bool _isSome;
        private readonly object? _value;

        public static StaticAccessorOption None { get; } = new(false, null);

        public static StaticAccessorOption Some(string value) => new(true, value);

        public static bool get_IsSome(StaticAccessorOption option) => option._isSome;

        public static object? get_Value(StaticAccessorOption option) => option._value;
    }

    private sealed record DeltaWithRequiredCapabilities(string[] RequiredCapabilities);

    private sealed class LegacyDelta
    {
    }
}
