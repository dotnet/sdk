// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.CodeMetrics.UnitTests
{
    public class CodeMetricsAnalyzerTests : DiagnosticAnalyzerTestBase
    {
        #region CA1501: Avoid excessive inheritance

        [Fact]
        public void CA1501_CSharp_VerifyDiagnostic()
        {
            var source = @"
class BaseClass { }
class FirstDerivedClass : BaseClass { }
class SecondDerivedClass : FirstDerivedClass { }
class ThirdDerivedClass : SecondDerivedClass { }
class FourthDerivedClass : ThirdDerivedClass { }

// This class violates the rule.
class FifthDerivedClass : FourthDerivedClass { }
";
            DiagnosticResult[] expected = new[] {
                // Test0.cs(9, 7): warning CA1501: 'FifthDerivedClass' has an object hierarchy '6' levels deep within the defining module. If possible, eliminate base classes within the hierarchy to decrease its hierarchy level below '6': 'FourthDerivedClass, ThirdDerivedClass, SecondDerivedClass, FirstDerivedClass, BaseClass, Object'
                GetCSharpCA1501ExpectedDiagnostic(9, 7, "FifthDerivedClass", 6, 6, "FourthDerivedClass, ThirdDerivedClass, SecondDerivedClass, FirstDerivedClass, BaseClass, Object")};
            VerifyCSharp(source, expected);
        }

        [Fact]
        public void CA1501_Basic_VerifyDiagnostic()
        {
            var source = @"
Class BaseClass
End Class

Class FirstDerivedClass
    Inherits BaseClass
End Class

Class SecondDerivedClass
    Inherits FirstDerivedClass
End Class

Class ThirdDerivedClass
    Inherits SecondDerivedClass
End Class

Class FourthDerivedClass
    Inherits ThirdDerivedClass
End Class

Class FifthDerivedClass
    Inherits FourthDerivedClass
End Class
";
            DiagnosticResult[] expected = new[] {
                // Test0.vb(21, 7): warning CA1501: 'FifthDerivedClass' has an object hierarchy '6' levels deep within the defining module. If possible, eliminate base classes within the hierarchy to decrease its hierarchy level below '6': 'FourthDerivedClass, ThirdDerivedClass, SecondDerivedClass, FirstDerivedClass, BaseClass, Object'
                GetBasicCA1501ExpectedDiagnostic(21, 7, "FifthDerivedClass", 6, 6, "FourthDerivedClass, ThirdDerivedClass, SecondDerivedClass, FirstDerivedClass, BaseClass, Object")};
            VerifyBasic(source, expected);
        }

        [Fact]
        public void CA1501_Configuration_CSharp_VerifyDiagnostic()
        {
            var source = @"
class BaseClass { }
class FirstDerivedClass : BaseClass { }
";
            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1501: 1
";
            DiagnosticResult[] expected = new[] {
                // Test0.cs(3, 7): warning CA1501: 'BaseClass' has an object hierarchy '2' levels deep within the defining module. If possible, eliminate base classes within the hierarchy to decrease its hierarchy level below '2': 'BaseClass, Object'
                GetCSharpCA1501ExpectedDiagnostic(3, 7, "FirstDerivedClass", 2, 2, "BaseClass, Object")};
            VerifyCSharp(source, GetAdditionalFile(additionalText), expected);
        }

        [Fact]
        public void CA1501_Configuration_Basic_VerifyDiagnostic()
        {
            var source = @"
Class BaseClass
End Class

Class FirstDerivedClass
    Inherits BaseClass
End Class
";
            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1501: 1
";
            DiagnosticResult[] expected = new[] {
                // Test0.vb(5, 7): warning CA1501: 'BaseClass' has an object hierarchy '2' levels deep within the defining module. If possible, eliminate base classes within the hierarchy to decrease its hierarchy level below '2': 'BaseClass, Object'
                GetBasicCA1501ExpectedDiagnostic(5, 7, "FirstDerivedClass", 2, 2, "BaseClass, Object")};
            VerifyBasic(source, GetAdditionalFile(additionalText), expected);
        }

        #endregion

        #region CA1502: Avoid excessive complexity

        [Fact]
        public void CA1502_CSharp_VerifyDiagnostic()
        {
            var source = @"
class C
{
    void M(bool b)
    {
        // Default threshold = 25
        var x = b && b && b && b && b && b && b &&
            b && b && b && b && b && b && b &&
            b && b && b && b && b && b && b &&
            b && b && b && b && b && b && b;
    }
}
";
            DiagnosticResult[] expected = new[] {
                // Test0.cs(4,10): warning CA1502: 'M' has a cyclomatic complexity of '28'. Rewrite or refactor the code to decrease its complexity below '26'.
                GetCSharpCA1502ExpectedDiagnostic(4, 10, "M", 28, 26)};
            VerifyCSharp(source, expected);
        }

        [Fact]
        public void CA1502_Basic_VerifyDiagnostic()
        {
            var source = @"
Class C
    Private Sub M(ByVal b As Boolean)
        Dim x = b AndAlso b AndAlso b AndAlso b AndAlso b AndAlso b AndAlso
            b AndAlso b AndAlso b AndAlso b AndAlso b AndAlso b AndAlso b AndAlso
            b AndAlso b AndAlso b AndAlso b AndAlso b AndAlso b AndAlso b AndAlso
            b AndAlso b AndAlso b AndAlso b AndAlso b AndAlso b AndAlso b AndAlso b
    End Sub
End Class
";
            DiagnosticResult[] expected = new[] {
                // Test0.vb(3,17): warning CA1502: 'M' has a cyclomatic complexity of '28'. Rewrite or refactor the code to decrease its complexity below '26'.
                GetBasicCA1502ExpectedDiagnostic(3, 17, "M", 28, 26)};
            VerifyBasic(source, expected);
        }

        [Fact]
        public void CA1502_Configuration_CSharp_VerifyDiagnostic()
        {
            var source = @"
class C
{
    void M1(bool b)
    {
        var x = b && b && b && b;
    }

    void M2(bool b)
    {
        var x = b && b;
    }
}
";

            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1502: 2
";
            DiagnosticResult[] expected = new[] {
                // Test0.cs(4,10): warning CA1502: 'M1' has a cyclomatic complexity of '4'. Rewrite or refactor the code to decrease its complexity below '3'.                
                GetCSharpCA1502ExpectedDiagnostic(4, 10, "M1", 4, 3)};
            VerifyCSharp(source, GetAdditionalFile(additionalText), expected);
        }

        [Fact]
        public void CA1502_Configuration_Basic_VerifyDiagnostic()
        {
            var source = @"
Class C
    Private Sub M1(ByVal b As Boolean)
        Dim x = b AndAlso b AndAlso b AndAlso b
    End Sub

    Private Sub M2(ByVal b As Boolean)
        Dim x = b AndAlso b
    End Sub
End Class
";
            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1502: 2
";
            DiagnosticResult[] expected = new[] {
                // Test0.vb(3,17): warning CA1502: 'M1' has a cyclomatic complexity of '4'. Rewrite or refactor the code to decrease its complexity below '3'.
                GetBasicCA1502ExpectedDiagnostic(3, 17, "M1", 4, 3)};
            VerifyBasic(source, GetAdditionalFile(additionalText), expected);
        }

        [Fact]
        public void CA1502_SymbolBasedConfiguration_CSharp_VerifyDiagnostic()
        {
            var source = @"
class C
{
    void M1(bool b)
    {
        var x = b && b && b && b;
    }

    void M2(bool b)
    {
        var x = b && b;
    }
}
";

            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1502(Type): 4
CA1502(Method): 2
";
            DiagnosticResult[] expected = new[] {
                // Test0.cs(2,7): warning CA1502: 'C' has a cyclomatic complexity of '6'. Rewrite or refactor the code to decrease its complexity below '5'.                
                GetCSharpCA1502ExpectedDiagnostic(2, 7, "C", 6, 5),
                // Test0.cs(4,10): warning CA1502: 'M1' has a cyclomatic complexity of '4'. Rewrite or refactor the code to decrease its complexity below '3'.
                GetCSharpCA1502ExpectedDiagnostic(4, 10, "M1", 4, 3)};
            VerifyCSharp(source, GetAdditionalFile(additionalText), expected);
        }

        [Fact]
        public void CA1502_SymbolBasedConfiguration_Basic_VerifyDiagnostic()
        {
            var source = @"
Class C
    Private Sub M1(ByVal b As Boolean)
        Dim x = b AndAlso b AndAlso b AndAlso b
    End Sub

    Private Sub M2(ByVal b As Boolean)
        Dim x = b AndAlso b
    End Sub
End Class
";
            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1502(Type): 4
CA1502(Method): 2
";
            DiagnosticResult[] expected = new[] {
                // Test0.vb(2,7): warning CA1502: 'C' has a cyclomatic complexity of '6'. Rewrite or refactor the code to decrease its complexity below '5'.
                GetBasicCA1502ExpectedDiagnostic(2, 7, "C", 6, 5),
                // Test0.vb(3,17): warning CA1502: 'M1' has a cyclomatic complexity of '4'. Rewrite or refactor the code to decrease its complexity below '3'.
                GetBasicCA1502ExpectedDiagnostic(3, 17, "M1", 4, 3)};
            VerifyBasic(source, GetAdditionalFile(additionalText), expected);
        }

        #endregion

        #region CA1505: Avoid unmaintainable code

        [Fact]
        public void CA1505_Configuration_CSharp_VerifyDiagnostic()
        {
            var source = @"
class C
{
    void M1(bool b)
    {
        var x = b && b && b && b;
    }
}
";

            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1505: 95
";
            DiagnosticResult[] expected = new[] {
                // Test0.cs(2,7): warning CA1505: 'C' has a maintainability index of '91'. Rewrite or refactor the code to increase its maintainability index (MI) above '94'.
                GetCSharpCA1505ExpectedDiagnostic(2, 7, "C", 91, 94),
                // Test0.cs(4,10): warning CA1505: 'M1' has a maintainability index of '91'. Rewrite or refactor the code to increase its maintainability index (MI) above '94'.
                GetCSharpCA1505ExpectedDiagnostic(4, 10, "M1", 91, 94)};
            VerifyCSharp(source, GetAdditionalFile(additionalText), expected);
        }

        [Fact]
        public void CA1505_Configuration_Basic_VerifyDiagnostic()
        {
            var source = @"
Class C
    Private Sub M1(ByVal b As Boolean)
        Dim x = b AndAlso b AndAlso b AndAlso b
    End Sub
End Class
";

            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1505: 95
";
            DiagnosticResult[] expected = new[] {
                // Test0.vb(2,7): warning CA1505: 'C' has a maintainability index of '91'. Rewrite or refactor the code to increase its maintainability index (MI) above '94'.
                GetBasicCA1505ExpectedDiagnostic(2, 7, "C", 91, 94),
                // Test0.vb(3,17): warning CA1505: 'M1' has a maintainability index of '91'. Rewrite or refactor the code to increase its maintainability index (MI) above '94'.
                GetBasicCA1505ExpectedDiagnostic(3, 17, "M1", 91, 94)};
            VerifyBasic(source, GetAdditionalFile(additionalText), expected);
        }

        [Fact]
        public void CA1505_SymbolBasedConfiguration_CSharp_VerifyDiagnostic()
        {
            var source = @"
class C
{
    void M1(bool b)
    {
        var x = b && b && b && b;
    }
}
";

            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1505(Type): 95
";
            DiagnosticResult[] expected = new[] {
                // Test0.cs(2,7): warning CA1505: 'C' has a maintainability index of '91'. Rewrite or refactor the code to increase its maintainability index (MI) above '94'.
                GetCSharpCA1505ExpectedDiagnostic(2, 7, "C", 91, 94)};
            VerifyCSharp(source, GetAdditionalFile(additionalText), expected);
        }

        [Fact]
        public void CA1505_SymbolBasedConfiguration_Basic_VerifyDiagnostic()
        {
            var source = @"
Class C
    Private Sub M1(ByVal b As Boolean)
        Dim x = b AndAlso b AndAlso b AndAlso b
    End Sub
End Class
";

            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1505(Type): 95
";
            DiagnosticResult[] expected = new[] {
                // Test0.vb(2,7): warning CA1505: 'C' has a maintainability index of '91'. Rewrite or refactor the code to increase its maintainability index (MI) above '94'.
                GetBasicCA1505ExpectedDiagnostic(2, 7, "C", 91, 94)};
            VerifyBasic(source, GetAdditionalFile(additionalText), expected);
        }

        #endregion

        #region CA1506: Avoid excessive class coupling

        [Fact]
        public void CA1506_Configuration_CSharp_VerifyDiagnostic()
        {
            var source = @"
class C
{
    void M1(C1 c1, C2 c2, C3 c3, N.C4 c4)
    {
    }
}

class C1 { }
class C2 { }
class C3 { }
namespace N { class C4 { } }
";
            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1506: 2
";
            DiagnosticResult[] expected = new[] {
                // Test0.cs(2,7): warning CA1506: 'C' is coupled with '4' different types from '2' different namespaces. Rewrite or refactor the code to decrease its class coupling below '3'.
                GetCSharpCA1506ExpectedDiagnostic(2, 7, "C", 4, 2, 3),
                // Test0.cs(4,10): warning CA1506: 'M1' is coupled with '4' different types from '2' different namespaces. Rewrite or refactor the code to decrease its class coupling below '3'.
                GetCSharpCA1506ExpectedDiagnostic(4, 10, "M1", 4, 2, 3)};
            VerifyCSharp(source, GetAdditionalFile(additionalText), expected);
        }

        [Fact]
        public void CA1506_Configuration_Basic_VerifyDiagnostic()
        {
            var source = @"
Class C
    Private Sub M1(c1 As C1, c2 As C2, c3 As C3, c4 As N.C4)
    End Sub
End Class

Class C1
End Class

Class C2
End Class

Class C3
End Class

Namespace N
    Class C4
    End Class
End Namespace
";
            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1506: 2
";
            DiagnosticResult[] expected = new[] {
                // Test0.vb(2,7): warning CA1506: 'C' is coupled with '4' different types from '2' different namespaces. Rewrite or refactor the code to decrease its class coupling below '3'.
                GetBasicCA1506ExpectedDiagnostic(2, 7, "C", 4, 2, 3),
                // Test0.vb(3,17): warning CA1506: 'M1' is coupled with '4' different types from '2' different namespaces. Rewrite or refactor the code to decrease its class coupling below '3'.
                GetBasicCA1506ExpectedDiagnostic(3, 17, "M1", 4, 2, 3)};
            VerifyBasic(source, GetAdditionalFile(additionalText), expected);
        }

        [Fact]
        public void CA1506_SymbolBasedConfiguration_CSharp_VerifyDiagnostic()
        {
            var source = @"
class C
{
    void M1(C1 c1, C2 c2, C3 c3, N.C4 c4)
    {
    }
}

class C1 { }
class C2 { }
class C3 { }
namespace N { class C4 { } }
";
            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1506(Method): 2
CA1506(Type): 10
";
            DiagnosticResult[] expected = new[] {
                // Test0.cs(4,10): warning CA1506: 'M1' is coupled with '4' different types from '2' different namespaces. Rewrite or refactor the code to decrease its class coupling below '3'.
                GetCSharpCA1506ExpectedDiagnostic(4, 10, "M1", 4, 2, 3)};
            VerifyCSharp(source, GetAdditionalFile(additionalText), expected);
        }

        [Fact]
        public void CA1506_SymbolBasedConfiguration_Basic_VerifyDiagnostic()
        {
            var source = @"
Class C
    Private Sub M1(c1 As C1, c2 As C2, c3 As C3, c4 As N.C4)
    End Sub
End Class

Class C1
End Class

Class C2
End Class

Class C3
End Class

Namespace N
    Class C4
    End Class
End Namespace
";
            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA1506(Method): 2
CA1506(Type): 10
";
            DiagnosticResult[] expected = new[] {
                // Test0.vb(3,17): warning CA1506: 'M1' is coupled with '4' different types from '2' different namespaces. Rewrite or refactor the code to decrease its class coupling below '3'.
                GetBasicCA1506ExpectedDiagnostic(3, 17, "M1", 4, 2, 3)};
            VerifyBasic(source, GetAdditionalFile(additionalText), expected);
        }

        #endregion

        #region CA1509: Invalid entry in code metrics rule specification file

        [Fact]
        public void CA1509_VerifyDiagnostics()
        {
            var source = @"";

            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

# 1. Multiple colons
CA1501: 1 : 2

# 2. Whitespace in RuleId
CA 1501: 1

# 3. Invalid Code Metrics RuleId
CA1600: 1

# 4. Non-integral Threshold.
CA1501: None

# 5. Not supported SymbolKind.
CA1501(Local): 1

# 6. Missing SymbolKind.
CA1501(: 1

# 7. Missing CloseParens after SymbolKind.
CA1501(Method: 1

# 8. Multiple SymbolKinds.
CA1501(Method)(Type): 1

# 9. Missing Threshold.
CA1501
";
            DiagnosticResult[] expected = new[] {
                // CodeMetricsConfig.txt(6,1): warning CA1509: Invalid entry 'CA1501: 1 : 2' in code metrics rule specification file 'CodeMetricsConfig.txt'
                GetCA1509ExpectedDiagnostic(6, 1, "CA1501: 1 : 2", AdditionalFileName),
                // CodeMetricsConfig.txt(9,1): warning CA1509: Invalid entry 'CA 1501: 1' in code metrics rule specification file 'CodeMetricsConfig.txt'
                GetCA1509ExpectedDiagnostic(9, 1, "CA 1501: 1", AdditionalFileName),
                // CodeMetricsConfig.txt(12,1): warning CA1509: Invalid entry 'CA1600: 1' in code metrics rule specification file 'CodeMetricsConfig.txt'
                GetCA1509ExpectedDiagnostic(12, 1, "CA1600: 1", AdditionalFileName),
                // CodeMetricsConfig.txt(15,1): warning CA1509: Invalid entry 'CA1501: None' in code metrics rule specification file 'CodeMetricsConfig.txt'
                GetCA1509ExpectedDiagnostic(15, 1, "CA1501: None", AdditionalFileName),
                // CodeMetricsConfig.txt(18,1): warning CA1509: Invalid entry 'CA1501(Local): 1' in code metrics rule specification file 'CodeMetricsConfig.txt'
                GetCA1509ExpectedDiagnostic(18, 1, "CA1501(Local): 1", AdditionalFileName),
                // CodeMetricsConfig.txt(21,1): warning CA1509: Invalid entry 'CA1501(: 1' in code metrics rule specification file 'CodeMetricsConfig.txt'
                GetCA1509ExpectedDiagnostic(21, 1, "CA1501(: 1", AdditionalFileName),
                // CodeMetricsConfig.txt(24,1): warning CA1509: Invalid entry 'CA1501(Method: 1' in code metrics rule specification file 'CodeMetricsConfig.txt'
                GetCA1509ExpectedDiagnostic(24, 1, "CA1501(Method: 1", AdditionalFileName),
                // CodeMetricsConfig.txt(27,1): warning CA1509: Invalid entry 'CA1501(Method)(Type): 1' in code metrics rule specification file 'CodeMetricsConfig.txt'
                GetCA1509ExpectedDiagnostic(27, 1, "CA1501(Method)(Type): 1", AdditionalFileName),
                // CodeMetricsConfig.txt(30,1): warning CA1509: Invalid entry 'CA1501' in code metrics rule specification file 'CodeMetricsConfig.txt'
                GetCA1509ExpectedDiagnostic(30, 1, "CA1501", AdditionalFileName)};
            VerifyCSharp(source, GetAdditionalFile(additionalText), expected);
        }

        [Fact]
        public void CA1509_NoDiagnostics()
        {
            var source = @"";

            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

# 1. Duplicates are allowed
CA1501: 1
CA1501: 2

# 2. Duplicate RuleId-SymbolKind pairs are allowed.
CA1501(Method): 1
CA1501(Method): 1

# 3. All valid symbol kinds
CA1502(Assembly): 1
CA1502(Namespace): 1
CA1502(Type): 1
CA1502(Method): 1
CA1502(Field): 1
CA1502(Property): 1
CA1502(Event): 1

# 4. Whitespaces before and after the key-value pair are allowed.
   CA1501: 1        

# 5. Whitespaces before and after the colon are allowed.
CA1501    :    1
";
            VerifyCSharp(source, GetAdditionalFile(additionalText));
        }

        [Fact]
        public void CA1509_VerifyNoMetricDiagnostics()
        {
            // Ensure we don't report any code metric diagnostics when we have invalid entries in code metrics configuration file.
            var source = @"
class BaseClass { }
class FirstDerivedClass : BaseClass { }
class SecondDerivedClass : FirstDerivedClass { }
class ThirdDerivedClass : SecondDerivedClass { }
class FourthDerivedClass : ThirdDerivedClass { }

// This class violates the CA1501 rule for default threshold.
class FifthDerivedClass : FourthDerivedClass { }";

            string additionalText = @"
# FORMAT:
# 'RuleId'(Optional 'SymbolKind'): 'Threshold'

CA 1501: 10
";
            DiagnosticResult[] expected = new[] {
                // CodeMetricsConfig.txt(5,1): warning CA1509: Invalid entry 'CA 1501: 10' in code metrics rule specification file 'CodeMetricsConfig.txt'
                GetCA1509ExpectedDiagnostic(5, 1, "CA 1501: 10", AdditionalFileName)};
            VerifyCSharp(source, GetAdditionalFile(additionalText), expected);
        }

        #endregion

        #region Helpers
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CodeMetricsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new CodeMetricsAnalyzer();
        }

        private static DiagnosticResult GetCSharpCA1501ExpectedDiagnostic(int line, int column, string symbolName, int metricValue, int threshold, string baseTypes)
        {
            return GetCA1501ExpectedDiagnostic(line, column, symbolName, metricValue, threshold, baseTypes);
        }

        private static DiagnosticResult GetBasicCA1501ExpectedDiagnostic(int line, int column, string symbolName, int metricValue, int threshold, string baseTypes)
        {
            return GetCA1501ExpectedDiagnostic(line, column, symbolName, metricValue, threshold, baseTypes);
        }

        private static DiagnosticResult GetCSharpCA1502ExpectedDiagnostic(int line, int column, string symbolName, int metricValue, int threshold)
        {
            return GetCA1502ExpectedDiagnostic(line, column, symbolName, metricValue, threshold);
        }

        private static DiagnosticResult GetBasicCA1502ExpectedDiagnostic(int line, int column, string symbolName, int metricValue, int threshold)
        {
            return GetCA1502ExpectedDiagnostic(line, column, symbolName, metricValue, threshold);
        }

        private static DiagnosticResult GetCSharpCA1505ExpectedDiagnostic(int line, int column, string symbolName, int metricValue, int threshold)
        {
            return GetCA1505ExpectedDiagnostic(line, column, symbolName, metricValue, threshold);
        }

        private static DiagnosticResult GetBasicCA1505ExpectedDiagnostic(int line, int column, string symbolName, int metricValue, int threshold)
        {
            return GetCA1505ExpectedDiagnostic(line, column, symbolName, metricValue, threshold);
        }

        private static DiagnosticResult GetCSharpCA1506ExpectedDiagnostic(int line, int column, string symbolName, int coupledTypesCount, int namespaceCount, int threshold)
        {
            return GetCA1506ExpectedDiagnostic(line, column, symbolName, coupledTypesCount, namespaceCount, threshold);
        }

        private static DiagnosticResult GetBasicCA1506ExpectedDiagnostic(int line, int column, string symbolName, int coupledTypesCount, int namespaceCount, int threshold)
        {
            return GetCA1506ExpectedDiagnostic(line, column, symbolName, coupledTypesCount, namespaceCount, threshold);
        }

        // '{0}' has an object hierarchy '{1}' levels deep within the defining module. If possible, eliminate base classes within the hierarchy to decrease its hierarchy level below '{2}': '{3}'
        private static DiagnosticResult GetCA1501ExpectedDiagnostic(int line, int column, string symbolName, int metricValue, int threshold, string baseTypes)
        {
            return new DiagnosticResult(CodeMetricsAnalyzer.CA1501RuleId, DiagnosticSeverity.Warning)
                .WithLocation(line, column)
                .WithMessageFormat(MicrosoftCodeQualityAnalyzersResources.AvoidExcessiveInheritanceMessage)
                .WithArguments(symbolName, metricValue, threshold, baseTypes);
        }

        // '{0}' has a cyclomatic complexity of '{1}'. Rewrite or refactor the code to decrease its complexity below '{2}'.
        private static DiagnosticResult GetCA1502ExpectedDiagnostic(int line, int column, string symbolName, int metricValue, int threshold)
        {
            return new DiagnosticResult(CodeMetricsAnalyzer.CA1502RuleId, DiagnosticSeverity.Warning)
                .WithLocation(line, column)
                .WithMessageFormat(MicrosoftCodeQualityAnalyzersResources.AvoidExcessiveComplexityMessage)
                .WithArguments(symbolName, metricValue, threshold);
        }

        // '{0}' has a maintainability index of '{1}'. Rewrite or refactor the code to increase its maintainability index (MI) above '{2}'.
        private static DiagnosticResult GetCA1505ExpectedDiagnostic(int line, int column, string symbolName, int metricValue, int threshold)
        {
            return new DiagnosticResult(CodeMetricsAnalyzer.CA1505RuleId, DiagnosticSeverity.Warning)
                .WithLocation(line, column)
                .WithMessageFormat(MicrosoftCodeQualityAnalyzersResources.AvoidUnmantainableCodeMessage)
                .WithArguments(symbolName, metricValue, threshold);
        }

        // '{0}' is coupled with '{1}' different types from '{2}' different namespaces. Rewrite or refactor the code to decrease its class coupling below '{3}'.
        private static DiagnosticResult GetCA1506ExpectedDiagnostic(int line, int column, string symbolName, int coupledTypesCount, int namespaceCount, int threshold)
        {
            return new DiagnosticResult(CodeMetricsAnalyzer.CA1506RuleId, DiagnosticSeverity.Warning)
                .WithLocation(line, column)
                .WithMessageFormat(MicrosoftCodeQualityAnalyzersResources.AvoidExcessiveClassCouplingMessage)
                .WithArguments(symbolName, coupledTypesCount, namespaceCount, threshold);
        }

        private static DiagnosticResult GetCA1509ExpectedDiagnostic(int line, int column, string entry, string additionalFile)
        {
            return new DiagnosticResult(CodeMetricsAnalyzer.CA1509RuleId, DiagnosticSeverity.Warning)
                .WithLocation(AdditionalFileName, line, column)
                .WithMessageFormat(MicrosoftCodeQualityAnalyzersResources.InvalidEntryInCodeMetricsConfigFileMessage)
                .WithArguments(entry, additionalFile);
        }

        private const string AdditionalFileName = "CodeMetricsConfig.txt";
        private FileAndSource GetAdditionalFile(string source)
            => new FileAndSource() { Source = source, FilePath = AdditionalFileName };

        #endregion
    }
}
