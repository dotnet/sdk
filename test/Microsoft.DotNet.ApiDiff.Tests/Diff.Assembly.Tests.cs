// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffAssemblyTests : DiffBaseTests
{
    [Fact]
    public void TestAssemblyAdd()
    {
        string assemblyName = "MyAddedAssembly.dll";
        RunTest(before: [],
                after: [(assemblyName, """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """)],
                expected: new Dictionary<string, string>() {
                    { assemblyName, """
                    + namespace MyNamespace
                    + {
                    +     public struct MyStruct
                    +     {
                    +     }
                    + }
                    """ }
                });
    }

    [Fact]
    public void TestAssemblyChange()
    {
        RunTest(before: [("MyBeforeAssembly.dll", """
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
                    { "MyBeforeAssembly.dll", """
                    - namespace MyNamespace
                    - {
                    -     public struct MyStruct
                    -     {
                    -     }
                    - }
                    """ },
                    { "MyAfterAssembly.dll", """
                    + namespace MyNamespace
                    + {
                    +     public struct MyStruct
                    +     {
                    +     }
                    + }
                    """ }
                });
    }

    [Fact]
    public void TestAssemblyDelete()
    {
        string assemblyName = "MyRemovedAssembly.dll";
        RunTest(before: [(assemblyName, """
                namespace MyNamespace
                {
                    public struct MyStruct
                    {
                    }
                }
                """)],
                after: [],
                expected: new Dictionary<string, string>() {
                    { assemblyName, """
                    - namespace MyNamespace
                    - {
                    -     public struct MyStruct
                    -     {
                    -     }
                    - }
                    """ }
                });
    }
}
