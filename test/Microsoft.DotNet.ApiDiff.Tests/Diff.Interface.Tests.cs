// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

[TestClass]
public class DiffInterfaceTests : DiffBaseTests
{
    #region Interfaces

    [TestMethod]
    public Task InterfaceAdd() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                    public interface IMyInterface
                    {
                        int MyMethod();
                        long MyProperty { get; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public interface IMyInterface
                +     {
                +         int MyMethod();
                +         long MyProperty { get; }
                +     }
                  }
                """);

    [TestMethod]
    public Task InterfaceChange() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public interface IMyBeforeInterface
                    {
                        int MyMethod();
                        long MyProperty { get; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public interface IMyAfterInterface
                    {
                        int MyMethod();
                        long MyProperty { get; }
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public interface IMyBeforeInterface
                -     {
                -         int MyMethod();
                -         long MyProperty { get; }
                -     }
                +     public interface IMyAfterInterface
                +     {
                +         int MyMethod();
                +         long MyProperty { get; }
                +     }
                  }
                """);

    [TestMethod]
    public Task InterfaceDelete() => RunTestAsync(
                beforeCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                    public interface IMyInterface
                    {
                        int MyMethod();
                        long MyProperty { get; }
                    }
                }
                """,
                afterCode: """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                -     public interface IMyInterface
                -     {
                -         int MyMethod();
                -         long MyProperty { get; }
                -     }
                  }
                """);

    [TestMethod]
    [Ignore("The resulting inheritance shows more than expected but not wrong, and does not show the nullability constraint")]
    // Shows: public interface IMyInterface<TKey, TValue> : System.Collections.Generic.IDictionary<TKey, TValue>, System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<TKey, TValue>>, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<TKey, TValue>>, System.Collections.IEnumerable, System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>, System.Collections.Generic.IReadOnlyCollection<System.Collections.Generic.KeyValuePair<TKey, TValue>>
    public Task InterfaceAddWithTypeConstraints() => RunTestAsync(
                beforeCode: """
                using System.Collections.Generic;
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """,
                afterCode: """
                using System.Collections.Generic;
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                    public interface IMyInterface<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue> where TKey : notnull
                    {
                        bool ContainsValue(TValue value);
                    }
                }
                """,
                expectedCode: """
                  namespace MyNamespace
                  {
                +     public interface IMyInterface<TKey, TValue> : System.Collections.Generic.IDictionary<TKey, TValue>, System.Collections.Generic.IReadOnlyDictionary<TKey, TValue> where TKey : notnull
                +     {
                +         bool ContainsValue(TValue value);
                +     }
                  }
                """);

    #endregion
}
