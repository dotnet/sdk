// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.ProvideStreamMemoryBasedAsyncOverrides,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.ProvideStreamMemoryBasedAsyncOverrides,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class ProvideStreamMemoryBasedAsyncOverridesTests
    {
        #region Reports Diagnostic
        [Fact]
        public Task ReadAsyncArray_NoReadAsyncMemory_ReportsDiagnostic_CSAsync()
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class {{|#0:FooStream|}} : Stream
    {{
        {CSAbstractMembers}
        {CSReadAsyncArray}
    }}
}}";

            var diagnostic = VerifyCS.Diagnostic(Rule)
                .WithLocation(0)
                .WithArguments("FooStream", CSDisplayReadAsyncArray, CSDisplayReadAsyncMemory);
            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            };
            return test.RunAsync();
        }

        [Fact]
        public Task ReadAsyncArray_NoReadAsyncMemory_ReportsDiagnostic_VBAsync()
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class {{|#0:FooStream|}} : Inherits Stream
        {VBAbstractMembers}
        {VBReadAsyncArray}
    End Class
End Namespace";

            var diagnostic = VerifyVB.Diagnostic(Rule)
                .WithLocation(0)
                .WithArguments("FooStream", VBDisplayReadAsyncArray, VBDisplayReadAsyncMemory);
            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            };
            return test.RunAsync();
        }

        [Fact]
        public Task WriteAsyncArray_NoWriteAsyncMemory_ReportsDiagnostic_CSAsync()
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class {{|#0:BarStream|}} : Stream
    {{
        {CSAbstractMembers}
        {CSWriteAsyncArray}
    }}
}}";

            var diagnostic = VerifyCS.Diagnostic(Rule)
                .WithLocation(0)
                .WithArguments("BarStream", CSDisplayWriteAsyncArray, CSDisplayWriteAsyncMemory);
            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            };
            return test.RunAsync();
        }

        [Fact]
        public Task WriteAsyncArray_NoWriteAsyncMemory_ReportsDiagnostic_VBAsync()
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class {{|#0:BarStream|}} : Inherits Stream
        {VBAbstractMembers}
        {VBWriteAsyncArray}
    End Class
End Namespace";

            var diagnostic = VerifyVB.Diagnostic(Rule)
                .WithLocation(0)
                .WithArguments("BarStream", VBDisplayWriteAsyncArray, VBDisplayWriteAsyncMemory);
            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            };
            return test.RunAsync();
        }

        [Fact]
        public Task BothArrayOverrides_MissingAllMemoryOverrides_ReportsMultipleBiagnostics_CSAsync()
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class {{|#0:River|}} : Stream
    {{
        {CSAbstractMembers}
        {CSReadAsyncArray}
        {CSWriteAsyncArray}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithArguments("River", CSDisplayReadAsyncArray, CSDisplayReadAsyncMemory),
                    VerifyCS.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithArguments("River", CSDisplayWriteAsyncArray, CSDisplayWriteAsyncMemory)
                }
            };
            return test.RunAsync();
        }

        [Fact]
        public Task BothArrayOverrides_MissingAllMemoryOverrides_ReportsMultipleDiagnostics_VBAsync()
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class {{|#0:River|}} : Inherits Stream
        {VBAbstractMembers}
        {VBReadAsyncArray}
        {VBWriteAsyncArray}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithArguments("River", VBDisplayWriteAsyncArray, VBDisplayWriteAsyncMemory),
                    VerifyVB.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithArguments("River", VBDisplayReadAsyncArray, VBDisplayReadAsyncMemory)

                }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(CSReadAsyncArray, CSDisplayReadAsyncArray, CSDisplayReadAsyncMemory)]
        [InlineData(CSWriteAsyncArray, CSDisplayWriteAsyncArray, CSDisplayWriteAsyncMemory)]
        public Task SingleArrayOverride_MultiplePartialsInSameFile_ReportsAllLocations_CSAsync(string arrayMethod, string displayArrayMethod, string displayMemoryMethod)
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public partial class {{|#0:River|}} : Stream
    {{
        {CSAbstractMembers}
    }}
}}

namespace Testopolis
{{
    partial class {{|#1:River|}}
    {{
        {arrayMethod}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithArguments("River", displayArrayMethod, displayMemoryMethod),
                }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(VBReadAsyncArray, VBDisplayReadAsyncArray, VBDisplayReadAsyncMemory)]
        [InlineData(VBWriteAsyncArray, VBDisplayWriteAsyncArray, VBDisplayWriteAsyncMemory)]
        public Task SingleArrayOverride_MultiplePartialsInSameFile_ReportsAllLocations_VBAsync(string arrayMethod, string displayArrayMethod, string displayMemoryMethod)
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Partial Public Class {{|#0:River|}} : Inherits Stream
        {VBAbstractMembers}
        {arrayMethod}
    End Class
End Namespace

Namespace Testopolis
    Partial Class {{|#1:River|}}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithArguments("River", displayArrayMethod, displayMemoryMethod)
                }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(CSReadAsyncArray, CSDisplayReadAsyncArray, CSDisplayReadAsyncMemory)]
        [InlineData(CSWriteAsyncArray, CSDisplayWriteAsyncArray, CSDisplayWriteAsyncMemory)]
        public Task SingleArrayOverride_MultiplePartialsInSeparateFiles_ReportsAllLocations_CSAsync(string arrayMethod, string displayArrayMethod, string displayMemoryMethod)
        {
            string fooSource = $@"
{CSUsings}
namespace Testopolis
{{
    public partial class {{|#0:River|}} : Stream
    {{
        {CSAbstractMembers}
    }}
}}";
            string barSource = $@"
{CSUsings}
namespace Testopolis
{{
    partial class {{|#1:River|}}
    {{
        {arrayMethod}
    }}
}}";
            string bazSource = $@"
{CSUsings}
namespace Testopolis
{{
    partial class {{|#2:River|}}
    {{
    }}
}}";
            var test = new VerifyCS.Test
            {
                TestState = { Sources = { fooSource, barSource, bazSource } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithLocation(2)
                        .WithArguments("River", displayArrayMethod, displayMemoryMethod)
                }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(VBReadAsyncArray, VBDisplayReadAsyncArray, VBDisplayReadAsyncMemory)]
        [InlineData(VBWriteAsyncArray, VBDisplayWriteAsyncArray, VBDisplayWriteAsyncMemory)]
        public Task SingleArrayOverride_MultiplePartialsInSeparateFiles_ReportsAllLocations_VBAsync(string arrayMethod, string displayArrayMethod, string displayMemoryMethod)
        {
            string fooSource = $@"
{VBUsings}
Namespace Testopolis
    Partial Public Class {{|#0:River|}} : Inherits Stream
        {VBAbstractMembers}
    End Class
End Namespace";
            string barSource = $@"
{VBUsings}
Namespace Testopolis
    Partial Class {{|#1:River|}}
        {arrayMethod}
    End Class
End Namespace";
            string bazSource = $@"
{VBUsings}
Namespace Testopolis
    Partial Class {{|#2:River|}}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestState = { Sources = { fooSource, barSource, bazSource } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithLocation(2)
                        .WithArguments("River", displayArrayMethod, displayMemoryMethod)
                }
            };
            return test.RunAsync();
        }

        [Fact]
        public Task BothArrayOverrides_MultiplePartialsInSameFile_ReportsAllLocations_CSAsync()
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public partial class {{|#0:River|}} : Stream
    {{
        {CSAbstractMembers}
        {CSReadAsyncArray}
    }}
}}

namespace Testopolis
{{
    partial class {{|#1:River|}}
    {{
    }}
}}

namespace Testopolis
{{
    partial class {{|#2:River|}}
    {{
        {CSWriteAsyncArray}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithLocation(2)
                        .WithArguments("River", CSDisplayReadAsyncArray, CSDisplayReadAsyncMemory),
                    VerifyCS.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithLocation(2)
                        .WithArguments("River", CSDisplayWriteAsyncArray, CSDisplayWriteAsyncMemory)
                }
            };
            return test.RunAsync();
        }

        [Fact]
        public Task BothArrayOverrides_MultiplePartialsInSameFile_ReportsAllLocations_VBAsync()
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Partial Public Class {{|#0:River|}} : Inherits Stream
        {VBAbstractMembers}
        {VBReadAsyncArray}
    End Class
End Namespace

Namespace Testopolis
    Partial Class {{|#1:River|}}
    End Class
End Namespace

Namespace Testopolis
    Partial Class {{|#2:River|}}
        {VBWriteAsyncArray}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithLocation(2)
                        .WithArguments("River", VBDisplayReadAsyncArray, VBDisplayReadAsyncMemory),
                    VerifyVB.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithLocation(2)
                        .WithArguments("River", VBDisplayWriteAsyncArray, VBDisplayWriteAsyncMemory)
                }
            };
            return test.RunAsync();
        }

        [Fact]
        public Task BothArrayOverrides_MultiplePartialsInSeparateFiles_ReportsAllLocations_CSAsync()
        {
            string fooSource = $@"
{CSUsings}
namespace Testopolis
{{
    public partial class {{|#0:River|}} : Stream
    {{
        {CSAbstractMembers}
        {CSReadAsyncArray}
    }}
}}";
            string barSource = $@"
{CSUsings}
namespace Testopolis
{{
    partial class {{|#1:River|}}
    {{
    }}
}}";
            string bazSource = $@"
{CSUsings}
namespace Testopolis
{{
    partial class {{|#2:River|}}
    {{
        {CSWriteAsyncArray}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestState = { Sources = { fooSource, barSource, bazSource } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithLocation(2)
                        .WithArguments("River", CSDisplayReadAsyncArray, CSDisplayReadAsyncMemory),
                    VerifyCS.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithLocation(2)
                        .WithArguments("River", CSDisplayWriteAsyncArray, CSDisplayWriteAsyncMemory)
                }
            };
            return test.RunAsync();
        }

        [Fact]
        public Task BothArrayOverrides_MultiplePartialsInSeparateFiles_ReportsAllLocations_VBAsync()
        {
            string fooSource = $@"
{VBUsings}
Namespace Testopolis
    Partial Public Class {{|#0:River|}} : Inherits Stream
        {VBAbstractMembers}
        {VBReadAsyncArray}
    End Class
End Namespace";
            string barSource = $@"
{VBUsings}
Namespace Testopolis
    Partial Class {{|#1:River|}}
    End Class
End Namespace";
            string bazSource = $@"
{VBUsings}
Namespace Testopolis
    Partial Class {{|#2:River|}}
        {VBWriteAsyncArray}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestState = { Sources = { fooSource, barSource, bazSource } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithLocation(2)
                        .WithArguments("River", VBDisplayReadAsyncArray, VBDisplayReadAsyncMemory),
                    VerifyVB.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithLocation(2)
                        .WithArguments("River", VBDisplayWriteAsyncArray, VBDisplayWriteAsyncMemory)
                }
            };
            return test.RunAsync();
        }

        //  This test has no VB counterpart because in Visual Basic it is illegal to override one overload
        //  of a base-class method while implicitly hiding another overload.
        [Theory]
        [InlineData(CSReadAsyncArray, CSHideReadAsyncMemory, CSDisplayReadAsyncArray, CSDisplayReadAsyncMemory)]
        [InlineData(CSWriteAsyncArray, CSHideWriteAsyncMemory, CSDisplayWriteAsyncArray, CSDisplayWriteAsyncMemory)]
        public Task WhenMemoryMethodNotDeclaredOverride_ReportsDiagnosticAsync(string arrayMethod, string memoryMethod, string displayArrayMethod, string displayMemoryMethod)
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class {{|#0:River|}} : Stream
    {{
        {CSAbstractMembers}
        {arrayMethod}
#pragma warning disable {CSMemberHidesBaseRuleId}
        {memoryMethod}
#pragma warning restore {CSMemberHidesBaseRuleId}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithArguments("River", displayArrayMethod, displayMemoryMethod)
                }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(CSReadAsyncArray, CSHideExplicitReadAsyncMemory, CSDisplayReadAsyncArray, CSDisplayReadAsyncMemory)]
        [InlineData(CSWriteAsyncArray, CSHideExplicitWriteAsyncMemory, CSDisplayWriteAsyncArray, CSDisplayWriteAsyncMemory)]
        public Task WhenMemoryMethodDeclaredNew_ReportsDiagnostic_CSAsync(string arrayMethod, string memoryMethod, string displayArrayMethod, string displayMemoryMethod)
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class {{|#0:River|}} : Stream
    {{
        {CSAbstractMembers}
        {arrayMethod}
        {memoryMethod}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithArguments("River", displayArrayMethod, displayMemoryMethod)
                }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(VBReadAsyncArray, VBHideExplicitReadAsyncMemory, VBDisplayReadAsyncArray, VBDisplayReadAsyncMemory)]
        [InlineData(VBWriteAsyncArray, VBHideExplicitWriteAsyncMemory, VBDisplayWriteAsyncArray, VBDisplayWriteAsyncMemory)]
        public Task WhenMemoryMethodDeclaredNew_ReportsDiagnostic_VBAsync(string arrayMethod, string memoryMethod, string displayArrayMethod, string displayMemoryMethod)
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class {{|#0:River|}} : Inherits Stream
        {VBAbstractMembers}
        {arrayMethod}
        {memoryMethod}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithArguments("River", displayArrayMethod, displayMemoryMethod)
                }
            };
            return test.RunAsync();
        }
        #endregion

        #region No Diagnostic
        [Fact]
        public Task ReadAsyncArray_WithReadAsyncMemory_NoDiagnostic_CSAsync()
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class BazStream : Stream
    {{
        {CSAbstractMembers}
        {CSReadAsyncArray}
        {CSReadAsyncMemory}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task ReadAsyncArray_WithReadAsyncMemory_NoDiagnostic_VBAsync()
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class BazStream : Inherits Stream
        {VBAbstractMembers}
        {VBReadAsyncArray}
        {VBReadAsyncMemory}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task WriteAsyncArray_WithWriteAsyncMemory_NoDiagnostic_CSAsync()
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class WhippleStream : Stream
    {{
        {CSAbstractMembers}
        {CSWriteAsyncArray}
        {CSWriteAsyncMemory}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task WriteAsyncArray_WithWriteAsyncMemory_NoDiagnostic_VBAsync()
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class WhippleStream : Inherits Stream
        {VBAbstractMembers}
        {VBWriteAsyncArray}
        {VBWriteAsyncMemory}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task ReadAsyncMemory_WithoutReadAsyncArray_NoDiagnostic_CSAsync()
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class River : Stream
    {{
        {CSAbstractMembers}
        {CSReadAsyncMemory}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task ReadAsyncMemory_WithoutReadAsyncArray_NoDiagnostic_VBAsync()
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class River : Inherits Stream
        {VBAbstractMembers}
        {VBReadAsyncMemory}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task WriteAsyncMemory_WithoutWriteAsyncArray_NoDiagnostic_CSAsync()
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class River : Stream
    {{
        {CSAbstractMembers}
        {CSWriteAsyncMemory}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task WriteAsyncMemory_WithoutWriteAsyncArray_NoDiagnostic_VBAsync()
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class River : Inherits Stream
        {VBAbstractMembers}
        {VBWriteAsyncMemory}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(CSReadAsyncArray)]
        [InlineData(CSWriteAsyncArray)]
        public Task WhenStreamIsGrandBase_andBaseDoesNotOverrideArrayMethod_NoDiagnostic_CSAsync(string arrayMethodDefinition)
        {
            string @base = $@"
{CSUsings}
namespace Testopolis
{{
    public class BaseStream : Stream
    {{
        {CSAbstractMembers}
    }}
}}";
            string derived = $@"
{CSUsings}
namespace Testopolis
{{
    public class DerivedStream : BaseStream
    {{
        {arrayMethodDefinition}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestState = { Sources = { @base, derived } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(VBReadAsyncArray)]
        [InlineData(VBWriteAsyncArray)]
        public Task WhenStreamIsGrandBase_andBaseDoesNotOverrideArrayMethod_NoDiagnostic_VBAsync(string arrayMethodDefinition)
        {
            string @base = $@"
{VBUsings}
Namespace Testopolis
    Public Class BaseStream : Inherits Stream
        {VBAbstractMembers}
    End Class
End Namespace";
            string derived = $@"
{VBUsings}
Namespace Testopolis
    Public Class DerivedStream : Inherits BaseStream
        {arrayMethodDefinition}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestState = { Sources = { @base, derived } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(CSReadAsyncArray)]
        [InlineData(CSWriteAsyncArray)]
        public Task WhenStreamIsGrandBase_andBaseOverridesArrayMethod_NoDiagnostic_CSAsync(string arrayMethodDefinition)
        {
            string @base = $@"
{CSUsings}
namespace Testopolis
{{
#pragma warning disable {RuleId}
    public class BaseStream : Stream
#pragma warning restore {RuleId}
    {{
        {CSAbstractMembers}
        {arrayMethodDefinition}
    }}
}}";
            string derived = $@"
{CSUsings}
namespace Testopolis
{{
    public class DerivedStream : BaseStream
    {{
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestState = { Sources = { @base, derived } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(VBReadAsyncArray)]
        [InlineData(VBWriteAsyncArray)]
        public Task WhenStreamIsGrandBase_andBaseOverridesArray_NoDiagnostic_VBAsync(string arrayMethodDefinition)
        {
            string @base = $@"
{VBUsings}
Namespace Testopolis
#Disable Warning {RuleId}
    Public Class BaseStream : Inherits Stream
#Enable Warning {RuleId}
        {VBAbstractMembers}
        {arrayMethodDefinition}
    End Class
End Namespace";
            string derived = $@"
{VBUsings}
Namespace Testopolis
    Public Class DerivedStream : Inherits BaseStream
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestState = { Sources = { @base, derived } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(CSReadAsyncArray)]
        [InlineData(CSWriteAsyncArray)]
        public Task WhenStreamIsGrandBase_andBothBaseAndDerivedOverrideArrayMethod_NoDiagnostic_CSAsync(string arrayMethodDefinition)
        {
            string @base = $@"
{CSUsings}
namespace Testopolis
{{
#pragma warning disable {RuleId}
    public class BaseStream : Stream
#pragma warning restore {RuleId}
    {{
        {CSAbstractMembers}
        {arrayMethodDefinition}
    }}
}}";
            string derived = $@"
{CSUsings}
namespace Testopolis
{{
    public class DerivedStream : BaseStream
    {{
        {arrayMethodDefinition}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestState = { Sources = { @base, derived } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(VBReadAsyncArray)]
        [InlineData(VBWriteAsyncArray)]
        public Task WhenStreamIsGrandBase_andBothBaseAndDerivedOverrideArrayMethod_NoDiagnostic_VBAsync(string arrayMethodDefinition)
        {
            string @base = $@"
{VBUsings}
Namespace Testopolis
#Disable Warning {RuleId}
    Public Class BaseStream : Inherits Stream
#Enable Warning {RuleId}
        {VBAbstractMembers}
        {arrayMethodDefinition}
    End Class
End Namespace";
            string derived = $@"
{VBUsings}
Namespace Testopolis
    Public Class DerivedStream : Inherits BaseStream
        {arrayMethodDefinition}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestState = { Sources = { @base, derived } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(CSHideReadAsyncArray)]
        [InlineData(CSHideWriteAsyncArray)]
        public Task WhenArrayMethodNotDeclaredOverride_NoDiagnostic_CSAsync(string arrayMethod)
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class River : Stream
    {{
        {CSAbstractMembers}
#pragma warning disable {CSMemberHidesBaseRuleId}
        {arrayMethod}
#pragma warning restore {CSMemberHidesBaseRuleId}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(VBHideReadAsyncArray)]
        [InlineData(VBHideWriteAsyncArray)]
        public Task WhenArrayMethodNotDeclaredOverride_NoDiagnostic_VBAsync(string arrayMethod)
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class River : Inherits Stream
        {VBAbstractMembers}
#Disable Warning {VBMemberHidesBaseRuleId}
        {arrayMethod}
#Enable Warning {VBMemberHidesBaseRuleId}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(CSHideExplicitReadAsyncArray)]
        [InlineData(CSHideExplicitWriteAsyncArray)]
        public Task WhenArrayMethodDeclaredNew_NoDiagnostic_CSAsync(string arrayMethod)
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class River : Stream
    {{
        {CSAbstractMembers}
        {arrayMethod}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(VBHideExplicitReadAsyncArray)]
        [InlineData(VBHideExplicitWriteAsyncArray)]
        public Task WhenArrayMethodDeclaredNew_NoDiagnostic_VBAsync(string arrayMethod)
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class River : Inherits Stream
        {VBAbstractMembers}
        {arrayMethod}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }
        #endregion

        #region Does Not Crash On Illegal Code
        [Theory]
        [InlineData(ReadAsyncName, CSReadAsyncArray)]
        [InlineData(WriteAsyncName, CSWriteAsyncArray)]
        public Task DuplicateArrayOverrides_WithoutMemoryOverride_ReportsDiagnosticWithoutCrashing_CSAsync(string methodName, string methodDefinition)
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class {{|#0:River|}} : Stream
    {{
        {CSAbstractMembers}
        {methodDefinition}
        {methodDefinition.Replace(methodName, $"{{|#1:{methodName}|}}")}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(RuleId, DiagnosticSeverity.Info)
                        .WithLocation(0),
                    new DiagnosticResult(CSMemberHasMultipleDefinitionsRuleId, DiagnosticSeverity.Error)
                        .WithLocation(1)
                }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(ReadAsyncName, VBReadAsyncArray)]
        [InlineData(WriteAsyncName, VBWriteAsyncArray)]
        public Task DuplicateArrayOverrides_WithoutMemoryOverride_ReportsDiagnosticWithoutCrashing_VBAsync(string methodName, string methodDefinition)
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class {{|#0:River|}} : Inherits Stream
        {VBAbstractMembers}
        {SurroundWithMarkup(methodDefinition, methodName, 1)}
        {methodDefinition}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(RuleId, DiagnosticSeverity.Info)
                        .WithLocation(0),
                    new DiagnosticResult(VBMemberHasMultipleDefinitionsRuleId, DiagnosticSeverity.Error)
                        .WithLocation(1)
                }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(ReadAsyncName, CSReadAsyncArray, CSReadAsyncMemory)]
        [InlineData(WriteAsyncName, CSWriteAsyncArray, CSWriteAsyncMemory)]
        public Task DuplicateArrayOverrides_WithMemoryOverride_NoDiagnostic_NoCrash_CSAsync(string methodName, string arrayMethodDefinition, string memoryMethodDefinition)
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class River : Stream
    {{
        {CSAbstractMembers}
        {arrayMethodDefinition}
        {SurroundWithMarkup(arrayMethodDefinition, methodName, 0)}
        {memoryMethodDefinition}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(CSMemberHasMultipleDefinitionsRuleId, DiagnosticSeverity.Error)
                        .WithLocation(0)
                }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(ReadAsyncName, VBReadAsyncArray, VBReadAsyncMemory)]
        [InlineData(WriteAsyncName, VBWriteAsyncArray, VBWriteAsyncMemory)]
        public Task DuplicateArrayOverrides_WithMemoryOverride_NoDiagnostic_NoCrash_VBAsync(string methodName, string arrayMethodDefinition, string memoryMethodDefinition)
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class River : Inherits Stream
        {VBAbstractMembers}
        {SurroundWithMarkup(arrayMethodDefinition, methodName, 0)}
        {arrayMethodDefinition}
        {memoryMethodDefinition}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(VBMemberHasMultipleDefinitionsRuleId, DiagnosticSeverity.Error)
                        .WithLocation(0)
                }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(ReadAsyncName, CSReadAsyncArray, CSReadAsyncMemory)]
        [InlineData(WriteAsyncName, CSWriteAsyncArray, CSWriteAsyncMemory)]
        public Task DuplicateMemoryOverrides_WithArrayOverride_NoDiagnostic_NoCrash_CSAsync(string methodName, string arrayMethodDefinition, string memoryMethodDefinition)
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class River : Stream
    {{
        {CSAbstractMembers}
        {arrayMethodDefinition}
        {memoryMethodDefinition}
        {SurroundWithMarkup(memoryMethodDefinition, methodName, 0)}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(CSMemberHasMultipleDefinitionsRuleId, DiagnosticSeverity.Error)
                        .WithLocation(0)
                }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(ReadAsyncName, VBReadAsyncArray, VBReadAsyncMemory)]
        [InlineData(WriteAsyncName, VBWriteAsyncArray, VBWriteAsyncMemory)]
        public Task DuplicateMemoryOverrides_WithArrayOverride_NoDiagnostic_NoCrash_VBAsync(string methodName, string arrayMethodDefinition, string memoryMethodDefinition)
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class River : Inherits Stream
        {VBAbstractMembers}
        {arrayMethodDefinition}
        {SurroundWithMarkup(memoryMethodDefinition, methodName, 0)}
        {memoryMethodDefinition}

    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(VBMemberHasMultipleDefinitionsRuleId, DiagnosticSeverity.Error)
                        .WithLocation(0)
                }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(ReadAsyncName, CSReadAsyncMemory)]
        [InlineData(WriteAsyncName, CSWriteAsyncMemory)]
        public Task DuplicateMemoryOverrides_NoArrayOverride_NoDiagnostic_NoCrash_CSAsync(string methodName, string memoryMethodDefinition)
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class River : Stream
    {{
        {CSAbstractMembers}
        {memoryMethodDefinition}
        {SurroundWithMarkup(memoryMethodDefinition, methodName, 0)}
    }}
}}";

            var test = new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(CSMemberHasMultipleDefinitionsRuleId, DiagnosticSeverity.Error)
                        .WithLocation(0)
                }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(ReadAsyncName, VBReadAsyncMemory)]
        [InlineData(WriteAsyncName, VBWriteAsyncMemory)]
        public Task DuplicateMemoryOverrides_NoArrayOverride_NoDiagnostic_NoCrash_VBAsync(string methodName, string memoryMethodDefinition)
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class River : Inherits Stream
        {VBAbstractMembers}
        {SurroundWithMarkup(memoryMethodDefinition, methodName, 0)}
        {memoryMethodDefinition}
    End Class
End Namespace";

            var test = new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(VBMemberHasMultipleDefinitionsRuleId, DiagnosticSeverity.Error)
                        .WithLocation(0)
                }
            };
            return test.RunAsync();
        }
        #endregion

        #region Helpers
        private const string ReadAsyncName = nameof(System.IO.Stream.ReadAsync);
        private const string WriteAsyncName = nameof(System.IO.Stream.WriteAsync);

        private const string CSUsings = @"using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;";
        private const string CSAbstractMembers = @"public override void Flush() => throw null;
        public override int Read(byte[] buffer, int offset, int count) => throw null;
        public override long Seek(long offset, SeekOrigin origin) => throw null;
        public override void SetLength(long value) => throw null;
        public override void Write(byte[] buffer, int offset, int count) => throw null;
        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public override long Length { get; }
        public override long Position { get; set; }";
        private const string CSReadAsyncArray = @"public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw null;";
        private const string CSReadAsyncMemory = @"public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw null;";
        private const string CSWriteAsyncArray = @"public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw null;";
        private const string CSWriteAsyncMemory = @"public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw null;";

        private const string VBUsings = @"Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks";
        private const string VBAbstractMembers = @"Public Overrides ReadOnly Property CanRead As Boolean
            Get
                Throw New NotImplementedException()
            End Get
        End Property
        Public Overrides ReadOnly Property CanSeek As Boolean
            Get
                Throw New NotImplementedException()
            End Get
        End Property
        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Throw New NotImplementedException()
            End Get
        End Property
        Public Overrides ReadOnly Property Length As Long
            Get
                Throw New NotImplementedException()
            End Get
        End Property
        Public Overrides Property Position As Long
            Get
                Throw New NotImplementedException()
            End Get
            Set(value As Long)
                Throw New NotImplementedException()
            End Set
        End Property
        Public Overrides Sub Flush()
            Throw New NotImplementedException()
        End Sub
        Public Overrides Sub SetLength(value As Long)
            Throw New NotImplementedException()
        End Sub
        Public Overrides Sub Write(buffer() As Byte, offset As Integer, count As Integer)
            Throw New NotImplementedException()
        End Sub
        Public Overrides Function Read(buffer() As Byte, offset As Integer, count As Integer) As Integer
            Throw New NotImplementedException()
        End Function
        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
            Throw New NotImplementedException()
        End Function";
        private const string VBReadAsyncArray = @"Public Overrides Function ReadAsync(buffer() As Byte, offset As Integer, count As Integer, ct As CancellationToken) As Task(Of Integer)
            Throw New NotImplementedException()
        End Function";
        private const string VBReadAsyncMemory = @"Public Overrides Function ReadAsync(buffer As Memory(Of Byte), Optional ct As CancellationToken = Nothing) As ValueTask(Of Integer)
            Throw New NotImplementedException()
        End Function";
        private const string VBWriteAsyncArray = @"Public Overrides Function WriteAsync(buffer() As Byte, offset As Integer, count As Integer, ct As CancellationToken) As Task
            Throw New NotImplementedException()
        End Function";
        private const string VBWriteAsyncMemory = @"Public Overrides Function WriteAsync(buffer As ReadOnlyMemory(Of Byte), Optional ct As CancellationToken = Nothing) As ValueTask
            Throw New NotImplementedException()
        End Function";

        private const string CSDisplayReadAsyncArray = @"ReadAsync";
        private const string CSDisplayReadAsyncMemory = @"ReadAsync";
        private const string CSDisplayWriteAsyncArray = @"WriteAsync";
        private const string CSDisplayWriteAsyncMemory = @"WriteAsync";

        private const string VBDisplayReadAsyncArray = @"ReadAsync";
        private const string VBDisplayReadAsyncMemory = @"ReadAsync";
        private const string VBDisplayWriteAsyncArray = @"WriteAsync";
        private const string VBDisplayWriteAsyncMemory = @"WriteAsync";

        private const string CSHideReadAsyncArray = @"public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw null;";
        private const string CSHideReadAsyncMemory = @"public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw null;";
        private const string CSHideWriteAsyncArray = @"public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw null;";
        private const string CSHideWriteAsyncMemory = @"public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw null;";

        private const string VBHideReadAsyncArray = @"Public Function ReadAsync(buffer() As Byte, offset As Integer, count As Integer, cancellationToken As CancellationToken) As Task(Of Integer)
            Throw New NotImplementedException()
        End Function";
        private const string VBHideWriteAsyncArray = @"Public Function WriteAsync(buffer As Byte, offset As Integer, count As Integer, cancellationToken As CancellationToken) As Task
            Throw New NotImplementedException()
        End Function";

        private const string CSHideExplicitReadAsyncArray = @"public new Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw null;";
        private const string CSHideExplicitReadAsyncMemory = @"public new ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw null;";
        private const string CSHideExplicitWriteAsyncArray = @"public new Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw null;";
        private const string CSHideExplicitWriteAsyncMemory = @"public new ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw null;";

        private const string VBHideExplicitReadAsyncArray = @"Public Overloads Function ReadAsync(buffer() As Byte, offset As Integer, count As Integer, cancellationToken As CancellationToken) As Task(Of Integer)
            Throw New NotImplementedException()
        End Function";
        private const string VBHideExplicitReadAsyncMemory = @"Public Overloads Function ReadAsync(buffer As Memory(Of Byte), Optional cancellationToken As CancellationToken = Nothing) As ValueTask(Of Integer)
            Throw New NotImplementedException()
        End Function";
        private const string VBHideExplicitWriteAsyncArray = @"Public Overloads Function WriteAsync(buffer As Byte, offset As Integer, count As Integer, cancellationToken As CancellationToken) As Task
            Throw New NotImplementedException()
        End Function";
        private const string VBHideExplicitWriteAsyncMemory = @"Public Overloads Function WriteAsync(buffer As ReadOnlyMemory(Of Byte), Optional cancellationToken As CancellationToken = Nothing) As ValueTask
            Throw New NotImplementedException()
        End Function";

        private static DiagnosticDescriptor Rule => ProvideStreamMemoryBasedAsyncOverrides.Rule;
        private static string RuleId => ProvideStreamMemoryBasedAsyncOverrides.RuleId;
        private const string CSMemberHidesBaseRuleId = "CS0114";
        private const string CSMemberHasMultipleDefinitionsRuleId = "CS0111";

        private const string VBMemberHidesBaseRuleId = "BC40005";
        private const string VBMemberHasMultipleDefinitionsRuleId = "BC30269";

        private static string SurroundWithMarkup(string source, string substring, int markupKey)
        {
            return source.Replace(substring, $"{{|#{markupKey}:{substring}|}}");
        }
        #endregion
    }
}
