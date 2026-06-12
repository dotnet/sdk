// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffEventTests : DiffBaseTests
{
    [Fact]
    public Task EventAdd() => RunTestAsync(
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
                public event MyEventHandler? MyEvent;
            }
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
              public class MyClass
              {
        +         public event MyNamespace.MyClass.MyEventHandler? MyEvent { add; remove; }
              }
          }
        """);

    [Fact]
    public Task EventChange() => RunTestAsync(
        beforeCode: """
        using System;
        namespace MyNamespace
        {
            public class MyClass
            {
                public delegate void MyEventHandler(object sender, EventArgs e);
                public event MyEventHandler? MyEvent1;
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
                public event MyEventHandler? MyEvent2;
            }
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
              public class MyClass
              {
        -         public event MyNamespace.MyClass.MyEventHandler? MyEvent1 { add; remove; }
        +         public event MyNamespace.MyClass.MyEventHandler? MyEvent2 { add; remove; }
              }
          }
        """);

    [Fact]
    public Task EventRemove() => RunTestAsync(
        beforeCode: """
        using System;
        namespace MyNamespace
        {
            public class MyClass
            {
                public delegate void MyEventHandler(object sender, EventArgs e);
                public event MyEventHandler? MyEvent;
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
            }
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
              public class MyClass
              {
        -         public event MyNamespace.MyClass.MyEventHandler? MyEvent { add; remove; }
              }
          }
        """);

    [Fact]
    public Task AbstractEvent() => RunTestAsync(
        beforeCode: """
        using System;
        namespace MyNamespace
        {
            public abstract class MyClass
            {
                public delegate void MyEventHandler(object sender, EventArgs e);
            }
        }
        """,
        afterCode: """
        using System;
        namespace MyNamespace
        {
            public abstract class MyClass
            {
                public delegate void MyEventHandler(object sender, EventArgs e);
                public abstract event MyEventHandler MyEvent;
            }
        }
        """,
        expectedCode: """
          namespace MyNamespace
          {
              public abstract class MyClass
              {
        +         public abstract event MyNamespace.MyClass.MyEventHandler MyEvent;
              }
          }
        """);
}
