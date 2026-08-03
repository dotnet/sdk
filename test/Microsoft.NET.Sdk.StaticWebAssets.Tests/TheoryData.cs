// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

public class TheoryData<T1> : List<object?[]>
{
    public void Add(T1 item1) => Add([item1]);
}

public class TheoryData<T1, T2> : List<object?[]>
{
    public void Add(T1 item1, T2 item2) => Add([item1, item2]);
}

public class TheoryData<T1, T2, T3, T4, T5> : List<object?[]>
{
    public void Add(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => Add([item1, item2, item3, item4, item5]);
}
