// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.HotReload.UnitTests;

// Skips a test when the current process is not 64-bit.
//
// NOTE: this is a hand-rolled, single-purpose ConditionBaseAttribute. xUnit ships a generic
// [ConditionalFact(typeof(X), nameof(member))] that evaluates an arbitrary static bool member
// at runtime and skips the test if it returns false. MSTest does not have an equivalent yet
// (see https://github.com/microsoft/testfx/issues/9070) — once it does, this attribute should
// be replaced.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
internal sealed class Is64BitProcessConditionAttribute : ConditionBaseAttribute
{
    public Is64BitProcessConditionAttribute()
        : base(ConditionMode.Include)
    {
        IgnoreMessage = "This test requires a 64-bit process.";
    }

    public override string GroupName => nameof(Is64BitProcessConditionAttribute);

    public override bool IsConditionMet => Environment.Is64BitProcess;
}
