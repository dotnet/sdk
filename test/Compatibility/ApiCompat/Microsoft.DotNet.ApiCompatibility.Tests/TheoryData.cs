// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class TheoryData<T1, T2, T3> : List<object[]>
    {
        public void Add(T1 item1, T2 item2, T3 item3) => Add([item1!, item2!, item3!]);
    }
}
