// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.PropertiesShouldNotBeWriteOnlyAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.PropertiesShouldNotBeWriteOnlyFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.PropertiesShouldNotBeWriteOnlyAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.PropertiesShouldNotBeWriteOnlyFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class PropertiesShouldNotBeWriteOnlyTests
    {
        // Valid C# Tests that should not be flagged based on CA1044 (good tests)
        [Fact]
        public async Task CS_CA1044Good_Read_WriteAsync()
        {
            var code = @"
using System;
namespace CS_DesignLibrary
{
    public class CS_GoodClassWithReadWriteProperty
    {
        string CS_someName;
        public string CS_Name
        {
            get { return CS_someName; }
            set { CS_someName = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CS_CA1044Good_Read_Write1Async()
        {
            var code = @"
using System;
namespace CS_GoodPropertiesShouldNotBeWriteOnlyTests1
{
    public class CS_GoodClassWithReadWriteProperty
    {
        protected string CS_field;
        public virtual string CS_ReadableProperty1
        {
            get { return CS_field; }
            set { CS_field = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CS_CA1044Good_public_Read_private_WriteAsync()
        {
            var code = @"
using System;
namespace CS_GoodPropertiesShouldNotBeWriteOnlyTests2
{
    public class CS_ClassWithReadableProperty
    {
        protected string CS_field;
        public string CS_AccessibleProperty2
        {
            get { return CS_field; }
            private set { CS_field = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CS_CA1044Good_protected_Read_private_WriteAsync()
        {
            var code = @"
using System;
namespace CS_GoodPropertiesShouldNotBeWriteOnlyTests3
{
    public class CS_ClassWithReadableProperty
    {
        protected string CS_field;
        protected string CS_AccessibleProperty3
        {
            get { return CS_field; }
            private set { CS_field = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CS_CA1044Good_internal_Read_private_WriteAsync()
        {
            var code = @"
using System;
namespace CS_GoodPropertiesShouldNotBeWriteOnlyTests4
{
    public class CS_GoodClassWithReadWriteProperty
    {
        protected string CS_field;
        internal string CS_AccessibleProperty4
        {
            get { return CS_field; }
            private set { CS_field = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CS_CA1044Good_protected_internal_Read_internal_WriteAsync()
        {
            var code = @"
using System;
namespace CS_GoodPropertiesShouldNotBeWriteOnlyTests5
{
    public class CS_GoodClassWithReadWriteProperty
    {
        protected string CS_field;
        protected internal string AccessibleProperty5
        {
            get { return CS_field; }
            internal set { CS_field = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CS_CA1044Good_public_Read_internal_WriteAsync()
        {
            var code = @"
using System;
namespace CS_GoodPropertiesShouldNotBeWriteOnlyTests6
{
    public class CS_GoodClassWithReadWriteProperty
    {
        protected string CS_field;
        public string CS_AccessibleProperty6
        {
            get { return CS_field; }
            internal set { CS_field = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CS_CA1044Good_public_Read_protected_WriteAsync()
        {
            var code = @"
using System;
namespace CS__GoodPropertiesShouldNotBeWriteOnlyTests7
{
    public class CS_GoodClassWithReadWriteProperty
    {
        protected string CS_field;
        public string CS_AccessibleProperty7
        {
            get { return CS_field; }
            protected set { CS_field = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CS_CA1044Good_public_override_WriteAsync()
        {
            var code = @"
using System;
namespace CS_GoodPropertiesShouldNotBeWriteOnlyTests8
{
    public class CS_ClassWithReadableProperty
    {
        public virtual string CS_ReadableProperty8 { get; set; }
    }
    public class CS_DerivedClassWithReadableProperty : CS_ClassWithReadableProperty
    {
        protected string CS_field;
        public override string CS_ReadableProperty8
        {
            set { CS_field = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CS_CA1044InterfaceAsync()
        {
            var code = @"
using System;
namespace CS_GoodPropertiesShouldNotBeWriteOnlyTests9
{
    public interface IInterface
    {
        string InterfaceProperty
        {
            get; set;
        }
    }
    public class Class1 : IInterface
    {
        string IInterface.InterfaceProperty
        {
            get { throw new NotImplementedException(); }
            set { }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CS_CA1044Base_WriteAsync()
        {
            var code = @"
using System;
namespace CS_GoodPropertiesShouldNotBeWriteOnlyTests10
{
    public class Base
    {
        public virtual string BaseProperty
        {
            get { throw new NotImplementedException(); }
            set { }
        }
    }
    public class Derived : Base
    {
        public override string BaseProperty
        {
            set { base.BaseProperty = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        // Valid VB Tests that should not be flagged based on CA1044 (good tests)
        [Fact]
        public async Task VB_CA1044Good_Read_WriteAsync()
        {
            var code = @"
Imports System
Namespace VB_DesignLibrary
    Public Class VB_GoodClassWithReadWriteProperty
        Private VB_someName As String
        Public Property VB_Name() As String
            Get
                Return VB_someName
            End Get
            Set(ByVal value As String)
                VB_someName = Value
            End Set
        End Property
    End Class
End Namespace
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task VB_CA1044Good_Read_Write1Async()
        {
            var code = @"
Imports System
Namespace VB_GoodPropertiesShouldNotBeWriteOnlyTests1
    Public Class VB_GoodClassWithReadWriteProperty
        Protected VB_field As String
        Public Overridable Property VB_ReadableProperty1() As String
            Get
                Return VB_field
            End Get
            Set(ByVal value As String)
                VB_field = Value
            End Set
        End Property
    End Class
End Namespace
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task VB_CA1044Good_public_Read_private_WriteAsync()
        {
            var code = @"
Imports System
Namespace VB_GoodPropertiesShouldNotBeWriteOnlyTests2
    Public class VB_ClassWithReadableProperty
        Protected VB_field As String
        Public Property VB_AccessibleProperty2() As String
            Get
                Return VB_field
            End Get
            Private Set(ByVal value As String)
                VB_field = Value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task VB_CA1044Good_protected_Read_private_WriteAsync()
        {
            var code = @"
Imports System
Namespace VB_GoodPropertiesShouldNotBeWriteOnlyTests3
    Public class VB_ClassWithReadableProperty
        Protected VB_field As String
        Protected Property VB_AccessibleProperty3() As String
            Get
                Return VB_field
            End Get
            Private Set(ByVal value As String)
                VB_field = Value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task VB_CA1044Good_internal_Read_private_WriteAsync()
        {
            var code = @"
Imports System
Namespace VB_GoodPropertiesShouldNotBeWriteOnlyTests4
    Public Class ClassWithReadableProperty
        Protected VB_field As String
        Friend Property VB_AccessibleProperty4() As String
            Get
                Return VB_field
            End Get
            Private Set(ByVal value As String)
                VB_field = Value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task VB_CA1044Good_protected_internal_Read_internal_WriteAsync()
        {
            var code = @"
Imports System
Namespace VB_GoodPropertiesShouldNotBeWriteOnlyTests5
    Public class VB_ClassWithReadableProperty
        Protected VB_field As String
        Protected Friend Property AccessibleProperty5() As String
            Get
                Return VB_field
            End Get
            Friend Set(ByVal value As String)
                VB_field = value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task VB_CA1044Good_public_Read_internal_WriteAsync()
        {
            var code = @"
Imports System
Namespace VB_GoodPropertiesShouldNotBeWriteOnlyTests6
    Public class VB_ClassWithReadableProperty
        Protected VB_field As String
        Public Property VB_AccessibleProperty6() As String
            Get
                Return VB_field
            End Get
            Friend Set(ByVal value As String)
                VB_field = Value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task VB_CA1044Good_public_Read_protected_WriteAsync()
        {
            var code = @"
Imports System
Namespace VB_GoodPropertiesShouldNotBeWriteOnlyTests7
    Public class VB_ClassWithReadableProperty
        Protected VB_field As String
        Public Property VB_AccessibleProperty7() As String
            Get
                Return VB_field
            End Get
            Protected Set(ByVal value As String)
                VB_field = Value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        // C# Tests that should be flagged with CA1044 Addgetter
        [Fact]
        public async Task CS_CA1044Bad_Write_with_NoReadAsync()
        {
            var code = @"
using System;
namespace CS_BadPropertiesShouldNotBeWriteOnlyTests
{
    public class CS_BadClassWithWriteOnlyProperty
    {
        protected string CS_someName;
        public string CS_WriteOnlyProperty
        {
            set { CS_someName = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCA1044CSharpResultAt(8, 23, CA1044MessageAddGetter, "CS_WriteOnlyProperty"));
        }

        [Fact]
        public async Task CS_CA1044Bad_Write_with_NoRead1Async()
        {
            var code = @"
using System;
namespace CS_BadPropertiesShouldNotBeWriteOnlyTests1
{
    public class CS_BadClassWithWriteOnlyProperty
    {
        protected string CS_someName;
        public string CS_WriteOnlyProperty1
        {
            set { CS_someName = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCA1044CSharpResultAt(8, 23, CA1044MessageAddGetter, "CS_WriteOnlyProperty1"));
        }

        [Fact]
        public async Task CS_CA1044Bad_Write_with_NoRead2Async()
        {
            var code = @"
using System;
namespace CS_BadPropertiesShouldNotBeWriteOnlyTests2
{
    public class CS_BadClassWithWriteOnlyProperty
    {
        string CS_someName;
        protected string CS_WriteOnlyProperty2
        {
            set { CS_someName = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCA1044CSharpResultAt(8, 26, CA1044MessageAddGetter, "CS_WriteOnlyProperty2"));
        }

        [Fact]
        public async Task CS_CA1044Bad_Write_with_NoRead3Async()
        {
            var code = @"
using System;
namespace CS_BadPropertiesShouldNotBeWriteOnlyTests3
{
    public class CS_BadClassWithWriteOnlyProperty
    {
        protected string CS_someName;
        protected string CS_WriteOnlyProperty3
        {
            set { CS_someName = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCA1044CSharpResultAt(8, 26, CA1044MessageAddGetter, "CS_WriteOnlyProperty3"));
        }

        [Fact]
        public async Task CS_CA1044Bad_Write_with_NoRead4Async()
        {
            var code = @"
using System;
namespace CS_BadPropertiesShouldNotBeWriteOnlyTests4
{
    public class CS_BadClassWithWriteOnlyProperty
    {
        protected string CS_someName;
        protected internal string CS_WriteOnlyProperty4
        {
            set { CS_someName = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCA1044CSharpResultAt(8, 35, CA1044MessageAddGetter, "CS_WriteOnlyProperty4"));
        }

        [Fact]
        public async Task CS_CA1044bad_Base_WriteAsync()
        {
            var code = @"
using System;
namespace CS_BadPropertiesShouldNotBeWriteOnlyTests5
{
    public class CS_Base
    {
        public virtual string CS_BaseProperty5
        {
            set { }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCA1044CSharpResultAt(7, 31, CA1044MessageAddGetter, "CS_BaseProperty5"));
        }

        [Fact]
        public async Task CS_CA1044bad_Interface_WriteAsync()
        {
            var code = @"
using System;
namespace CS_BadPropertiesShouldNotBeWriteOnlyTests6
{
    public interface CS_IInterface
    {
        string CS_InterfaceProperty6
        {
            set;
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCA1044CSharpResultAt(7, 16, CA1044MessageAddGetter, "CS_InterfaceProperty6"));
        }

        // C# Tests that should be flagged with CA1044 MakeMoreAccessible
        [Fact]
        public async Task CS_CA1044Bad_InaccessibleReadAsync()
        {
            var code = @"
using System;
namespace CS_BadPropertiesShouldNotBeWriteOnlyTests
{
    public class CS_BadClassWithWriteOnlyProperty
    {
         string field;
         public string CS_InaccessibleProperty
         {
            private get { return field; }
            set { field = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCA1044CSharpResultAt(8, 24, CA1044MessageMakeMoreAccessible, "CS_InaccessibleProperty"));
        }

        [Fact]
        public async Task CS_CA1044Bad_InaccessibleRead1Async()
        {
            var code = @"
using System;
namespace CS_BadPropertiesShouldNotBeWriteOnlyTests1
{
    public class CS_BadClassWithWriteOnlyProperty
    {
        string field;
        protected string CS_InaccessibleProperty1
        {
            private get { return field; }
            set { field = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCA1044CSharpResultAt(8, 26, CA1044MessageMakeMoreAccessible, "CS_InaccessibleProperty1"));
        }

        [Fact]
        public async Task CS_CA1044Bad_InaccessibleRead2Async()
        {
            var code = @"
using System;
namespace CS_BadPropertiesShouldNotBeWriteOnlyTests2
{
    public class CS_BadClassWithWriteOnlyProperty
    {
        protected string field;
        protected internal string CS_InaccessibleProperty2
        {
            internal get { return field; }
            set { field = value; }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCA1044CSharpResultAt(8, 35, CA1044MessageMakeMoreAccessible, "CS_InaccessibleProperty2"));
        }

        // VB Tests that should be flagged with CA1044 Addgetter
        [Fact]
        public async Task VB_CA1044Bad_Write_with_NoReadAsync()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests
    Public Class VB_BadClassWithWriteOnlyProperty
        Protected VB_someName As String
        Public WriteOnly Property VB_WriteOnlyProperty As String
            Set(ByVal value As String)
                VB_someName = value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(6, 35, CA1044MessageAddGetter, "VB_WriteOnlyProperty"));
        }

        [Fact]
        public async Task VB_CA1044Bad_Write_with_NoRead1Async()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests1
    Public Class VB_BadClassWithWriteOnlyProperty
        Protected VB_someName As String
        Protected WriteOnly Property VB_WriteOnlyProperty1() As String
            Set(ByVal value As String)
                VB_someName = value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(6, 38, CA1044MessageAddGetter, "VB_WriteOnlyProperty1"));
        }

        [Fact]
        public async Task VB_CA1044Bad_Write_with_NoRead2Async()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests
Public Class VB_BadClassWithWriteOnlyProperty
    Protected VB_someName As String
        Public WriteOnly Property VB_WriteOnlyProperty2 As String
            Set(ByVal value As String)
                VB_someName = value
            End Set
        End Property
   End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(6, 35, CA1044MessageAddGetter, "VB_WriteOnlyProperty2"));
        }

        [Fact]
        public async Task VB_CA1044Bad_Write_with_NoRead3Async()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests3
    Public Class VB_BadClassWithWriteOnlyProperty
        Protected VB_someName As String
        Protected Friend WriteOnly Property VB_WriteOnlyProperty3() As String
            Set(ByVal value As String)
                VB_someName = value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(6, 45, CA1044MessageAddGetter, "VB_WriteOnlyProperty3"));
        }

        [Fact]
        public async Task VB_CA1044Bad_Write_with_NoRead4Async()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests4
    Public Class VB_BadClassWithWriteOnlyProperty
        Protected VB_someName As String
        Public WriteOnly Property VB_WriteOnlyProperty4() As String
            Set(ByVal value As String)
                VB_someName = value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(6, 35, CA1044MessageAddGetter, "VB_WriteOnlyProperty4"));
        }

        [Fact]
        public async Task VB_CA1044Bad_Write_with_NoRead5Async()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests5
    Public Class VB_BadClassWithWriteOnlyProperty
        Protected VB_someName As String
        Public WriteOnly Property VB_WriteOnlyProperty5() As String
            Set(ByVal value As String)
                VB_someName = value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(6, 35, CA1044MessageAddGetter, "VB_WriteOnlyProperty5"));
        }
        [Fact]
        public async Task VB_CA1044Bad_Interface_WriteAsync()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests
    Public Interface IInterface
        WriteOnly Property InterfaceProperty As String
    End Interface
    Public Class Class1
        Implements IInterface
        Private WriteOnly Property IInterface_InterfaceProperty As String Implements IInterface.InterfaceProperty
            Set(ByVal value As String)
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(5, 28, CA1044MessageAddGetter, "InterfaceProperty"));
        }

        [Fact]
        public async Task VB_CA1044Bad_Interface_Write1Async()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests1
    Public Interface VB_IInterface
        WriteOnly Property VB_InterfaceProperty1() As String
    End Interface
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(5, 28, CA1044MessageAddGetter, "VB_InterfaceProperty1"));
        }

        [Fact]
        public async Task VB_CA1044Bad_Write_with_NoRead6Async()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests5
    Public Interface IInterface
        WriteOnly Property InterfaceProperty As String
    End Interface
    Public Class Class1
        Implements IInterface
        Private WriteOnly Property IInterface_InterfaceProperty As String Implements IInterface.InterfaceProperty
            Set(ByVal value As String)
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(5, 28, CA1044MessageAddGetter, "InterfaceProperty"));
        }
        [Fact]
        public async Task VB_CA1044Bad_Base_WriteAsync()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests
    Public Class VB_Base
        Public Overridable WriteOnly Property VB_BaseProperty() As String
            Set(ByVal value As String)
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(5, 47, CA1044MessageAddGetter, "VB_BaseProperty"));
        }

        // VB Tests that should be flagged with CA1044 MakeMoreAccessible
        [Fact]
        public async Task VB_CA1044Bad_InaccessibleReadAsync()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests
    Public Class VB_BadClassWithWriteOnlyProperty
        Private field As String
        Public Property VB_InaccessibleProperty As String
            Private Get
                Return field
            End Get
            Set(ByVal value As String)
                field = value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(6, 25, CA1044MessageMakeMoreAccessible, "VB_InaccessibleProperty"));
        }

        [Fact]
        public async Task VB_CA1044Bad_InaccessibleRead1Async()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests1
    Public Class VB_BadClassWithWriteOnlyProperty
        Private field As String
        Protected Property VB_InaccessibleProperty1() As String
            Private Get
                Return field
            End Get
            Set(ByVal value As String)
                field = value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(6, 28, CA1044MessageMakeMoreAccessible, "VB_InaccessibleProperty1"));
        }

        [Fact]
        public async Task VB_CA1044Bad_InaccessibleRead2Async()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests2
    Public Class VB_BadClassWithWriteOnlyProperty
        Private field As String
        Protected Friend Property VB_InaccessibleProperty2() As String
            Friend Get
                Return field
            End Get
            Set(ByVal value As String)
                field = value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(6, 35, CA1044MessageMakeMoreAccessible, "VB_InaccessibleProperty2"));
        }

        [Fact]
        public async Task VB_CA1044Bad_InaccessibleRead3Async()
        {
            var code = @"
Imports System
Namespace VB_BadPropertiesShouldNotBeWriteOnlyTests3
    Public Class VB_BadClassWithWriteOnlyProperty
        Private field As String
        Public Property VB_InaccessibleProperty3() As String
            Friend Get
                Return field
            End Get
            Set(ByVal value As String)
                field = value
            End Set
        End Property
    End Class
End NameSpace
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1044BasicResultAt(6, 25, CA1044MessageMakeMoreAccessible, "VB_InaccessibleProperty3"));
        }

        private static readonly string CA1044MessageAddGetter = MicrosoftCodeQualityAnalyzersResources.PropertiesShouldNotBeWriteOnlyMessageAddGetter;
        private static readonly string CA1044MessageMakeMoreAccessible = MicrosoftCodeQualityAnalyzersResources.PropertiesShouldNotBeWriteOnlyMessageMakeMoreAccessible;

        private static DiagnosticResult GetCA1044CSharpResultAt(int line, int column, string CA1044Message, string objectName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(CA1044Message == CA1044MessageAddGetter
                    ? PropertiesShouldNotBeWriteOnlyAnalyzer.AddGetterRule
                    : PropertiesShouldNotBeWriteOnlyAnalyzer.MakeMoreAccessibleRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(objectName);

        private static DiagnosticResult GetCA1044BasicResultAt(int line, int column, string CA1044Message, string objectName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(CA1044Message == CA1044MessageAddGetter
                    ? PropertiesShouldNotBeWriteOnlyAnalyzer.AddGetterRule
                    : PropertiesShouldNotBeWriteOnlyAnalyzer.MakeMoreAccessibleRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(objectName);
    }
}
