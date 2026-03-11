// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseCompositeFormatAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public partial class UseCompositeFormatTests
    {
        [Fact]
        public async Task LacksTargetTypes_NoDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                class C
                {
                    private static string StaticProperty { get; }

                    public void Test(System.Text.StringBuilder sb)
                    {
                        string.Format(StaticProperty, 42);
                        sb.AppendFormat(StaticProperty, 42);
                    }
                }
                """);
        }

        [Fact]
        public async Task ValidateAnalyzer()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = new CodeAnalysis.Testing.ReferenceAssemblies("doesntexist"),
                TestCode = Preamble + """
                    class C
                    {
                        private string InstanceProperty { get; }
                        public string InstanceField;
                        internal string InstanceMethod() => null;
                        internal string InstanceMethod(string arg) => null;
                
                        private static string StaticProperty { get; }
                        public static string StaticField;
                        internal static string StaticMethod() => null;
                        internal static string StaticMethod(string arg) => null;
                        internal static string StaticMethod(string arg1, string arg2) => null;

                        public void TestStringFormat(string format)
                        {
                            // No diagnostics for a literal
                            string.Format("Hello {0}", 42);
                            string.Format("Hello {0} {1}", 42, "something");
                            string.Format("Hello {0} {1} {2}", 42, "something", "else");
                            string.Format("Hello {0} {1} {2} {3}", new object[] { 42, "something", "else", 84 });
                            string.Format(null, "Hello {0}", 42);
                            string.Format(null, "Hello {0} {1}", 42, "something");
                            string.Format(null, "Hello {0} {1} {2}", 42, "something", "else");
                            string.Format(null, "Hello {0} {1} {2} {3}", new object[] { 42, "something", "else", 84 });
                            string.Format(CultureInfo.InvariantCulture, "Hello {0}", 42);
                            string.Format(CultureInfo.InvariantCulture, "Hello {0} {1}", 42, "something");
                            string.Format(CultureInfo.InvariantCulture, "Hello {0} {1} {2}", 42, "something", "else");
                            string.Format(CultureInfo.InvariantCulture, "Hello {0} {1} {2} {3}", new object[] { 42, "something", "else", 84 });

                            // No diagnostics because of an unsupported format string argument
                            string.Format(InstanceProperty, 42);
                            string.Format(InstanceField, 42);
                            string.Format(InstanceMethod(), 42);
                            string.Format(InstanceMethod(""), 42);
                            string.Format(format, 42);
                            string.Format(null, InstanceProperty, 42);
                            string.Format(null, InstanceField, 42);
                            string.Format(null, InstanceMethod(), 42);
                            string.Format(null, InstanceMethod(""), 42);
                            string.Format(null, format, 42);
                            string.Format(CultureInfo.InvariantCulture, InstanceProperty, 42);
                            string.Format(CultureInfo.InvariantCulture, InstanceField, 42);
                            string.Format(CultureInfo.InvariantCulture, InstanceMethod(), 42);
                            string.Format(CultureInfo.InvariantCulture, InstanceMethod(""), 42);
                            string.Format(CultureInfo.InvariantCulture, format, 42);
                            string.Format(CultureInfo.InvariantCulture, StaticMethod(InstanceMethod("")), 42);
                            string.Format(CultureInfo.InvariantCulture, $"Something {0}", 42);
                        
                            // Diagnostics for CompositeFormat
                            string.Format({|CA1863:StaticProperty|}, 42);
                            string.Format({|CA1863:StaticProperty|}, 42, "something");
                            string.Format({|CA1863:StaticProperty|}, 42, "something", "else");
                            string.Format({|CA1863:StaticProperty|}, new object[] { 42, "something", "else", 84 });
                            string.Format({|CA1863:StaticField|}, 42);
                            string.Format({|CA1863:StaticField|}, 42, "something");
                            string.Format({|CA1863:StaticField|}, 42, "something", "else");
                            string.Format({|CA1863:StaticField|}, new object[] { 42, "something", "else", 84 });
                            string.Format({|CA1863:StaticMethod()|}, 42);
                            string.Format({|CA1863:StaticMethod()|}, 42, "something");
                            string.Format({|CA1863:StaticMethod()|}, 42, "something", "else");
                            string.Format({|CA1863:StaticMethod()|}, new object[] { 42, "something", "else", 84 });
                            string.Format(null, {|CA1863:StaticProperty|}, 42);
                            string.Format(null, {|CA1863:StaticProperty|}, 42, "something");
                            string.Format(null, {|CA1863:StaticProperty|}, 42, "something", "else");
                            string.Format(null, {|CA1863:StaticProperty|}, new object[] { 42, "something", "else", 84 });
                            string.Format(null, {|CA1863:StaticField|}, 42);
                            string.Format(null, {|CA1863:StaticField|}, 42, "something");
                            string.Format(null, {|CA1863:StaticField|}, 42, "something", "else");
                            string.Format(null, {|CA1863:StaticField|}, new object[] { 42, "something", "else", 84 });
                            string.Format(null, {|CA1863:StaticMethod()|}, 42);
                            string.Format(null, {|CA1863:StaticMethod()|}, 42, "something");
                            string.Format(null, {|CA1863:StaticMethod()|}, 42, "something", "else");
                            string.Format(null, {|CA1863:StaticMethod()|}, new object[] { 42, "something", "else", 84 });
                            string.Format(CultureInfo.InvariantCulture, {|CA1863:StaticProperty|}, 42);
                            string.Format(CultureInfo.InvariantCulture, {|CA1863:StaticProperty|}, 42, "something");
                            string.Format(CultureInfo.InvariantCulture, {|CA1863:StaticProperty|}, 42, "something", "else");
                            string.Format(CultureInfo.InvariantCulture, {|CA1863:StaticProperty|}, new object[] { 42, "something", "else", 84 });
                            string.Format(CultureInfo.InvariantCulture, {|CA1863:StaticField|}, 42);
                            string.Format(CultureInfo.InvariantCulture, {|CA1863:StaticField|}, 42, "something");
                            string.Format(CultureInfo.InvariantCulture, {|CA1863:StaticField|}, 42, "something", "else");
                            string.Format(CultureInfo.InvariantCulture, {|CA1863:StaticField|}, new object[] { 42, "something", "else", 84 });
                            string.Format(CultureInfo.InvariantCulture, {|CA1863:StaticMethod()|}, 42);
                            string.Format(CultureInfo.InvariantCulture, {|CA1863:StaticMethod()|}, 42, "something");
                            string.Format(CultureInfo.InvariantCulture, {|CA1863:StaticMethod()|}, 42, "something", "else");
                            string.Format(CultureInfo.InvariantCulture, {|CA1863:StaticMethod()|}, new object[] { 42, "something", "else", 84 });
                            string.Format(arg0: 42, arg1: "something", arg2: "else", format: {|CA1863:StaticMethod()|}, provider: CultureInfo.InvariantCulture);
                            string.Format(null, {|CA1863:StaticMethod("")|}, 42);
                            string.Format(null, {|CA1863:StaticMethod(StaticProperty)|}, 42);
                            string.Format(null, {|CA1863:StaticMethod(StaticProperty, StaticMethod(StaticField, StaticProperty))|}, 42);
                            string.Format(null, {|CA1863:StaticMethod(StaticField)|}, 42);
                            string.Format(null, {|CA1863:StaticMethod(StaticMethod(StaticMethod(StaticProperty)))|}, 42);
                        }
                    
                        public void TestStringBuilderAppendFormat(StringBuilder sb, string format)
                        {
                            // No diagnostics for a literal
                            sb.AppendFormat("Hello {0}", 42);
                            sb.AppendFormat("Hello {0} {1}", 42, "something");
                            sb.AppendFormat("Hello {0} {1} {2}", 42, "something", "else");
                            sb.AppendFormat("Hello {0} {1} {2} {3}", new object[] { 42, "something", "else", 84 });
                            sb.AppendFormat(null, "Hello {0}", 42);
                            sb.AppendFormat(null, "Hello {0} {1}", 42, "something");
                            sb.AppendFormat(null, "Hello {0} {1} {2}", 42, "something", "else");
                            sb.AppendFormat(null, "Hello {0} {1} {2} {3}", new object[] { 42, "something", "else", 84 });
                            sb.AppendFormat(CultureInfo.InvariantCulture, "Hello {0}", 42);
                            sb.AppendFormat(CultureInfo.InvariantCulture, "Hello {0} {1}", 42, "something");
                            sb.AppendFormat(CultureInfo.InvariantCulture, "Hello {0} {1} {2}", 42, "something", "else");
                            sb.AppendFormat(CultureInfo.InvariantCulture, "Hello {0} {1} {2} {3}", new object[] { 42, "something", "else", 84 });

                            // No diagnostics because of an unsupported format string argument
                            sb.AppendFormat(InstanceProperty, 42);
                            sb.AppendFormat(InstanceField, 42);
                            sb.AppendFormat(InstanceMethod(), 42);
                            sb.AppendFormat(InstanceMethod(""), 42);
                            sb.AppendFormat(format, 42);
                            sb.AppendFormat(null, InstanceProperty, 42);
                            sb.AppendFormat(null, InstanceField, 42);
                            sb.AppendFormat(null, InstanceMethod(), 42);
                            sb.AppendFormat(null, InstanceMethod(""), 42);
                            sb.AppendFormat(null, format, 42);
                            sb.AppendFormat(CultureInfo.InvariantCulture, InstanceProperty, 42);
                            sb.AppendFormat(CultureInfo.InvariantCulture, InstanceField, 42);
                            sb.AppendFormat(CultureInfo.InvariantCulture, InstanceMethod(), 42);
                            sb.AppendFormat(CultureInfo.InvariantCulture, InstanceMethod(""), 42);
                            sb.AppendFormat(CultureInfo.InvariantCulture, format, 42);
                            sb.AppendFormat(CultureInfo.InvariantCulture, StaticMethod(InstanceMethod("")), 42);
                            sb.AppendFormat(CultureInfo.InvariantCulture, $"Something {0}", 42);
                        
                            // Diagnostics for CompositeFormat
                            sb.AppendFormat({|CA1863:StaticProperty|}, 42);
                            sb.AppendFormat({|CA1863:StaticProperty|}, 42, "something");
                            sb.AppendFormat({|CA1863:StaticProperty|}, 42, "something", "else");
                            sb.AppendFormat({|CA1863:StaticProperty|}, new object[] { 42, "something", "else", 84 });
                            sb.AppendFormat({|CA1863:StaticField|}, 42);
                            sb.AppendFormat({|CA1863:StaticField|}, 42, "something");
                            sb.AppendFormat({|CA1863:StaticField|}, 42, "something", "else");
                            sb.AppendFormat({|CA1863:StaticField|}, new object[] { 42, "something", "else", 84 });
                            sb.AppendFormat({|CA1863:StaticMethod()|}, 42);
                            sb.AppendFormat({|CA1863:StaticMethod()|}, 42, "something");
                            sb.AppendFormat({|CA1863:StaticMethod()|}, 42, "something", "else");
                            sb.AppendFormat({|CA1863:StaticMethod()|}, new object[] { 42, "something", "else", 84 });
                            sb.AppendFormat(null, {|CA1863:StaticProperty|}, 42);
                            sb.AppendFormat(null, {|CA1863:StaticProperty|}, 42, "something");
                            sb.AppendFormat(null, {|CA1863:StaticProperty|}, 42, "something", "else");
                            sb.AppendFormat(null, {|CA1863:StaticProperty|}, new object[] { 42, "something", "else", 84 });
                            sb.AppendFormat(null, {|CA1863:StaticField|}, 42);
                            sb.AppendFormat(null, {|CA1863:StaticField|}, 42, "something");
                            sb.AppendFormat(null, {|CA1863:StaticField|}, 42, "something", "else");
                            sb.AppendFormat(null, {|CA1863:StaticField|}, new object[] { 42, "something", "else", 84 });
                            sb.AppendFormat(null, {|CA1863:StaticMethod()|}, 42);
                            sb.AppendFormat(null, {|CA1863:StaticMethod()|}, 42, "something");
                            sb.AppendFormat(null, {|CA1863:StaticMethod()|}, 42, "something", "else");
                            sb.AppendFormat(null, {|CA1863:StaticMethod()|}, new object[] { 42, "something", "else", 84 });
                            sb.AppendFormat(CultureInfo.InvariantCulture, {|CA1863:StaticProperty|}, 42);
                            sb.AppendFormat(CultureInfo.InvariantCulture, {|CA1863:StaticProperty|}, 42, "something");
                            sb.AppendFormat(CultureInfo.InvariantCulture, {|CA1863:StaticProperty|}, 42, "something", "else");
                            sb.AppendFormat(CultureInfo.InvariantCulture, {|CA1863:StaticProperty|}, new object[] { 42, "something", "else", 84 });
                            sb.AppendFormat(CultureInfo.InvariantCulture, {|CA1863:StaticField|}, 42);
                            sb.AppendFormat(CultureInfo.InvariantCulture, {|CA1863:StaticField|}, 42, "something");
                            sb.AppendFormat(CultureInfo.InvariantCulture, {|CA1863:StaticField|}, 42, "something", "else");
                            sb.AppendFormat(CultureInfo.InvariantCulture, {|CA1863:StaticField|}, new object[] { 42, "something", "else", 84 });
                            sb.AppendFormat(CultureInfo.InvariantCulture, {|CA1863:StaticMethod()|}, 42);
                            sb.AppendFormat(CultureInfo.InvariantCulture, {|CA1863:StaticMethod()|}, 42, "something");
                            sb.AppendFormat(CultureInfo.InvariantCulture, {|CA1863:StaticMethod()|}, 42, "something", "else");
                            sb.AppendFormat(CultureInfo.InvariantCulture, {|CA1863:StaticMethod()|}, new object[] { 42, "something", "else", 84 });
                            sb.AppendFormat(arg0: 42, arg1: "something", arg2: "else", format: {|CA1863:StaticMethod()|}, provider: CultureInfo.InvariantCulture);
                            sb.AppendFormat(null, {|CA1863:StaticMethod("")|}, 42);
                            sb.AppendFormat(null, {|CA1863:StaticMethod(StaticProperty)|}, 42);
                            sb.AppendFormat(null, {|CA1863:StaticMethod(StaticProperty, StaticMethod(StaticField, StaticProperty))|}, 42);
                            sb.AppendFormat(null, {|CA1863:StaticMethod(StaticField)|}, 42);
                            sb.AppendFormat(null, {|CA1863:StaticMethod(StaticMethod(StaticMethod(StaticProperty)))|}, 42);
                        }
                    }
                    """,
            }.RunAsync();
        }

        private const string Preamble = """
            using System;
            using System.Globalization;
            using System.Text;
            
            namespace System
            {
                public class Object { }
                public struct Void { }
                public struct Int32 { private int _field; }
                public class ValueType { }
                public interface IFormatProvider { }
                public class Array { }
            
                public sealed class String
                {
                    public static String Format(String format, object arg0) => null;
                    public static String Format(String format, object arg0, object arg1) => null;
                    public static String Format(String format, object arg0, object arg1, object arg2) => null;
                    public static String Format(String format, object[] args) => null;
                    public static String Format(IFormatProvider provider, String format, object arg0) => null;
                    public static String Format(IFormatProvider provider, String format, object arg0, object arg1) => null;
                    public static String Format(IFormatProvider provider, String format, object arg0, object arg1, object arg2) => null;
                    public static String Format(IFormatProvider provider, String format, object[] args) => null;
                
                    public static String Format<T1>(IFormatProvider provider, CompositeFormat format, T1 arg1) => null;
                    public static String Format<T1, T2>(IFormatProvider provider, CompositeFormat format, T1 arg1, T2 arg2) => null;
                    public static String Format<T1, T2, T3>(IFormatProvider provider, CompositeFormat format, T1 arg1, T2 arg2, T3 arg3) => null;
                    public static String Format(IFormatProvider provider, CompositeFormat format, object[] args) => null;
                }
            }
            
            namespace System.Globalization
            {
                public class CultureInfo : IFormatProvider
                {
                    public static CultureInfo InvariantCulture { get; }
                }
            }
            
            namespace System.Text
            {
                public sealed class CompositeFormat { }
            
                public sealed class StringBuilder
                {
                    public StringBuilder AppendFormat(String format, object arg0) => null;
                    public StringBuilder AppendFormat(String format, object arg0, object arg1) => null;
                    public StringBuilder AppendFormat(String format, object arg0, object arg1, object arg2) => null;
                    public StringBuilder AppendFormat(String format, object[] args) => null;
                    public StringBuilder AppendFormat(IFormatProvider provider, String format, object arg0) => null;
                    public StringBuilder AppendFormat(IFormatProvider provider, String format, object arg0, object arg1) => null;
                    public StringBuilder AppendFormat(IFormatProvider provider, String format, object arg0, object arg1, object arg2) => null;
                    public StringBuilder AppendFormat(IFormatProvider provider, String format, object[] args) => null;
                
                    public StringBuilder AppendFormat<T1>(IFormatProvider provider, CompositeFormat format, T1 arg1) => null;
                    public StringBuilder AppendFormat<T1, T2>(IFormatProvider provider, CompositeFormat format, T1 arg1, T2 arg2) => null;
                    public StringBuilder AppendFormat<T1, T2, T3>(IFormatProvider provider, CompositeFormat format, T1 arg1, T2 arg2, T3 arg3) => null;
                    public StringBuilder AppendFormat(IFormatProvider provider, CompositeFormat format, object[] args) => null;
                }
            }

            """;
    }
}
