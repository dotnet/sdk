// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Data.ReviewSqlQueriesForSecurityVulnerabilities,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Data.ReviewSqlQueriesForSecurityVulnerabilities,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Data.UnitTests
{
    public class ReviewSQLQueriesForSecurityVulnerabilitiesTests
    {
        private static DiagnosticResult GetCSharpResultAt(int line, int column, string invokedSymbol, string containingMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(invokedSymbol, containingMethod);

        private static DiagnosticResult GetBasicResultAt(int line, int column, string invokedSymbol, string containingMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(invokedSymbol, containingMethod);

        protected const string SetupCodeCSharp = @"
using System.Data;

class Command : IDbCommand
{
    public virtual string CommandText { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
    public int CommandTimeout { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
    public CommandType CommandType { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
    public IDbConnection Connection { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

    public IDataParameterCollection Parameters => throw new System.NotImplementedException();

    public IDbTransaction Transaction { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
    public UpdateRowSource UpdatedRowSource { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

    public void Cancel()
    {
        throw new System.NotImplementedException();
    }

    public IDbDataParameter CreateParameter()
    {
        throw new System.NotImplementedException();
    }

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }

    public int ExecuteNonQuery()
    {
        throw new System.NotImplementedException();
    }

    public IDataReader ExecuteReader()
    {
        throw new System.NotImplementedException();
    }

    public IDataReader ExecuteReader(CommandBehavior behavior)
    {
        throw new System.NotImplementedException();
    }

    public object ExecuteScalar()
    {
        throw new System.NotImplementedException();
    }

    public void Prepare()
    {
        throw new System.NotImplementedException();
    }
}

class Adapter : IDataAdapter
{
    public MissingMappingAction MissingMappingAction { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
    public MissingSchemaAction MissingSchemaAction { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

    public ITableMappingCollection TableMappings => throw new System.NotImplementedException();

    public int Fill(DataSet dataSet)
    {
        throw new System.NotImplementedException();
    }

    public DataTable[] FillSchema(DataSet dataSet, SchemaType schemaType)
    {
        throw new System.NotImplementedException();
    }

    public IDataParameter[] GetFillParameters()
    {
        throw new System.NotImplementedException();
    }

    public int Update(DataSet dataSet)
    {
        throw new System.NotImplementedException();
    }
}";

        protected const string SetupCodeBasic = @"
Imports System
Imports System.Data

Class Command
    Implements IDbCommand
    Public Overridable Property CommandText As String Implements IDbCommand.CommandText
        Get
            Throw New NotImplementedException()
        End Get
        Set(value As String)
            Throw New NotImplementedException()
        End Set
    End Property
    Public Property CommandTimeout As Integer Implements IDbCommand.CommandTimeout
        Get
            Throw New NotImplementedException()
        End Get
        Set(value As Integer)
            Throw New NotImplementedException()
        End Set
    End Property
    Public Property CommandType As CommandType Implements IDbCommand.CommandType
        Get
            Throw New NotImplementedException()
        End Get
        Set(value As CommandType)
            Throw New NotImplementedException()
        End Set
    End Property
    Public Property Connection As IDbConnection Implements IDbCommand.Connection
        Get
            Throw New NotImplementedException()
        End Get
        Set(value As IDbConnection)
            Throw New NotImplementedException()
        End Set
    End Property
    Public ReadOnly Property Parameters As IDataParameterCollection Implements IDbCommand.Parameters
        Get
            Throw New NotImplementedException()
        End Get
    End Property
    Public Property Transaction As IDbTransaction Implements IDbCommand.Transaction
        Get
            Throw New NotImplementedException()
        End Get
        Set(value As IDbTransaction)
            Throw New NotImplementedException()
        End Set
    End Property
    Public Property UpdatedRowSource As UpdateRowSource Implements IDbCommand.UpdatedRowSource
        Get
            Throw New NotImplementedException()
        End Get
        Set(value As UpdateRowSource)
            Throw New NotImplementedException()
        End Set
    End Property
    Public Sub Cancel() Implements IDbCommand.Cancel
        Throw New NotImplementedException()
    End Sub
    Public Sub Prepare() Implements IDbCommand.Prepare
        Throw New NotImplementedException()
    End Sub
    Public Sub Dispose() Implements IDisposable.Dispose
        Throw New NotImplementedException()
    End Sub
    Public Function CreateParameter() As IDbDataParameter Implements IDbCommand.CreateParameter
        Throw New NotImplementedException()
    End Function
    Public Function ExecuteNonQuery() As Integer Implements IDbCommand.ExecuteNonQuery
        Throw New NotImplementedException()
    End Function
    Public Function ExecuteReader() As IDataReader Implements IDbCommand.ExecuteReader
        Throw New NotImplementedException()
    End Function
    Public Function ExecuteReader(behavior As CommandBehavior) As IDataReader Implements IDbCommand.ExecuteReader
        Throw New NotImplementedException()
    End Function
    Public Function ExecuteScalar() As Object Implements IDbCommand.ExecuteScalar
        Throw New NotImplementedException()
    End Function
End Class

Class Adapter
    Implements IDataAdapter
    Public Property MissingMappingAction As MissingMappingAction Implements IDataAdapter.MissingMappingAction
        Get
            Throw New NotImplementedException()
        End Get
        Set(value As MissingMappingAction)
            Throw New NotImplementedException()
        End Set
    End Property
    Public Property MissingSchemaAction As MissingSchemaAction Implements IDataAdapter.MissingSchemaAction
        Get
            Throw New NotImplementedException()
        End Get
        Set(value As MissingSchemaAction)
            Throw New NotImplementedException()
        End Set
    End Property
    Public ReadOnly Property TableMappings As ITableMappingCollection Implements IDataAdapter.TableMappings
        Get
            Throw New NotImplementedException()
        End Get
    End Property
    Public Function Fill(dataSet As DataSet) As Integer Implements IDataAdapter.Fill
        Throw New NotImplementedException()
    End Function
    Public Function FillSchema(dataSet As DataSet, schemaType As SchemaType) As DataTable() Implements IDataAdapter.FillSchema
        Throw New NotImplementedException()
    End Function
    Public Function GetFillParameters() As IDataParameter() Implements IDataAdapter.GetFillParameters
        Throw New NotImplementedException()
    End Function
    Public Function Update(dataSet As DataSet) As Integer Implements IDataAdapter.Update
        Throw New NotImplementedException()
    End Function
End Class";

        [Fact]
        public async Task Unrelated_ConstructorParameter_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
class Test
{{
    public Test(string test) {{ }}

    public static void M1(string param)
    {{
        var str = param;
        var t = new Test(str);
    }}
}}");
        }

        [Fact]
        public async Task DbCommand_CommandText_StringLiteral_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Test
{{
    void M1()
    {{
        Command c = new Command();
        c.CommandText = ""asdf"";
    }}
}}");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Module Test
    Sub M1()
        Dim c as New Command()
        c.CommandText = ""asdf""
    End Sub
End Module");
        }

        [Fact]
        public async Task DbCommand_ConstructorParameter_StringLiteral_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string command)
    {{
    }}
}}

class Test
{{
    void M1()
    {{
        Command c = new Command1(""asdf"");
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(command As String)
    End Sub
End Class

Module Test
    Sub M1()
        Dim c as New Command1(""asdf"")
    End Sub
End Module");

        }

        [Fact]
        public async Task DataAdapter_ConstructorParameter_StringLiteral_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string command)
    {{
    }}
}}

class Test
{{
    void M1()
    {{
        Adapter a = new Adapter1(""asdf"");
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(command As String)
    End Sub
End Class

Module Test
    Sub M1()
        Dim a as New Adapter1(""asdf"")
    End Sub
End Module");
        }

        [Fact]
        public async Task DbCommand_CommandText_ClassConstant_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Test
{{
    const string str = """";

    void M1()
    {{
        Command c = new Command();
        c.CommandText = str;
    }}
}}");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Module Test
    Const str As String = ""asdf""
    Sub M1()
        Dim c as New Command()
        c.CommandText = str
    End Sub
End Module");
        }

        [Fact]
        public async Task DbCommand_ConstructorParameter_ClassConstant_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string command)
    {{
    }}
}}

class Test
{{
    const string str = """";

    void M1()
    {{
        Command c = new Command1(str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(command As String)
    End Sub
End Class

Module Test
    Const str As String = ""asdf""
    Sub M1()
        Dim c as New Command1(str)
    End Sub
End Module");
        }

        [Fact]
        public async Task DataAdapter_ConstructorParameter_ClassConstant_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string command)
    {{
    }}
}}

class Test
{{
    const string str = ""asdf"";
    void M1()
    {{
        Adapter a = new Adapter1(str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(command As String)
    End Sub
End Class

Module Test
    Const str As String = ""asdf""

    Sub M1()
        Dim a as New Adapter1(str)
    End Sub
End Module");
        }

        [Fact]
        public async Task DbCommand_ConstructorParameter_CallingAnotherConstructor_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    static string static_str = """";
    public Command1(string command, string cmd)
    {{
    }}
    public Command1(string parameter) : this(parameter, static_str)
    {{
    }}
}}

class Test
{{
    void M1()
    {{
        Command c = new Command1("""");
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(command As String, cmd As String)
    End Sub

    Sub New(command As String)
        Me.New(command, command)
    End Sub
End Class

Module Test
    Const str As String = ""asdf""
    Sub M1()
        Dim c as New Command1(str)
    End Sub
End Module");
        }

        [Fact]
        public async Task DataAdapter_ConstructorParameter_CallingAnotherConstructor_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string command, string cmd)
    {{}}
    public Adapter1(string command) : this (command, command)
    {{
    }}
}}

class Test
{{
    const string str = ""asdf"";
    void M1()
    {{
        Adapter a = new Adapter1(str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(command As String, cmd As String)
    End Sub
    Sub New(command As String)
        Me.New(command, command)
    End Sub
End Class

Module Test
    Const str As String = ""asdf""

    Sub M1()
        Dim a as New Adapter1(str)
    End Sub
End Module");
        }

        [Fact]
        public async Task DbCommand_BaseConstructor_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Command2 : Command1
{{
    public Command2(string cmd) : base(cmd, cmd) {{ }}
}}

class Test
{{
    void M1()
    {{
        string str = """";
        Command c = new Command2("""");
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(command As String)
    End Sub
End Class

Class Command2
    Inherits Command1

    Sub New(command As String)
        MyBase.New(command)
    End Sub
End Class

Module Test
    Const str As String = ""asdf""
    Sub M1()
        Dim c as New Command2(str)
    End Sub
End Module");
        }

        [Fact]
        public async Task DataAdapter_BaseConstructor_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string command)
    {{
    }}
}}

class Adapter2 : Adapter1
{{
    public Adapter2(string command) : base(command) {{ }}
}}

class Test
{{
    const string str = ""asdf"";
    void M1()
    {{
        Adapter a = new Adapter2(str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(command As String)
    End Sub
End Class

Class Adapter2
    Inherits Adapter1

    Sub New(command As String)
        MyBase.New(command)
    End Sub
End Class

Module Test
    Const str As String = ""asdf""

    Sub M1()
        Dim a as New Adapter2(str)
    End Sub
End Module");
        }

        [Fact]
        public async Task DbCommand_PropertyAssignment_NotCommandText_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public string NotCommandText {{ get; set; }}
}}
class Test
{{
    void M1(string param)
    {{
        string str = param;
        var c = new Command1();
        c.NotCommandText = str;
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Public Property NotCommandText As String
End Class

Module Test
    Const str As String = ""asdf""
    Sub M1(param As String)
        Dim str As String = param
        Dim c As New Command1()
        c.NotCommandText = str
    End Sub
End Module");
        }

        [Fact]
        public async Task DbCommand_CommandTextUsage_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Test
{{
    void M1()
    {{
        var c = new Command();
        string commandText = c.CommandText;
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Module Test
    Sub M1()
        Dim c As New Command()
        Dim str As String = c.CommandText
    End Sub
End Module");
        }

        [Fact]
        public async Task DbCommand_CommandTextUsage_InClass_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    void M2()
    {{
        string str;
        str = CommandText;
    }}
}}
class Test
{{
    void M1()
    {{
        var c = new Command1();
        string commandText = c.CommandText;
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub M2()
        Dim str As String
        str = CommandText
    End Sub
End Class

Module Test
    Sub M1()
        Dim c As New Command1()
        Dim commandText As String = c.CommandText
    End Sub
End Module");
        }

        [Fact]
        public async Task DbCommand_OtherMethodInvocation_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public void M2(string cmd)
    {{
    }}
}}
class Test
{{
    void M1(string param)
    {{
        var c = new Command1();
        string str = param;
        c.M2(str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub M2(cmd As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim c As New Command1()
        c.M2(str)
    End Sub
End Module");

        }

        [Fact]
        public async Task DataAdapter_OtherMethodInvocation_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public void M2(string cmd)
    {{
    }}
}}
class Test
{{
    void M1(string param)
    {{
        var a = new Adapter1();
        string str = param;
        a.M2(str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub M2(cmd As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim a As New Adapter1()
        a.M2(str)
    End Sub
End Module");
        }

        [Fact]
        public async Task DataAdapter_SingleConstructorParameter_NotCmdOrCommand()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string parameter)
    {{
    }}
}}
class Test
{{
    void M1(string param)
    {{
        string str = param;
        var a = new Adapter1(str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub M2(cmd As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim a As New Adapter1()
        a.M2(str)
    End Sub
End Module");
        }

        [Fact]
        public async Task DbCommand_ConstructorParameter_MultipleParameters_NeitherNamedCommandOrCmd_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string parameter1, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = param;
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(parameter1 As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim c As New Command1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task DataAdapter_ConstructorParameter_MultipleParameters_NeitherNamedCommandOrCmd_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string parameter1, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = param;
        Adapter1 c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(parameter1 As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim c As New Adapter1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task DbCommand_ConstructorParameter_MultipleParameters_OneNamedCmd_WithStringLiteral_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = param;
        Command c = new Command1("""", str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim c As New Command1("""", str)
    End Sub
End Module");
        }

        [Fact]
        public async Task DataAdapter_ConstructorParameter_MultipleParameters_OneNamedCmd_WithStringLiteral_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = param;
        var a = new Adapter1("""", str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim a As New Adapter1("""", str)
    End Sub
End Module");
        }

        [Fact]
        public async Task DbCommand_CommandText_LocalVariable_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Test
{{
    void M1(string param)
    {{
        Command c = new Command();
        var str = param;
        c.CommandText = str;
    }}
}}",
            GetCSharpResultAt(92, 9, "string Command.CommandText", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Module Test
    Sub M1(param As String)
        Dim c As New Command()
        Dim str As String = param
        c.CommandText = str
    End Sub
End Module",
            GetBasicResultAt(128, 9, "Property Command.CommandText As String", "M1"));
        }

        [Fact]
        public async Task AutoGeneratedCode_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
// <auto-generated/>
{SetupCodeCSharp}

class Test
{{
    void M1(string param)
    {{
        Command c = new Command();
        var str = param;
        c.CommandText = str;
    }}
}}",
            GetCSharpResultAt(93, 9, "string Command.CommandText", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
' <auto-generated/>
{SetupCodeBasic}

Module Test
    Sub M1(param As String)
        Dim c As New Command()
        Dim str As String = param
        c.CommandText = str
    End Sub
End Module",
            GetBasicResultAt(129, 9, "Property Command.CommandText As String", "M1"));
        }

        [Fact]
        public async Task DbCommand_ConstructorParameter_LocalVariable_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string parameter)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        var str = param;
        Command c = new Command1(str);
    }}
}}
",
            GetCSharpResultAt(98, 21, "Command1.Command1(string parameter)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command
    Sub New(parameter As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim c As New Command1(str)
    End Sub
End Module",
            GetBasicResultAt(133, 18, "Sub Command1.New(parameter As String)", "M1"));
        }

        [Fact]
        public async Task DataAdapter_ConstructorParameter_LocalVariableName_Diagnostic()
        {
            // Constructor Parameter named command
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string command)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        var str = param;
        var c = new Adapter1(str);
    }}
}}
",
            GetCSharpResultAt(98, 17, "Adapter1.Adapter1(string command)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter
    Sub New(command As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim a As New Adapter1(str)
    End Sub
End Module",
            GetBasicResultAt(133, 18, "Sub Adapter1.New(command As String)", "M1"));

            // Constructor parameter named cmd
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        var str = param;
        var c = new Adapter1(str);
    }}
}}
",
            GetCSharpResultAt(98, 17, "Adapter1.Adapter1(string cmd)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter
    Sub New(cmd As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim a As New Adapter1(str)
    End Sub
End Module",
            GetBasicResultAt(133, 18, "Sub Adapter1.New(cmd As String)", "M1"));
        }

        [Fact]
        public async Task DbCommand_CommandText_Parameter_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Test
{{
    void M1(string str)
    {{
        Command c = new Command();
        c.CommandText = str;
    }}
}}",
            GetCSharpResultAt(91, 9, "string Command.CommandText", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Module Test
    Sub M1(str As String)
        Dim c As New Command()
        c.CommandText = str
    End Sub
End Module",
            GetBasicResultAt(127, 9, "Property Command.CommandText As String", "M1"));

        }

        [Fact, WorkItem(1625, "https://github.com/dotnet/roslyn-analyzers/issues/1625")]
        public async Task DbCommand_CommandText_PropertyOverride_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public override string CommandText {{ get; set; }}
}}

class Test
{{
    void M1(string str)
    {{
        Command1 c = new Command1();
        c.CommandText = str;
    }}
}}",
            // Test0.cs(96,9): warning CA2100: Review if the query string passed to 'string Command1.CommandText' in 'M1', accepts any user input.
            GetCSharpResultAt(96, 9, "string Command1.CommandText", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Public Overrides Property CommandText As String
End Class

Module Test
    Sub M1(str As String)
        Dim c As New Command1()
        c.CommandText = str
    End Sub
End Module",
            // Test0.vb(133,9): warning CA2100: Review if the query string passed to 'Property Command1.CommandText As String' in 'M1', accepts any user input.
            GetBasicResultAt(133, 9, "Property Command1.CommandText As String", "M1"));
        }

        [Fact]
        public async Task DbCommand_ConstructorParameter_Parameter_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string parameter)
    {{
    }}
}}

class Test
{{
    void M1(string str)
    {{
        Command c = new Command1(str);
    }}
}}
",
            GetCSharpResultAt(97, 21, "Command1.Command1(string parameter)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command
    Sub New(parameter As String)
    End Sub
End Class

Module Test
    Sub M1(str As String)
        Dim c As New Command1(str)
    End Sub
End Module",
            GetBasicResultAt(132, 18, "Sub Command1.New(parameter As String)", "M1"));
        }

        [Fact]
        public async Task DataAdapter_ConstructorParameter_Parameter_Diagnostic()
        {
            // Constructor parameter named command
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string command)
    {{
    }}
}}

class Test
{{
    void M1(string str)
    {{
        Adapter a = new Adapter1(str);
    }}
}}
",
            GetCSharpResultAt(97, 21, "Adapter1.Adapter1(string command)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter
    Sub New(command As String)
    End Sub
End Class

Module Test
    Sub M1(str As String)
        Dim a As New Adapter1(str)
    End Sub
End Module",
            GetBasicResultAt(132, 18, "Sub Adapter1.New(command As String)", "M1"));

            // Constructor parameter named cmd

            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cMd)
    {{
    }}
}}

class Test
{{
    void M1(string str)
    {{
        Adapter a = new Adapter1(str);
    }}
}}
",
            GetCSharpResultAt(97, 21, "Adapter1.Adapter1(string cMd)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter
    Sub New(cmD As String)
    End Sub
End Class

Module Test
    Sub M1(str As String)
        Dim a As New Adapter1(str)
    End Sub
End Module",
            GetBasicResultAt(132, 18, "Sub Adapter1.New(cmD As String)", "M1"));
        }

        [Fact]
        public async Task DbCommand_ConstructorParameter_MultipleParameters_OneNamedCmd_WithLocal_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = param;
        Command c = new Command1(str, str);
    }}
}}
",
            GetCSharpResultAt(98, 21, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim c As New Command1(str, str)
    End Sub
End Module",
            GetBasicResultAt(134, 18, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task DataAdapter_ConstructorParameter_MultipleParameters_OneNamedCmd_WithLocal_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = param;
        Adapter c = new Adapter1(str, str);
    }}
}}
",
            GetCSharpResultAt(98, 21, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim c As New Adapter1(str, str)
    End Sub
End Module",
            GetBasicResultAt(134, 18, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task DbCommand_ConstructorParameter_MultipleParameters_NonConstants_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string command)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = param;
        Command c = new Command1(str, str);
    }}
}}
",
            GetCSharpResultAt(98, 21, "Command1.Command1(string cmd, string command)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, command As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim c As New Command1(str, str)
    End Sub
End Module",
            GetBasicResultAt(134, 18, "Sub Command1.New(cmd As String, command As String)", "M1"));
        }

        [Fact]
        public async Task DataAdapter_ConstructorParameter_MultipleParameters_NonConstants_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string command)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = param;
        Adapter c = new Adapter1(str, str);
    }}
}}
",
            GetCSharpResultAt(98, 21, "Adapter1.Adapter1(string cmd, string command)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, command As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = param
        Dim c As New Adapter1(str, str)
    End Sub
End Module",
            GetBasicResultAt(134, 18, "Sub Adapter1.New(cmd As String, command As String)", "M1"));
        }

        [Fact]
        public async Task MissingWellKnownTypes_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class C { }");

            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
