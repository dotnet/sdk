// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpMakeTypesInternal,
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpMakeTypesInternalFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability.BasicMakeTypesInternal,
    Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability.BasicMakeTypesInternalFixer>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    public sealed class MakeTypesInternalTests
    {
        [Theory]
        [MemberData(nameof(NonDiagnosticTriggeringOutputKinds))]
        public async Task LibraryCode_NoDiagnostic(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind, "public class MyService {}");

            await VerifyVbAsync(outputKind, "Public Class MyService\nEnd Class");
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task ApplicationCode_Diagnostic(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind, @"
public class [|Program|]
{
    public static void Main() {}
}",
                @"
internal class Program
{
    public static void Main() {}
}");

            await VerifyVbAsync(outputKind, @"
Public Class [|Program|]
    Public Shared Sub Main()
    End Sub
End Class",
                @"
Friend Class Program
    Public Shared Sub Main()
    End Sub
End Class");
        }

        [Theory]
        [MemberData(nameof(NonDiagnosticTriggeringOutputKinds))]
        public async Task MultipleTypes_LibraryCode_NoDiagnostic(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind, @"
public class MyTests {}

internal class MyService {}

public class MyValidator {}");

            await VerifyVbAsync(outputKind, @"
Public Class MyTests
End Class

Friend Class MyService
End Class

Public Class MyValidator
End Class");
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task MultipleTypes_ApplicationCode_Diagnostic(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind, @"
public class [|Program|]
{
    public static void Main() {}
}

internal class MyService {}

public class [|MyValidator|] {}",
                @"
internal class Program
{
    public static void Main() {}
}

internal class MyService {}

internal class MyValidator {}");
            await VerifyVbAsync(outputKind, @"
Public Class [|Program|]
    Public Shared Sub Main()
    End Sub
End Class

Friend Class MyService
End Class

Public Class [|MyValidator|]
End Class",
                @"
Friend Class Program
    Public Shared Sub Main()
    End Sub
End Class

Friend Class MyService
End Class

Friend Class MyValidator
End Class");
        }

        [Theory]
        [MemberData(nameof(NonDiagnosticTriggeringOutputKinds))]
        public async Task MultipleDifferentTypes_LibraryCode_NoDiagnostic(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind, @"
public class MyTests {}

public abstract class MyBaseType {}

public struct MyValueType {}

public interface IValidator {}

public enum Types {}");

            await VerifyVbAsync(outputKind, @"
Public Class MyTests
End Class

Public MustInherit Class MyBaseType
End Class 

Public Structure MyValueType
End Structure

Public Interface IValidator
End Interface

Public Enum Types
    None
End Enum");
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task MultipleDifferentTypes_ApplicationCode_Diagnostic(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind, @"
public class [|Program|]
{
    public static void Main() {}
}

public abstract class [|MyBaseType|] {}

public struct [|MyValueType|] {}

public interface [|IValidator|] {}

public record [|Person|];

public enum [|Types|] {}",
                @"
internal class Program
{
    public static void Main() {}
}

internal abstract class MyBaseType {}

internal struct MyValueType {}

internal interface IValidator {}

internal record Person;

internal enum Types {}");

            await VerifyVbAsync(outputKind, @"
Public Class [|Program|]
    Public Shared Sub Main()
    End Sub
End Class

Public MustInherit Class [|MyBaseType|]
End Class

Public Structure [|MyValueType|]
End Structure

Public Interface [|IValidator|]
End Interface

Public Enum [|Types|]
    None
End Enum",
                @"
Friend Class Program
    Public Shared Sub Main()
    End Sub
End Class

Friend MustInherit Class MyBaseType
End Class

Friend Structure MyValueType
End Structure

Friend Interface IValidator
End Interface

Friend Enum Types
    None
End Enum");
        }

        [Theory]
        [InlineData(OutputKind.DynamicallyLinkedLibrary)]
        [InlineData(OutputKind.WindowsRuntimeMetadata)]
        public Task Records_LibraryCode_NoDiagnostic(OutputKind outputKind)
        {
            return VerifyCsAsync(outputKind, @"public record Person;");
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public Task Records_ApplicationCode_Diagnostic(OutputKind outputKind)
        {
            return VerifyCsAsync(outputKind, @"
public class [|Program|]
{
    public static void Main() {}
}

public record [|Person|];",
                @"
internal class Program
{
    public static void Main() {}
}

internal record Person;");
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task NoModifier_ApplicationCode_NoDiagnostic(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind, @"
class Program
{
    public static void Main() {}
}

class MyService {}

sealed class MyValidator {}");
            await VerifyVbAsync(outputKind, @"
Class Program
    Public Shared Sub Main()
    End Sub
End Class

Class MyService
End Class

NotInheritable Class MyValidator
End Class");
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task MultipleModifiers_ApplicationCode_Diagnostic(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind, @"
public class [|Program|]
{
    public static void Main() {}
}

public sealed class [|MyService|] {}

public abstract partial class [|MyValidator|] {}

public partial interface [|IValidator|] {}",
                @"
internal class Program
{
    public static void Main() {}
}

internal sealed class MyService {}

internal abstract partial class MyValidator {}

internal partial interface IValidator {}");
            await VerifyVbAsync(outputKind, @"
Public Class [|Program|]
    Public Shared Sub Main()
    End Sub
End Class

Public NotInheritable Class [|MyService|]
End Class

Public Partial MustInherit Class [|MyValidator|]
End Class

Public Partial Interface [|IValidator|]
End Interface",
                @"
Friend Class Program
    Public Shared Sub Main()
    End Sub
End Class

Friend NotInheritable Class MyService
End Class

Friend Partial MustInherit Class MyValidator
End Class

Friend Partial Interface IValidator
End Interface");
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task MultipleUnorderedModifiers_ApplicationCode_Diagnostic(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind, @"
public class [|Program|]
{
    public static void Main() {}
}

sealed public class [|MyService|] {}",
                @"
internal class Program
{
    public static void Main() {}
}

sealed internal class MyService {}");
            await VerifyVbAsync(outputKind, @"
Public Class [|Program|]
    Public Shared Sub Main()
    End Sub
End Class

NotInheritable Public Class [|MyService|]
End Class",
                @"
Friend Class Program
    Public Shared Sub Main()
    End Sub
End Class

NotInheritable Friend Class MyService
End Class");
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task NestedTypes_ApplicationCode_NoDiagnostic(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind, @"
class Program
{
    public static void Main() {}

    public struct [|MyValueType|]
    {
        public class [|Nested|] {}
    }
}",
                @"
class Program
{
    public static void Main() {}

    internal struct MyValueType
    {
        internal class Nested {}
    }
}");
            await VerifyVbAsync(outputKind, @"
Class Program
    Public Shared Sub Main()
    End Sub

    Public Structure [|MyValueType|]
        Public Class [|Nested|]
        End Class
    End Structure
End Class",
                @"
Class Program
    Public Shared Sub Main()
    End Sub

    Friend Structure MyValueType
        Friend Class Nested
        End Class
    End Structure
End Class");
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task ProtectedTypes_ApplicationCode_NoDiagnostic(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind, @"
class Program
{
    public static void Main() {}

    protected abstract class MyService {}
}");
            await VerifyVbAsync(outputKind, @"
Class Program
    Public Shared Sub Main()
    End Sub

    Protected MustInherit Class MyService
    End Class
End Class");
        }

        [Theory]
        [MemberData(nameof(NonDiagnosticTriggeringOutputKinds))]
        public async Task Delegates_LibraryCode_NoDiagnostic(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind, @"
public delegate int GetValue(string s);
public delegate void Print(object x);
public delegate bool Match();
");

            await VerifyVbAsync(outputKind, @"
Imports System

Public Delegate Function GetValue(s As String) As Int32
Public Delegate Sub Print(x As Object)
Public Delegate Function Match() As Boolean
");
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task Delegates_ApplicationCode_Diagnostic(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind, @"
public delegate int [|GetValue|](string s);
public delegate void [|Print|](object x);
public delegate bool [|Match|]();
internal delegate char GetFirst(string s);

class Program {
    public static void Main() {}
}",
                @"
internal delegate int GetValue(string s);
internal delegate void Print(object x);
internal delegate bool Match();
internal delegate char GetFirst(string s);

class Program {
    public static void Main() {}
}");

            await VerifyVbAsync(outputKind, @"
Imports System

Public Delegate Function [|GetValue|](s As String) As Int32
Public Delegate Sub [|Print|](x As Object)
Public Delegate Function [|Match|]() As Boolean
Friend Delegate Function GetFirst(s As String) As Char

Class Program
    Public Shared Sub Main()
    End Sub
End Class",
                @"
Imports System

Friend Delegate Function GetValue(s As String) As Int32
Friend Delegate Sub Print(x As Object)
Friend Delegate Function Match() As Boolean
Friend Delegate Function GetFirst(s As String) As Char

Class Program
    Public Shared Sub Main()
    End Sub
End Class");
        }

        [Theory]
        [InlineData("DynamicallyLinkedLibrary", OutputKind.DynamicallyLinkedLibrary)]
        [InlineData("ConsoleApplication", OutputKind.ConsoleApplication)]
        [InlineData("ConsoleApplication,DynamicallyLinkedLibrary", OutputKind.DynamicallyLinkedLibrary)]
        [InlineData("WindowsApplication", OutputKind.WindowsApplication)]
        [InlineData("WindowsRuntimeApplication", OutputKind.WindowsRuntimeApplication)]
        [InlineData("ConsoleApplication,WindowsApplication", OutputKind.WindowsApplication)]
        [InlineData("ConsoleApplication,WindowsApplication,WindowsRuntimeApplication", OutputKind.WindowsRuntimeApplication)]
        public async Task CompilationOptions_Diagnostic(string optionsText, OutputKind outputKind)
        {
            await new VerifyCS.Test
            {
                TestCode = """
                           public class [|Program|]
                           {
                               public static void Main() {}
                           }
                           """,
                FixedCode = """
                            internal class Program
                            {
                                public static void Main() {}
                            }
                            """,
                TestState =
                {
                    OutputKind = outputKind,
                    AnalyzerConfigFiles =
                    {
                        ("/.editorconfig",
                            $"""
                             root = true

                             [*]
                             dotnet_code_quality.CA1515.output_kind = {optionsText}
                             """)
                    }
                },
                LanguageVersion = LanguageVersion.CSharp10
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestCode = """
                           Public Class [|Program|]
                               Public Shared Sub Main()
                               End Sub
                           End Class
                           """,
                FixedCode = """
                            Friend Class Program
                                Public Shared Sub Main()
                                End Sub
                            End Class
                            """,
                TestState =
                {
                    OutputKind = outputKind,
                    AnalyzerConfigFiles =
                    {
                        ("/.editorconfig",
                            $"""
                             root = true

                             [*]
                             dotnet_code_quality.CA1515.output_kind = {optionsText}
                             """)
                    }
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("ConsoleApplication", OutputKind.DynamicallyLinkedLibrary)]
        [InlineData("DynamicallyLinkedLibrary", OutputKind.ConsoleApplication)]
        [InlineData("WindowsApplication", OutputKind.ConsoleApplication)]
        [InlineData("WindowsRuntimeApplication", OutputKind.ConsoleApplication)]
        [InlineData("ConsoleApplication,WindowsApplication", OutputKind.WindowsRuntimeApplication)]
        [InlineData("ConsoleApplication,WindowsApplication,WindowsRuntimeApplication", OutputKind.DynamicallyLinkedLibrary)]
        public async Task CompilationOptions_NoDiagnostic(string optionsText, OutputKind outputKind)
        {
            await new VerifyCS.Test
            {
                TestCode = """
                           public class Program
                           {
                               public static void Main() {}
                           }
                           """,
                TestState =
                {
                    OutputKind = outputKind,
                    AnalyzerConfigFiles =
                    {
                        ("/.editorconfig",
                            $"""
                             root = true

                             [*]
                             dotnet_code_quality.CA1515.output_kind = {optionsText}
                             """)
                    }
                },
                LanguageVersion = LanguageVersion.CSharp10
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestCode = """
                           Public Class Program
                               Public Shared Sub Main()
                               End Sub
                           End Class
                           """,
                TestState =
                {
                    OutputKind = outputKind,
                    AnalyzerConfigFiles =
                    {
                        ("/.editorconfig",
                            $"""
                             root = true

                             [*]
                             dotnet_code_quality.CA1515.output_kind = {optionsText}
                             """)
                    }
                },
            }.RunAsync();
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task Trivia(OutputKind outputKind)
        {
            await VerifyCsAsync(outputKind,
                """
                // This is the entry point class.
                public class [|Program|]
                {
                    public static void Main() {}
                }
                """,
                """
                // This is the entry point class.
                internal class Program
                {
                    public static void Main() {}
                }
                """);

            await VerifyVbAsync(outputKind,
                """
                ' This is the entry point class.
                Public Class [|Program|]
                    Public Shared Sub Main()
                    End Sub
                End Class
                """,
                """
                ' This is the entry point class.
                Friend Class Program
                    Public Shared Sub Main()
                    End Sub
                End Class
                """);
        }

        private Task VerifyCsAsync(OutputKind outputKind, string testCode, string fixedCode = null)
        {
            return new VerifyCS.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode!,
                TestState = { OutputKind = outputKind },
                LanguageVersion = LanguageVersion.CSharp10
            }.RunAsync();
        }

        private Task VerifyVbAsync(OutputKind outputKind, string testCode, string fixedCode = null)
        {
            return new VerifyVB.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode!,
                TestState = { OutputKind = outputKind }
            }.RunAsync();
        }

        public static IEnumerable<object[]> DiagnosticTriggeringOutputKinds()
        {
            yield return new object[] { OutputKind.ConsoleApplication };
            yield return new object[] { OutputKind.WindowsRuntimeApplication };
            yield return new object[] { OutputKind.WindowsApplication };
        }

        public static IEnumerable<object[]> NonDiagnosticTriggeringOutputKinds()
        {
            yield return new object[] { OutputKind.NetModule };
            yield return new object[] { OutputKind.DynamicallyLinkedLibrary };
            yield return new object[] { OutputKind.WindowsRuntimeMetadata };
        }
    }
}