// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
}
