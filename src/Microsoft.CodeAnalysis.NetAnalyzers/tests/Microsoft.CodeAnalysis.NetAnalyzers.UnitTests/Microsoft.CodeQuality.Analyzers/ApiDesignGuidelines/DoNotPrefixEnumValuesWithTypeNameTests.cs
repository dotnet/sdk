// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.UnitTests.ApiDesignGuidelines
{
    public class DoNotPrefixEnumValuesWithTypeNameTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DoNotPrefixEnumValuesWithTypeNameAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DoNotPrefixEnumValuesWithTypeNameAnalyzer();
        }

        [Fact]
        public void CSharp_NoDiagnostic_NoPrefix()
        {
            VerifyCSharp(@" 
                class A
                { 
                    enum State
                    {
                        Ok = 0,
                        Error = 1,
                        Unknown = 2
                    };
                }");
        }

        [Fact]
        public void Basic_NoDiagnostic_NoPrefix()
        {
            VerifyBasic(@"
                Class A
                    Private Enum State
                        Ok = 0
                        Err = 1
                        Unknown = 2
                    End Enum
                End Class");
        }

        [Fact]
        public void CSharp_Diagnostic_EachValuePrefixed()
        {
            VerifyCSharp(@"
                class A
                {
                    enum State
                    {
                        StateOk = 0,
                        StateError = 1,
                        StateUnknown = 2
                    };
                }",
                GetCSharpResultAt(6, 25, DoNotPrefixEnumValuesWithTypeNameAnalyzer.Rule, "State"),
                GetCSharpResultAt(7, 25, DoNotPrefixEnumValuesWithTypeNameAnalyzer.Rule, "State"),
                GetCSharpResultAt(8, 25, DoNotPrefixEnumValuesWithTypeNameAnalyzer.Rule, "State"));
        }

        [Fact]
        public void Basic_Diagnostic_EachValuePrefixed()
        {
            VerifyBasic(@"
                Class A
                    Private Enum State
                        StateOk = 0
                        StateErr = 1
                        StateUnknown = 2
                    End Enum
                End Class
                ",
                GetBasicResultAt(4, 25, DoNotPrefixEnumValuesWithTypeNameAnalyzer.Rule, "State"),
                GetBasicResultAt(5, 25, DoNotPrefixEnumValuesWithTypeNameAnalyzer.Rule, "State"),
                GetBasicResultAt(6, 25, DoNotPrefixEnumValuesWithTypeNameAnalyzer.Rule, "State"));
        }

        [Fact]
        public void CSharp_NoDiagnostic_HalfOfValuesPrefixed()
        {
            VerifyCSharp(@"
                class A
                {
                    enum State
                    {
                        Ok = 0,
                        StateError = 1,
                        StateUnknown = 2,
                        Invalid = 3
                    };
                }");
        }

        [Fact]
        public void CSharp_Diagnostic_ThreeOfFourValuesPrefixed()
        {
            VerifyCSharp(@"
                class A
                {
                    enum State
                    {
                        StateOk = 0,
                        StateError = 1,
                        StateUnknown = 2,
                        Invalid = 3
                    };
                }",
                GetCSharpResultAt(6, 25, DoNotPrefixEnumValuesWithTypeNameAnalyzer.Rule, "State"),
                GetCSharpResultAt(7, 25, DoNotPrefixEnumValuesWithTypeNameAnalyzer.Rule, "State"),
                GetCSharpResultAt(8, 25, DoNotPrefixEnumValuesWithTypeNameAnalyzer.Rule, "State"));
        }

        [Fact]
        public void CSharp_Diagnostic_PrefixCaseDiffers()
        {
            VerifyCSharp(@"
                class A
                {
                    enum State
                    {
                        stateOk = 0
                    };
                }",
                GetCSharpResultAt(6, 25, DoNotPrefixEnumValuesWithTypeNameAnalyzer.Rule, "State"));
        }

        [Fact]
        public void CSharp_NoDiagnostic_EmptyEnum()
        {
            VerifyCSharp(@"
                class A
                {
                    enum State
                    {
                    };
                }");
        }
    }
}
