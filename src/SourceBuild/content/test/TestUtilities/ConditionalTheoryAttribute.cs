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

[XunitTestCaseDiscoverer("TestUtilities.ConditionalTheoryDiscoverer", "TestUtilities")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ConditionalTheoryAttribute : TheoryAttribute
{
    public Type? CalleeType { get; }
    public string[] ConditionMemberNames { get; }

    public ConditionalTheoryAttribute(Type calleeType, params string[] conditionMemberNames)
    {
        CalleeType = calleeType;
        ConditionMemberNames = conditionMemberNames;
    }

    public ConditionalTheoryAttribute(params string[] conditionMemberNames)
    {
        ConditionMemberNames = conditionMemberNames;
    }
}