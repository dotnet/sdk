// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Globalization;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumsShouldHavePluralNamesAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpEnumsShouldHavePluralNamesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumsShouldHavePluralNamesAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicEnumsShouldHavePluralNamesFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class EnumsShouldHavePluralNamesTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new EnumsShouldHavePluralNamesAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new EnumsShouldHavePluralNamesAnalyzer();
        }

        [Fact]
        public void CA1714_CA1717_Test_EnumWithNoFlags_SingularName()
        {
            VerifyCSharp(@" 
                            public class A 
                            { 
                               public enum Day 
                                {
                                    Sunday = 0,
                                    Monday = 1,
                                    Tuesday = 2
                                       
                                };
                            }"
                          );

            VerifyBasic(@"
                        Public Class A
	                        Public Enum Day
		                           Sunday = 0
		                           Monday = 1
		                           Tuesday = 2

	                        End Enum
                        End Class
                        ");
        }

        [Fact]
        public void CA1714_CA1717__Test_EnumWithNoFlags_PluralName()
        {
            VerifyCSharp(@" 
                            public class A 
                            { 
                               public enum Days 
                                {
                                    sunday = 0,
                                    Monday = 1,
                                    Tuesday = 2
                                       
                                };
                            }",
                            GetCSharpNoPluralResultAt(4, 44));

            VerifyBasic(@"
                        Public Class A
	                        Public Enum Days
		                           Sunday = 0
		                           Monday = 1
		                           Tuesday = 2

	                        End Enum
                        End Class
                        ",
                        GetBasicNoPluralResultAt(3, 38));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CA1714_CA1717__Test_EnumWithNoFlags_PluralName_Internal()
        {
            VerifyCSharp(@" 
class A 
{ 
    enum Days 
    {
        sunday = 0,
        Monday = 1,
        Tuesday = 2
                                       
    };
}

public class A2
{ 
    private enum Days 
    {
        sunday = 0,
        Monday = 1,
        Tuesday = 2
                                       
    };
}

internal class A3
{ 
    public enum Days 
    {
        sunday = 0,
        Monday = 1,
        Tuesday = 2
                                       
    };
}
");

            VerifyBasic(@"
Class A
	Private Enum Days
		Sunday = 0
		Monday = 1
		Tuesday = 2
	End Enum
End Class

Public Class A2
	Private Enum Days
		Sunday = 0
		Monday = 1
		Tuesday = 2
	End Enum
End Class

Friend Class A3
	Public Enum Days
		Sunday = 0
		Monday = 1
		Tuesday = 2
	End Enum
End Class
");
        }

        [Fact]
        public void CA1714_CA1717__Test_EnumWithNoFlags_PluralName_UpperCase()
        {
            VerifyCSharp(@" 
                            public class A 
                            { 
                               public enum DAYS 
                                {
                                    sunday = 0,
                                    Monday = 1,
                                    Tuesday = 2
                                       
                                };
                            }",
                            GetCSharpNoPluralResultAt(4, 44));

            VerifyBasic(@"
                        Public Class A
	                        Public Enum DAYS
		                           Sunday = 0
		                           Monday = 1
		                           Tuesday = 2

	                        End Enum
                        End Class
                        ",
                        GetBasicNoPluralResultAt(3, 38));
        }

        [Fact]
        public void CA1714_CA1717_Test_EnumWithFlags_SingularName()
        {
            VerifyCSharp(@" 
                            public class A 
                            { 
                               [System.Flags] 
                               public enum Day 
                               {
                                    sunday = 0,
                                    Monday = 1,
                                    Tuesday = 2
                                       
                                };
                            }",
                            GetCSharpPluralResultAt(5, 44));

            VerifyBasic(@"
                       Public Class A
	                    <System.Flags> _
	                    Public Enum Day
		                    Sunday = 0
		                    Monday = 1
		                    Tuesday = 2
	                    End Enum
                        End Class",
                            GetBasicPluralResultAt(4, 34));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CA1714_CA1717_Test_EnumWithFlags_SingularName_Internal()
        {
            VerifyCSharp(@" 
class A 
{ 
    [System.Flags] 
    enum Day 
    {
        sunday = 0,
        Monday = 1,
        Tuesday = 2
                                       
    }
}

public class A2
{ 
    [System.Flags] 
    private enum Day 
    {
        sunday = 0,
        Monday = 1,
        Tuesday = 2
                                       
    }
}

internal class A3
{ 
    [System.Flags] 
    public enum Day 
    {
        sunday = 0,
        Monday = 1,
        Tuesday = 2
                                       
    }
}
");

            VerifyBasic(@"
Class A
    <System.Flags> _
    Enum Day
	    Sunday = 0
	    Monday = 1
	    Tuesday = 2
    End Enum
End Class

Public Class A2
    <System.Flags> _
    Private Enum Day
	    Sunday = 0
	    Monday = 1
	    Tuesday = 2
    End Enum
End Class

Friend Class A3
    <System.Flags> _
    Public Enum Day
	    Sunday = 0
	    Monday = 1
	    Tuesday = 2
    End Enum
End Class
");
        }

        [Fact]
        public void CA1714_CA1717_Test_EnumWithFlags_PluralName()
        {
            VerifyCSharp(@" 
                            public class A 
                            { 
                               [System.Flags] 
                               public enum Days 
                               {
                                    sunday = 0,
                                    Monday = 1,
                                    Tuesday = 2
                                       
                                };
                            }");

            VerifyBasic(@"
                       Public Class A
	                    <System.Flags> _
	                    Public Enum Days
		                    Sunday = 0
		                    Monday = 1
		                    Tuesday = 2
	                    End Enum
                        End Class");
        }

        [Fact]
        public void CA1714_CA1717_Test_EnumWithFlags_PluralName_UpperCase()
        {
            VerifyCSharp(@" 
                            public class A 
                            { 
                               [System.Flags] 
                               public enum DAYS 
                               {
                                    sunday = 0,
                                    Monday = 1,
                                    Tuesday = 2
                                       
                                };
                            }");

            VerifyBasic(@"
                       Public Class A
	                    <System.Flags> _
	                    Public Enum DAYS
		                    Sunday = 0
		                    Monday = 1
		                    Tuesday = 2
	                    End Enum
                        End Class");
        }

        [Fact, WorkItem(1323, "https://github.com/dotnet/roslyn-analyzers/issues/1323")]
        public void CA1714_CA1717_Test_EnumWithFlags_NonPluralNameEndsWithS()
        {
            VerifyCSharp(@" 
                            public class A 
                            { 
                               [System.Flags] 
                               public enum Axis 
                               {
                                    x = 0,
                                    y = 1,
                                    z = 2
                                       
                                };
                            }",
                            GetCSharpPluralResultAt(5, 44));

            VerifyBasic(@"
                       Public Class A
	                    <System.Flags> _
	                    Public Enum Axis
		                    x = 0
		                    y = 1
		                    z = 2
	                    End Enum
                        End Class",
                        GetBasicPluralResultAt(4, 34));
        }

        [Fact, WorkItem(1323, "https://github.com/dotnet/roslyn-analyzers/issues/1323")]
        public void CA1714_CA1717_Test_EnumWithFlags_PluralNameEndsWithS()
        {
            VerifyCSharp(@" 
                            public class A 
                            { 
                               [System.Flags] 
                               public enum Axes 
                               {
                                    x = 0,
                                    y = 1,
                                    z = 2
                                       
                                };
                            }");

            VerifyBasic(@"
                       Public Class A
	                    <System.Flags> _
	                    Public Enum Axes
		                    x = 0
		                    y = 1
		                    z = 2
	                    End Enum
                        End Class");
        }

        [Fact, WorkItem(1323, "https://github.com/dotnet/roslyn-analyzers/issues/1323")]
        public void CA1714_CA1717_Test_EnumWithFlags_PluralName_NotEndingWithS()
        {
            VerifyCSharp(@" 
                            public class A 
                            { 
                               [System.Flags] 
                               public enum Men 
                               {
                                    M1 = 0,
                                    M2 = 1,
                                    M3 = 2
                                       
                                };
                            }");

            VerifyBasic(@"
                       Public Class A
                        < System.Flags > _
                        Public Enum Men
                            M1 = 0
                            M2 = 1
                            M3 = 2
                        End Enum
                        End Class");
        }

        [Fact, WorkItem(1323, "https://github.com/dotnet/roslyn-analyzers/issues/1323")]
        public void CA1714_CA1717_Test_EnumWithNoFlags_PluralWord_NotEndingWithS()
        {
            VerifyCSharp(@" 
                            public class A 
                            { 
                               public enum Men 
                               {
                                    M1 = 0,
                                    M2 = 1,
                                    M3 = 2
                                       
                                };
                            }",
                            GetCSharpNoPluralResultAt(4, 44));

            VerifyBasic(@"
                       Public Class A
                        Public Enum Men
                            M1 = 0
                            M2 = 1
                            M3 = 2
                        End Enum
                        End Class",
                        GetBasicNoPluralResultAt(3, 37));
        }

        [Fact, WorkItem(1323, "https://github.com/dotnet/roslyn-analyzers/issues/1323")]
        public void CA1714_CA1717_Test_EnumWithNoFlags_irregularPluralWord_EndingWith_ae()
        {
            // Humanizer does not recognize 'formulae' as plural, but we skip words ending with 'ae'
            VerifyCSharp(@" 
                            public class A 
                            { 
                               [System.Flags] 
                               public enum formulae 
                               {
                                    M1 = 0,
                                    M2 = 1,
                                    M3 = 2

                                };
                            }");

            VerifyBasic(@"
                       Public Class A
                        < System.Flags > _
                        Public Enum formulae
                            M1 = 0
                            M2 = 1
                            M3 = 2
                        End Enum
                        End Class");
        }

        [Fact, WorkItem(1323, "https://github.com/dotnet/roslyn-analyzers/issues/1323")]
        public void CA1714_CA1717_Test_EnumWithNoFlags_irregularPluralWord_EndingWith_i()
        {
            // Humanizer does not recognize 'trophi' as plural, but we skip words ending with 'i'
            VerifyCSharp(@" 
                            public class A 
                            { 
                               [System.Flags] 
                               public enum trophi 
                               {
                                    M1 = 0,
                                    M2 = 1,
                                    M3 = 2
                                       
                                };
                            }");

            VerifyBasic(@"
                       Public Class A
                        < System.Flags > _
                        Public Enum trophi
                            M1 = 0
                            M2 = 1
                            M3 = 2
                        End Enum
                        End Class");
        }

        [Fact, WorkItem(1323, "https://github.com/dotnet/roslyn-analyzers/issues/1323")]
        public void CA1714_CA1717_Test_EnumWithNoFlags_NonAscii()
        {
            // We skip non-ASCII names.
            VerifyCSharp(@" 
                            public class A 
                            { 
                               [System.Flags] 
                               public enum UnicodeNameΔ
                               {
                                    M1 = 0,
                                    M2 = 1,
                                    M3 = 2

                                };
                            }");

            VerifyBasic(@"
                       Public Class A
                        < System.Flags > _
                        Public Enum UnicodeNameΔ
                            M1 = 0
                            M2 = 1
                            M3 = 2
                        End Enum
                        End Class");
        }

        [Theory, WorkItem(2229, "https://github.com/dotnet/roslyn-analyzers/issues/2229")]
        [InlineData("en-US")]
        [InlineData("es-ES")]
        [InlineData("pl-PL")]
        [InlineData("fi-FI")]
        [InlineData("de-DE")]
        public void CA1714_CA1717__Test_EnumWithNoFlags_PluralName_MultipleCultures(string culture)
        {
            var currentCulture = CultureInfo.DefaultThreadCurrentCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo(culture);

            VerifyCSharp(@" 
                            public class A 
                            { 
                               public enum Days 
                                {
                                    sunday = 0,
                                    Monday = 1,
                                    Tuesday = 2
                                       
                                };
                            }",
                            GetCSharpNoPluralResultAt(4, 44));

            VerifyBasic(@"
                        Public Class A
	                        Public Enum Days
		                           Sunday = 0
		                           Monday = 1
		                           Tuesday = 2

	                        End Enum
                        End Class
                        ",
                        GetBasicNoPluralResultAt(3, 38));

            CultureInfo.DefaultThreadCurrentCulture = currentCulture;
        }

        private static DiagnosticResult GetCSharpPluralResultAt(int line, int column)
        {
            return GetCSharpResultAt(line, column, EnumsShouldHavePluralNamesAnalyzer.RuleId_Plural, MicrosoftCodeQualityAnalyzersResources.FlagsEnumsShouldHavePluralNamesMessage);
        }

        private static DiagnosticResult GetBasicPluralResultAt(int line, int column)
        {
            return GetBasicResultAt(line, column, EnumsShouldHavePluralNamesAnalyzer.RuleId_Plural, MicrosoftCodeQualityAnalyzersResources.FlagsEnumsShouldHavePluralNamesMessage);
        }

        private static DiagnosticResult GetCSharpNoPluralResultAt(int line, int column)
        {
            return GetCSharpResultAt(line, column, EnumsShouldHavePluralNamesAnalyzer.RuleId_NoPlural, MicrosoftCodeQualityAnalyzersResources.OnlyFlagsEnumsShouldHavePluralNamesMessage);
        }

        private static DiagnosticResult GetBasicNoPluralResultAt(int line, int column)
        {
            return GetBasicResultAt(line, column, EnumsShouldHavePluralNamesAnalyzer.RuleId_NoPlural, MicrosoftCodeQualityAnalyzersResources.OnlyFlagsEnumsShouldHavePluralNamesMessage);
        }
    }
}
