// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffEventsTests : DiffBaseTests
{
    [Fact]
    public Task TestEventAdd() => RunTestAsync(
        beforeCode: """
        using System;
        namespace MyNamespace
        {
            public class MyClass
            {
                public delegate void MyEventHandler(object sender, EventArgs e);
            }
        }
        """,
        afterCode: """
        using System;
        namespace MyNamespace
        {
            public class MyClass
            {
                public delegate void MyEventHandler(object sender, EventArgs e);
                public event MyEventHandler? MyEvent { add { } remove { } }
            }
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
              public class MyClass
              {
        +         public event MyEventHandler? MyEvent { add { } remove { } }
              }
          }
        """);

}
