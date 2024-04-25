// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace TestUtilities;

[XunitTestCaseDiscoverer("TestUtilities.ConditionalFactDiscoverer", "TestUtilities")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ConditionalFactAttribute : FactAttribute
{
    public Type? CalleeType { get; }
    public string[] ConditionMemberNames { get; }

    public ConditionalFactAttribute(Type calleeType, params string[] conditionMemberNames)
    {
        CalleeType = calleeType;
        ConditionMemberNames = conditionMemberNames;
    }

    public ConditionalFactAttribute(params string[] conditionMemberNames)
    {
        ConditionMemberNames = conditionMemberNames;
    }
}