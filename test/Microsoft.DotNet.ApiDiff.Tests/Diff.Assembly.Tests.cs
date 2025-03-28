// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffAssemblyTests : DiffBaseTests
{
    [Fact]
    public Task AssemblyAdd() => RunTestAsync(
                before: [],
                after: [("MyAddedAssembly.dll", """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """)],
                expected: new Dictionary<string, string>() {
                    { "MyAddedAssembly", """
                    + namespace MyNamespace
                    + {
                    +     public struct MyStruct
                    +     {
                    +     }
                    + }
                    """ }
                });

    [Fact]
    public Task AssemblyChange() => RunTestAsync(
                before: [("MyBeforeAssembly.dll", """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """)],
                after: [("MyAfterAssembly.dll", """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """)],
                expected: new Dictionary<string, string>() {
                    { "MyBeforeAssembly", """
                    - namespace MyNamespace
                    - {
                    -     public struct MyStruct
                    -     {
                    -     }
                    - }
                    """ },
                    { "MyAfterAssembly", """
                    + namespace MyNamespace
                    + {
                    +     public struct MyStruct
                    +     {
                    +     }
                    + }
                    """ }
                });

    [Fact]
    public Task AssemblyDelete() => RunTestAsync(
                before: [("MyRemovedAssembly.dll", """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """)],
                after: [],
                expected: new Dictionary<string, string>() {
                    { "MyRemovedAssembly", """
                    - namespace MyNamespace
                    - {
                    -     public struct MyStruct
                    -     {
                    -     }
                    - }
                    """ }
                });

    [Fact]
    public Task AssemblyUnchanged() => RunTestAsync(
                before: [("MyAssembly.dll", """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """)],
                after: [("MyAssembly.dll", """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """)],
                expected: []); // No changes

}
