// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.TypesShouldNotExtendCertainBaseTypesAnalyzer,
    Microsoft.NetFramework.CSharp.Analyzers.CSharpTypesShouldNotExtendCertainBaseTypesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.TypesShouldNotExtendCertainBaseTypesAnalyzer,
    Microsoft.NetFramework.VisualBasic.Analyzers.BasicTypesShouldNotExtendCertainBaseTypesFixer>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public class TypesShouldNotExtendCertainBaseTypesTests
    {
        [Fact]
        public async Task TypesShouldNotExtendCertainBaseTypes_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class C : Attribute
{
}
");
        }

        [Fact]
        public async Task TypesShouldNotExtendCertainBaseTypes_CSharp_ApplicationException()
        {
            var source = @"
using System;

public class C1 : ApplicationException
{
}
";
            DiagnosticResult[] expected = new[]
            {
                GetCSharpApplicationExceptionResultAt(4, 14, "C1", "System.ApplicationException")
            };

            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TypesShouldNotExtendCertainBaseTypes_CSharp_ApplicationException_Internal()
        {
            var source = @"
using System;

class C1 : ApplicationException
{
}
";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task TypesShouldNotExtendCertainBaseTypes_CSharp_XmlDocument()
        {
            var source = @"
using System.Xml;

public class C1 : XmlDocument
{
}
";
            DiagnosticResult[] expected = new[]
            {
                GetCSharpXmlDocumentResultAt(4, 14, "C1", "System.Xml.XmlDocument")
            };

            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TypesShouldNotExtendCertainBaseTypes_CSharp_XmlDocument_Internal()
        {
            var source = @"
using System.Xml;

class C1 : XmlDocument
{
}
";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task TypesShouldNotExtendCertainBaseTypes_CSharp_Collection()
        {
            var source = @"
using System.Collections;

public class C1 : CollectionBase
{
}

public class C2 : DictionaryBase
{
}

public class C3 : Queue
{
}

public class C4 : ReadOnlyCollectionBase
{
}

public class C5 : SortedList
{
}

public class C6 : Stack
{
}";
            DiagnosticResult[] expected = new[]
            {
                GetCSharpCollectionBaseResultAt(4, 14, "C1", "System.Collections.CollectionBase"),
                GetCSharpDictionaryBaseResultAt(8, 14, "C2", "System.Collections.DictionaryBase"),
                GetCSharpQueueResultAt(12, 14, "C3", "System.Collections.Queue"),
                GetCSharpReadOnlyCollectionResultAt(16, 14, "C4", "System.Collections.ReadOnlyCollectionBase"),
                GetCSharpSortedListResultAt(20, 14, "C5", "System.Collections.SortedList"),
                GetCSharpStackResultAt(24, 14, "C6", "System.Collections.Stack")
            };

            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TypesShouldNotExtendCertainBaseTypes_CSharp_Collection_Internal()
        {
            var source = @"
using System.Collections;

class C1 : CollectionBase
{
}

class C2 : DictionaryBase
{
}

class C3 : Queue
{
}

class C4 : ReadOnlyCollectionBase
{
}

internal class C5 : SortedList
{
}

public class C6
{
    private class Inner : Stack
    {
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task TypesShouldNotExtendCertainBaseTypes_Basic_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class Class2
    Inherits Attribute

End Class
");
        }

        [Fact]
        public async Task TypesShouldNotExtendCertainBaseTypes_Basic_ApplicationException()
        {
            var source = @"
Imports System

Public Class C1
    Inherits ApplicationException

End Class

";
            DiagnosticResult[] expected = new[]
            {
                GetBasicApplicationExceptionResultAt(4, 14, "C1", "System.ApplicationException")
            };

            await VerifyVB.VerifyAnalyzerAsync(source, expected);
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TypesShouldNotExtendCertainBaseTypes_Basic_ApplicationException_Internal()
        {
            var source = @"
Imports System

Friend Class C1
    Inherits ApplicationException

End Class

";

            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task TypesShouldNotExtendCertainBaseTypes_Basic_XmlDocument()
        {
            var source = @"
Imports System.Xml

Public Class C1
    Inherits XmlDocument

End Class
";
            DiagnosticResult[] expected = new[]
            {
                GetBasicXmlDocumentResultAt(4, 14, "C1", "System.Xml.XmlDocument")
            };

            await VerifyVB.VerifyAnalyzerAsync(source, expected);
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TypesShouldNotExtendCertainBaseTypes_Basic_XmlDocument_Internal()
        {
            var source = @"
Imports System.Xml

Friend Class C1
    Inherits XmlDocument

End Class
";

            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task TypesShouldNotExtendCertainBaseTypes_Basic_Collection()
        {
            var source = @"
Imports System.Collections

Public Class C1
    Inherits CollectionBase

End Class

Public Class C2
    Inherits DictionaryBase

End Class

Public Class C3
    Inherits Queue

End Class

Public Class C4
    Inherits ReadOnlyCollectionBase

End Class

Public Class C5
    Inherits SortedList

End Class

Public Class C6
    Inherits Stack

End Class
";
            DiagnosticResult[] expected = new[]
            {
                GetBasicCollectionBaseResultAt(4, 14, "C1", "System.Collections.CollectionBase"),
                GetBasicDictionaryBaseResultAt(9, 14, "C2", "System.Collections.DictionaryBase"),
                GetBasicQueueResultAt(14, 14, "C3", "System.Collections.Queue"),
                GetBasicReadOnlyCollectionBaseResultAt(19, 14, "C4", "System.Collections.ReadOnlyCollectionBase"),
                GetBasicSortedListResultAt(24, 14, "C5", "System.Collections.SortedList"),
                GetBasicStackResultAt(29, 14, "C6", "System.Collections.Stack")
            };

            await VerifyVB.VerifyAnalyzerAsync(source, expected);
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TypesShouldNotExtendCertainBaseTypes_Basic_Collection_Internal()
        {
            var source = @"
Imports System.Collections

Class C1
    Inherits CollectionBase

End Class

Class C2
    Inherits DictionaryBase

End Class

Class C3
    Inherits Queue

End Class

Class C4
    Inherits ReadOnlyCollectionBase

End Class

Friend Class C5
    Inherits SortedList

End Class

Public Class C6
    Private Class InnerClass
        Inherits Stack
    End Class
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        private static DiagnosticResult GetCSharpCollectionBaseResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsCollectionBase, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetBasicCollectionBaseResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsCollectionBase, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetCSharpDictionaryBaseResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsDictionaryBase, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetBasicDictionaryBaseResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsDictionaryBase, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetCSharpQueueResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsQueue, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetBasicQueueResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsQueue, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetCSharpReadOnlyCollectionResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsReadOnlyCollectionBase, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetBasicReadOnlyCollectionBaseResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsReadOnlyCollectionBase, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetCSharpSortedListResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsSortedList, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetBasicSortedListResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsSortedList, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetCSharpStackResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsStack, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetBasicStackResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsStack, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetCSharpApplicationExceptionResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemApplicationException, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetBasicApplicationExceptionResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemApplicationException, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetCSharpXmlDocumentResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemXmlXmlDocument, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }

        private static DiagnosticResult GetBasicXmlDocumentResultAt(int line, int column, string declaredTypeName, string badBaseTypeName)
        {
            string message = string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemXmlXmlDocument, declaredTypeName, badBaseTypeName);
#pragma warning disable RS0030 // Do not used banned APIs
            return VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(message);
#pragma warning restore RS0030 // Do not used banned APIs
        }
    }
}