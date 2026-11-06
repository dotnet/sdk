// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections;
using System.Collections.Generic;

namespace Test.Utilities
{
    /// <summary>
    /// Minimal MSTest-friendly replacement for xUnit's <c>TheoryData</c> types. It exposes the
    /// same authoring surface (collection initializers and <c>Add</c> overloads, plus the
    /// <c>IEnumerable&lt;T&gt;</c>/<c>params</c> constructors used by the migrated tests) while
    /// implementing <see cref="IEnumerable{T}"/> of <see cref="object"/>[] so it can be consumed
    /// directly by MSTest's <c>[DynamicData]</c>.
    /// </summary>
    public abstract class TheoryData : IEnumerable<object[]>
    {
        private readonly List<object[]> _rows = new();

        protected void AddRow(params object[] values) => _rows.Add(values);

        public IEnumerator<object[]> GetEnumerator() => _rows.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _rows.GetEnumerator();
    }

    public class TheoryData<T> : TheoryData
    {
        public TheoryData()
        {
        }

        public TheoryData(IEnumerable<T> values)
        {
            foreach (var value in values)
            {
                Add(value);
            }
        }

        public TheoryData(params T[] values)
        {
            foreach (var value in values)
            {
                Add(value);
            }
        }

        public void Add(T p) => AddRow(p);
    }

    public class TheoryData<T1, T2> : TheoryData
    {
        public void Add(T1 p1, T2 p2) => AddRow(p1, p2);
    }

    public class TheoryData<T1, T2, T3> : TheoryData
    {
        public void Add(T1 p1, T2 p2, T3 p3) => AddRow(p1, p2, p3);
    }

    public class TheoryData<T1, T2, T3, T4> : TheoryData
    {
        public void Add(T1 p1, T2 p2, T3 p3, T4 p4) => AddRow(p1, p2, p3, p4);
    }
}
