// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpPreventNumericIntPtrUIntPtrBehavioralChanges,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreventNumericIntPtrUIntPtrBehavioralChangesTests
    {

        [Fact]
        public async Task IntPtrAdditionSubstructionWithFieldReference()
        {
            await PopulateTestCs(@"
using System;

class Program
{
    IntPtr intPtr1;
    IntPtr intPtr2;

    public void M1()
    {
        checked
        {
            int i = 0;
            intPtr2 = {|#0:intPtr1 + 2|}; // Starting with .NET 7 the operator '+' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.
            intPtr2 = {|#1:intPtr1 - 2 * 3|}; // Starting with .NET 7 the operator '-' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.
            intPtr2 = {|#2:intPtr1 - 2|} - 3; // Starting with .NET 7 the operator '-' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.
            intPtr2 = {|#3:intPtr1 + i|} + 3; // Starting with .NET 7 the operator '+' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.
            intPtr1 = {|#4:intPtr2 - i++|};
            intPtr1++; 
            intPtr1+=2;
            intPtr2 = 2 + intPtr1;
            intPtr2 = intPtr1 * 2;
            intPtr2 = intPtr1 / 2;
        }

        intPtr2 = checked({|#5:intPtr1 - 2|}); // Starting with .NET 7 the operator '-' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.

        intPtr2 = intPtr1 + 2; // unchecked context 

        checked
        {
            intPtr2 = unchecked(intPtr1 + 2); // wrapped with unchecked, not warn
            intPtr2 = unchecked(intPtr1 - 2);
        }
    }
}",
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.OperatorThrowsRule).WithLocation(0).WithArguments("+"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.OperatorThrowsRule).WithLocation(1).WithArguments("-"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.OperatorThrowsRule).WithLocation(2).WithArguments("-"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.OperatorThrowsRule).WithLocation(3).WithArguments("+"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.OperatorThrowsRule).WithLocation(4).WithArguments("-"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.OperatorThrowsRule).WithLocation(5).WithArguments("-")).RunAsync();
        }

        [Fact]
        public async Task NintNUintUsedNotWarn()
        {
            await PopulateTestCs(@"
using System;

class Program
{
    nint nint1;
    nint nint2;

    public void M1()
    {
        nuint nuint1 = 0;
        checked
        {
            nint2 = nint1 + 2;
            nuint1 = nuint1 + 1;
        }

        nint2 = checked(nint1 - 2);
        nuint1 = checked(nuint1 - 2);

        nint2 = nint1 + 2;
        nuint1 = nuint1 + 2;
    }
}").RunAsync();
        }

        [Fact]
        public async Task ConversionInCheckedExpressionNotWarn()
        {
            await PopulateTestCs(@"
using System;

class Program
{
    IntPtr intPtr1 = IntPtr.Zero;

    public void M1(long long1, int offset)
    {
        intPtr1 = checked((IntPtr)long1);
        intPtr1 = checked((IntPtr)long1 + offset);
    }
}").RunAsync();
        }

        [Fact]
        public async Task IntPtrAdditionSubstructionWithParameterReference()
        {
            await PopulateTestCs(@"
using System;

class Program
{
    private IntPtr M2(IntPtr intPtr, int a)
    {
        return checked({|#0:intPtr + a|}); // Starting with .NET 7 the operator '+' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.
    }

    private IntPtr M3(IntPtr intPtr, int a)
    {
        return intPtr + a; 
    }

    private nint M4(IntPtr intPtr, int a)
    {
        return checked({|#1:intPtr - a|}); // Starting with .NET 7 the operator '-' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.
    }

    private nint M5(IntPtr intPtr, int a)
    {
        return intPtr + a;
    }

    private IntPtr M6(nint intPtr, int a)
    {
        return checked(intPtr - a);
    }

    private IntPtr M7(nint intPtr, int a)
    {
        return intPtr + a;
    }
}",
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.OperatorThrowsRule).WithLocation(0).WithArguments("+"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.OperatorThrowsRule).WithLocation(1).WithArguments("-")).RunAsync();
        }

        [Fact]
        public async Task UIntPtrAdditionSubstructionWithFieldReference()
        {
            await PopulateTestCs(@"
using System;

class Program
{
    UIntPtr uintPtr1;
    UIntPtr uintPtr2;

    public void M1()
    {
        checked
        {
            uintPtr2 = {|#0:uintPtr1 + 2|}; // Starting with .NET 7 the operator '+' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.
        }

        uintPtr2 = checked({|#2:uintPtr1 - 2|}); // Starting with .NET 7 the operator '-' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.

        uintPtr2 = uintPtr1 + 2;
        uintPtr2 = uintPtr1 + uintPtr2;
    }
}",
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.OperatorThrowsRule).WithLocation(0).WithArguments("+"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.OperatorThrowsRule).WithLocation(2).WithArguments("-")).RunAsync();
        }

        [Fact]
        public async Task IntPtrAdditionSubsructionWithLocalReference()
        {
            await PopulateTestCs(@"
using System;

class Program
{
    public void M1()
    {
        IntPtr intPtr1 = IntPtr.Zero;
        IntPtr intPtr2;

        checked
        {
            intPtr2 = {|#0:intPtr1 + 2|}; // Starting with .NET 7 the operator '+' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.
        }

        intPtr2 = checked({|#1:intPtr1 - 2|}); // Starting with .NET 7 the operator '+' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.

        intPtr2 = intPtr1 + 2;

        checked
        {
            intPtr2 = unchecked(intPtr1 + 2);
        }
    }
}",
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.OperatorThrowsRule).WithLocation(0).WithArguments("+"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.OperatorThrowsRule).WithLocation(1).WithArguments("-")).RunAsync();
        }

        [Fact]
        public async Task IntPtrExplicitConversion()
        {
            await PopulateTestCs(@"
using System;

class Program
{
    IntPtr intPtr1 = IntPtr.Zero;
    
    public unsafe void M1(int int1, IntPtr intPtr2)
    {
        void* ptr = null;
        long long1 = 0;

        checked
        {
            ptr = {|#0:(void*)intPtr1|}; // Starting with .NET 7 the explicit conversion '(void*)IntPtr' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.

            intPtr2 = {|#1:(IntPtr)ptr|}; // Starting with .NET 7 the explicit conversion '(IntPtr)void*' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.

            long1 = (int)intPtr2;
            intPtr1 = (IntPtr)long1;
            intPtr1 = (IntPtr)int1;
            short s1 = (short)intPtr1;
        }

        ptr = (void*)intPtr1;
        intPtr2 = (IntPtr)ptr;
        intPtr1 = (IntPtr)int1;
        short s = (short)intPtr1;

        intPtr1 = {|#2:(IntPtr)long1|}; // Starting with .NET 7 the explicit conversion '(IntPtr)Int64' will not throw when overflowing in an unchecked context. Wrap the expression with a 'checked' statement to restore the .NET 6 behavior.

        int a = {|#3:(int)intPtr1|}; // Starting with .NET 7 the explicit conversion '(Int32)IntPtr' will not throw when overflowing in an unchecked context. Wrap the expression with a 'checked' statement to restore the .NET 6 behavior.
    }
}",
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.ConversionThrowsRule).WithLocation(0).WithArguments("(void*)IntPtr"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.ConversionThrowsRule).WithLocation(1).WithArguments("(IntPtr)void*"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.ConversionNotThrowRule).WithLocation(2).WithArguments("(IntPtr)Int64"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.ConversionNotThrowRule).WithLocation(3).WithArguments("(Int32)IntPtr")).RunAsync();
        }

        [Fact]
        public async Task IntPtrExplicitConversionToFromDifferentPointerTypes()
        {
            await PopulateTestCs(@"
using System;

class Program
{
    IntPtr intPtr1 = IntPtr.Zero;
    
    public unsafe void M1()
    {
        void** voidPtr = null;
        byte*** bytePtr = null;

        checked
        {
            voidPtr = {|#0:(void**)intPtr1|}; // Starting with .NET 7 the explicit conversion '(void**)IntPtr' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.
            intPtr1 = {|#1:(IntPtr)voidPtr|}; // Starting with .NET 7 the explicit conversion '(IntPtr)void**' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.
            bytePtr = {|#2:(byte***)intPtr1|}; // Starting with .NET 7 the explicit conversion '(byte***)IntPtr' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.
            intPtr1 = {|#3:(IntPtr)bytePtr|}; // Starting with .NET 7 the explicit conversion '(IntPtr)byte***' will throw when overflowing in a checked context. Wrap the expression with an 'unchecked' statement to restore the .NET 6 behavior.
        }

        voidPtr = (void**)intPtr1;
        intPtr1 = (IntPtr)voidPtr;
        bytePtr = (byte***)intPtr1;
        intPtr1 = (IntPtr)bytePtr;
    }
}",
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.ConversionThrowsRule).WithLocation(0).WithArguments("(void**)IntPtr"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.ConversionThrowsRule).WithLocation(1).WithArguments("(IntPtr)void**"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.ConversionThrowsRule).WithLocation(2).WithArguments("(byte***)IntPtr"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.ConversionThrowsRule).WithLocation(3).WithArguments("(IntPtr)byte***")).RunAsync();
        }

        [Fact]
        public async Task UIntPtrExplicitConversion()
        {
            await PopulateTestCs(@"
using System;

class Program
{
    UIntPtr uintPtr2;
    
    public unsafe void M1(UIntPtr uintPtr1, ulong uLongValue)
    {
        void* ptr = null;
        long longValue = 0;

        checked
        {
            uintPtr1 = (UIntPtr)uLongValue;
            uint uint1 = (uint)uintPtr1;
            ptr = (void*)uintPtr1; 
            uintPtr2 = (UIntPtr)ptr;
        }

        uintPtr1 = (UIntPtr)longValue;
        int a = (int)uintPtr1;

        uintPtr1 = {|#0:(UIntPtr)uLongValue|}; // Starting with .NET 7 the explicit conversion '(UIntPtr)UInt64' will not throw when overflowing in an unchecked context. Wrap the expression with a 'checked' statement to restore the .NET 6 behavior.

        uint ui = {|#1:(uint)uintPtr1|}; // Starting with .NET 7 the explicit conversion '(UInt32)UIntPtr' will not throw when overflowing in an unchecked context. Wrap the expression with a 'checked' statement to restore the .NET 6 behavior.
    }
}",
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.ConversionNotThrowRule).WithLocation(0).WithArguments("(UIntPtr)UInt64"),
            VerifyCS.Diagnostic(PreventNumericIntPtrUIntPtrBehavioralChanges.ConversionNotThrowRule).WithLocation(1).WithArguments("(UInt32)UIntPtr")).RunAsync();
        }

        private static VerifyCS.Test PopulateTestCs(string sourceCode, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test
            {
                TestCode = sourceCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp11
            };
            test.ExpectedDiagnostics.AddRange(expected);
            return test;
        }
    }
}
