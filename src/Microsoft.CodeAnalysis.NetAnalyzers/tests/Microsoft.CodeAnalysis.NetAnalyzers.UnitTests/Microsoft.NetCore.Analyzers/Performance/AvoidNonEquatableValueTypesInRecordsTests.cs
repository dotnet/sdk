// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.AvoidNonEquatableValueTypesInRecordsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    [TestClass]
    public class AvoidNonEquatableValueTypesInRecordsTests
    {
        [TestMethod]
        [DataRow("record")]
        [DataRow("record class")]
        [DataRow("sealed record")]
        [DataRow("abstract record")]
        [DataRow("record struct")]
        [DataRow("readonly record struct")]
        public Task PositionalParameter_NonEquatableStruct_ReportsDiagnostic(string recordKind)
        {
            string code = $$"""
                public struct S
                {
                    public int X;
                }

                public {{recordKind}} R(S [|Value|], int Other);
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task Diagnostic_HasMemberTypeAndRecordArguments()
        {
            const string code = """
                using System.Collections.Generic;

                public struct S
                {
                }

                public record R(S {|#0:Value|})
                {
                    public KeyValuePair<int, string> {|#1:Pair|} { get; init; }
                    private S? {|#2:_field|};
                }
                """;

            return VerifyAsync(
                code,
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("Value", "S", "R"),
                VerifyCS.Diagnostic().WithLocation(1).WithArguments("Pair", "KeyValuePair<int, string>", "R"),
                VerifyCS.Diagnostic().WithLocation(2).WithArguments("_field", "S", "R"));
        }

        [TestMethod]
        [DataRow("record")]
        [DataRow("record struct")]
        public Task MembersDeclaredInBody_NonEquatableStruct_ReportsDiagnostic(string recordKind)
        {
            string code = $$"""
                public struct S
                {
                }

                public {{recordKind}} R
                {
                    public R()
                    {
                    }

                    public S [|AutoProperty|] { get; set; }
                    public S [|GetOnlyProperty|] { get; }
                    public S [|InitializedProperty|] { get; init; } = default;
                    public S [|PublicField|];
                    private readonly S [|_readonlyField|];
                    private S [|_first|], [|_second|];
                }
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task PropertyUsingFieldKeyword_NonEquatableStruct_ReportsDiagnostic()
        {
            const string code = """
                public struct S
                {
                }

                public record R
                {
                    public S [|Value|]
                    {
                        get => field;
                        set => field = value;
                    }
                }
                """;

            return VerifyAsync(code, LanguageVersion.CSharp14);
        }

        [TestMethod]
        public Task PositionalParameterWithExplicitAutoProperty_ReportsDiagnosticOnProperty()
        {
            const string code = """
                public struct S
                {
                }

                public record R(S Value)
                {
                    public S [|Value|] { get; } = Value;
                }
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        [DataRow("S?")]
        [DataRow("System.Nullable<S>")]
        public Task NullableOfNonEquatableStruct_ReportsDiagnostic(string type)
        {
            string code = $$"""
                public struct S
                {
                }

                public record R({{type}} [|Value|]);
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task GenericStruct_ReportsDiagnosticOnlyWhenNotEquatable()
        {
            const string code = """
                using System;

                public struct NonEquatable<T>
                {
                    public T Value;
                }

                public struct Equatable<T> : IEquatable<Equatable<T>>
                {
                    public T Value;

                    public bool Equals(Equatable<T> other) => true;
                }

                public record R(NonEquatable<int> [|First|], Equatable<int> Second, Equatable<(int Left, string Right)>? Third);

                public record Generic<T>(NonEquatable<T> [|First|], Equatable<T> Second, NonEquatable<T>? [|Third|]);
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task StructFromReferencedAssembly_ReportsDiagnostic()
        {
            const string code = """
                using System.Collections;
                using System.Collections.Generic;

                public record R(
                    KeyValuePair<string, int> [|Pair|],
                    DictionaryEntry [|Entry|],
                    List<int>.Enumerator [|Enumerator|]);
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task StructImplementingEquatableOfOtherType_ReportsDiagnostic()
        {
            const string code = """
                using System;

                public struct S : IEquatable<int>, IEquatable<S?>
                {
                    public bool Equals(int other) => true;
                    public bool Equals(S? other) => true;
                }

                public record R(S [|Value|]);
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task MultipleMembers_OnlyNonEquatableStructsAreReported()
        {
            const string code = """
                using System;

                public struct NonEquatable
                {
                }

                public struct Equatable : IEquatable<Equatable>
                {
                    public bool Equals(Equatable other) => true;
                }

                public record struct R(int Id, NonEquatable [|First|], Equatable Second, string Name, NonEquatable [|Third|])
                {
                    public Equatable Fourth { get; set; }
                    public NonEquatable [|Fifth|] { get; set; }
                }
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task PartialRecord_ReportsMembersOfEveryPart()
        {
            const string code = """
                public struct S
                {
                }

                public partial record R(S [|Value|]);

                public partial record R
                {
                    public S [|Other|] { get; init; }
                }
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task NestedRecordAndNestedStruct_ReportsDiagnostic()
        {
            const string code = """
                public class Outer<T>
                {
                    public struct S
                    {
                    }

                    public record R(S [|Value|]);

                    public record struct RS(Outer<int>.S [|Value|]);
                }
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task DerivedRecord_ReportsInheritedMemberOnlyOnBaseRecord()
        {
            const string code = """
                public struct S
                {
                }

                public abstract record Base(S [|Value|]);

                public record Derived(S Value, int Other) : Base(Value);

                public record DerivedWithOwnMember(S Value, S [|Second|]) : Base(Value);
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task UnrelatedEqualsOverload_ReportsDiagnostic()
        {
            const string code = """
                public struct S
                {
                }

                public record R(S [|Value|])
                {
                    public bool Equals(int other) => false;
                    public bool Equals(R left, R right) => false;
                    public static bool Equals(S other) => false;
                }
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task StructImplementingEquatableOfItself_NoDiagnostic()
        {
            const string code = """
                using System;

                public struct Implicit : IEquatable<Implicit>
                {
                    public bool Equals(Implicit other) => true;
                }

                public struct Explicit : IEquatable<Explicit>
                {
                    bool IEquatable<Explicit>.Equals(Explicit other) => true;
                }

                public interface IValue<T> : IEquatable<T>
                {
                }

                public struct Indirect : IValue<Indirect>
                {
                    public bool Equals(Indirect other) => true;
                }

                public record R(Implicit First, Explicit Second, Indirect Third, Implicit? Fourth)
                {
                    public Implicit Fifth { get; init; }
                    private Explicit _sixth;
                }
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task RecordStructMember_NoDiagnostic()
        {
            const string code = """
                public record struct Inner(int X);

                public readonly record struct ReadOnlyInner(int X);

                public record R(Inner First, ReadOnlyInner Second, Inner? Third);
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task BuiltInAndFrameworkEquatableTypes_NoDiagnostic()
        {
            const string code = """
                using System;
                using System.Collections.Immutable;

                public enum E
                {
                    None
                }

                public record R(
                    int Int32,
                    bool Boolean,
                    char Char,
                    double Double,
                    decimal Decimal,
                    nint NativeInteger,
                    int? NullableInt32,
                    E Enum,
                    E? NullableEnum,
                    DayOfWeek FrameworkEnum,
                    Guid Guid,
                    DateTime DateTime,
                    TimeSpan? NullableTimeSpan,
                    ImmutableArray<int> ImmutableArray);
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task ReferenceTypes_NoDiagnostic()
        {
            const string code = """
                using System;
                using System.Collections.Generic;

                public class C
                {
                }

                public interface I
                {
                }

                public struct S : I
                {
                }

                public record R(string Text, object Object, C Class, I Interface, int[] Array, S[] StructArray, List<S> List, Action Delegate, dynamic Dynamic)
                {
                    public event EventHandler Event;
                }
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task TypeParameters_NoDiagnostic()
        {
            const string code = """
                public record Unconstrained<T>(T Value);

                public record struct ValueConstrained<T>(T Value, T? Nullable) where T : struct;
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task Tuples_NoDiagnostic()
        {
            const string code = """
                using System;

                public struct S
                {
                }

                public record R(
                    (int Left, string Right) Named,
                    (int, string) Unnamed,
                    ValueTuple<int, int> ValueTuple,
                    ((int Inner, dynamic Other) Left, string Right)? Nested,
                    (int, int, int, int, int, int, int, int Eighth) Long,
                    (S First, S Second) ContainingNonEquatable);
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task StaticMembers_NoDiagnostic()
        {
            const string code = """
                public struct S
                {
                }

                public record R(int Id)
                {
                    public static S StaticProperty { get; set; }
                    public static readonly S StaticField;
                    private static S s_privateStaticField;
                }
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task MembersWithoutBackingField_NoDiagnostic()
        {
            const string code = """
                public struct S
                {
                }

                public abstract record R(S Value)
                {
                    public S Value => default;
                    public S Computed => default;
                    public S ComputedWithSetter { get => default; set { } }
                    public abstract S Abstract { get; }
                    public S Method(S parameter) => parameter;
                }
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        [DataRow("record", "public virtual bool Equals(R other)")]
        [DataRow("sealed record", "public bool Equals(R other)")]
        [DataRow("record struct", "public bool Equals(R other)")]
        [DataRow("readonly record struct", "public bool Equals(R other)")]
        public Task RecordDeclaringEquals_NoDiagnostic(string recordKind, string equalsSignature)
        {
            string code = $$"""
                public struct S
                {
                }

                public {{recordKind}} R(S Value, int Id)
                {
                    public S Other { get; init; }

                    {{equalsSignature}} => true;

                    public override int GetHashCode() => 0;
                }
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task RecordDeclaringEqualsWithNullableAnnotation_NoDiagnostic()
        {
            const string code = """
                #nullable enable

                public struct S
                {
                }

                public record R(S Value)
                {
                    public virtual bool Equals(R? other) => true;

                    public override int GetHashCode() => 0;
                }
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task PartialRecordDeclaringEqualsInOtherPart_NoDiagnostic()
        {
            const string code = """
                public struct S
                {
                }

                public partial record R(S Value);

                public partial record R
                {
                    public virtual bool Equals(R other) => true;

                    public override int GetHashCode() => 0;
                }
                """;

            return VerifyAsync(code);
        }

        [TestMethod]
        public Task NonRecordTypes_NoDiagnostic()
        {
            const string code = """
                public struct S
                {
                }

                public class C(S value)
                {
                    public S Property { get; set; } = value;
                    private S _field;
                }

                public struct Container(S value)
                {
                    public S Property { get; set; } = value;
                    private S _field;
                }

                public interface I
                {
                    S Property { get; }
                }
                """;

            return VerifyAsync(code);
        }

        private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
            => VerifyAsync(source, LanguageVersion.CSharp12, expected);

        private static Task VerifyAsync(string source, LanguageVersion languageVersion, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                LanguageVersion = languageVersion,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            };

            test.ExpectedDiagnostics.AddRange(expected);

            return test.RunAsync();
        }
    }
}