End Class");
        }

        [Fact]
        public async Task HttpRequest_Form_LocalString_Diagnostic()
        {
            var source = $@"
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

{SetupCodeCSharp}

    public partial class WebForm : System.Web.UI.Page
    {{
        protected void Page_Load(object sender, EventArgs e)
        {{
            string input = Request.Form[""in""];
            if (Request.Form != null && !String.IsNullOrWhiteSpace(input))
            {{
                Command sqlCommand = new Command()
                {{
                    CommandText = input,
                    CommandType = CommandType.Text,
                }};
            }}
        }}
     }}
            ";

            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSystemWeb,
                TestState =
                {
                    Sources = { source },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(102, 21, "string Command.CommandText", "Page_Load")
                    }
                }
            }.RunAsync();
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = M1")]
        [InlineData("dotnet_code_quality.CA2100.excluded_symbol_names = M1")]
        [InlineData("dotnet_code_quality.dataflow.excluded_symbol_names = M1")]
        [InlineData("dotnet_code_quality.CA2100.excluded_symbol_names = M*")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOption(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
{SetupCodeCSharp}

class Test
{{
    void M1(string param)
    {{
        Command c = new Command();
        var str = param;
        c.CommandText = str;
    }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };

            if (editorConfigText.Length == 0)
            {
                csharpTest.ExpectedDiagnostics.Add(
                    GetCSharpResultAt(92, 9, "string Command.CommandText", "M1")
                );
            }

            await csharpTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
{SetupCodeBasic}

Module Test
    Sub M1(param As String)
        Dim c As New Command()
        Dim str As String = param
        c.CommandText = str
    End Sub
End Module"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };

            if (editorConfigText.Length == 0)
            {
                vbTest.ExpectedDiagnostics.Add(
                    GetBasicResultAt(128, 9, "Property Command.CommandText As String", "M1")
                );
            }

            await vbTest.RunAsync();
        }

        [Fact, WorkItem(3613, "https://github.com/dotnet/roslyn-analyzers/issues/3613")]
        public async Task GlobalAssemblyAttributes_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(""Category"", ""Id"")]");

            await VerifyVB.VerifyAnalyzerAsync(@"
<assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(""Category"", ""Id"")>");
        }
    }
}
