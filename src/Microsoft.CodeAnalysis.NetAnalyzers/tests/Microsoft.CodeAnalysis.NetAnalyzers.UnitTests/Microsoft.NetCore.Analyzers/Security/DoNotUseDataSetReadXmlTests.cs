// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseDataSetReadXml,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotUseDataSetReadXmlTests
    {
        [Fact]
        public async Task ReadXml_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Data;

namespace Blah
{
    public class Program
    {
        public void Unsafe(Stream s)
        {
            DataSet dataSet = new DataSet();
            dataSet.ReadXml(s);
        }
    }
}",
                GetCSharpResultAt(12, 13, "XmlReadMode DataSet.ReadXml(Stream stream)"));
        }

        [Fact]
        public async Task DerivedReadXml_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Data;

namespace Blah
{
    public class Program
    {
        public void Unsafe(string s)
        {
            MyDataSet dataSet = new MyDataSet();
            dataSet.ReadXml(s);
        }
    }

    public class MyDataSet : DataSet
    {
    }
}",
                GetCSharpResultAt(12, 13, "XmlReadMode DataSet.ReadXml(string fileName)"));
        }

        [Fact]
        public async Task DerivedReadXmlEvenWithReadXmlSchema_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Data;

namespace Blah
{
    public class Program
    {
        public void Unsafe(string s)
        {
            MyDataSet dataSet = new MyDataSet();
            dataSet.ReadXmlSchema("""");
            dataSet.ReadXml(s);
        }
    }

    public class MyDataSet : DataSet
    {
    }
}",
                GetCSharpResultAt(13, 13, "XmlReadMode DataSet.ReadXml(string fileName)"));
        }

        [Fact]
        public async Task RejectChanges_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Data;

namespace Blah
{
    public class Program
    {
        public void Safe(Stream s)
        {
            DataSet dataSet = new DataSet();
            dataSet.RejectChanges();
        }
    }
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
            => VerifyCS.Diagnostic(DoNotUseDataSetReadXml.RealMethodUsedDescriptor)
                .WithLocation(line, column)
                .WithArguments(arguments);
    }
}
