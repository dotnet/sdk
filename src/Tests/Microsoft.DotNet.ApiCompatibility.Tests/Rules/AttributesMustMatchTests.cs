// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class AttributesMustMatchTests
    {
        /*
         * Tests for:
         * - Types
         * - Fields
         * - Properties
         * - Methods
         * - Events
         * - ReturnValues
         * - Constructors
         * - Generic Parameters
         * 
         * Grouped into:
         * - Type
         * - Member
         */
        [Fact]
        public static void EnsureDiagnosticIsReported()
        {
            // TODO: Interesting behavior from Roslyn. It attaches an attribute
            // to a class even if it is used inside it. This is why we have a newline
            // after the class declaration.
            string leftSyntax = @"
namespace CompatTests
{
  using System;
  using System.Runtime.InteropServices;
  internal class FooAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
  internal class BarAttribute : Attribute { }
  [Foo]
  [Bar]
  public class First {

    [DllImport(""user32.dll"", SetLastError=true, ExactSpelling=false)]
    public static extern void G();

    [Foo]
    [Bar]
    [Bar]
    public void F() {}
  }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  using System;
  using System.Runtime.InteropServices;
  internal class FooAttribute : Attribute { }
  internal class BarAttribute : Attribute { }
  [Bar]
  public class First {

    [DllImport(""user32.dll"", SetLastError=true, ExactSpelling=true)]
    public static extern void G();

    [Bar]
    public void F() {}
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });
            CompatDifference[] expected = new CompatDifference[] { };
            Assert.Equal(expected, differences);
        }
    }
}
