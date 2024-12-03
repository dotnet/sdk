// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotCallEnumerableCastOrOfTypeWithIncompatibleTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotCallEnumerableCastOrOfTypeWithIncompatibleTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DoNotCallEnumerableCastOrOfTypeWithIncompatibleTypesAnalyzerTests
    {
        private readonly DiagnosticDescriptor castRule = DoNotCallEnumerableCastOrOfTypeWithIncompatibleTypesAnalyzer.CastRule;
        private readonly DiagnosticDescriptor ofTypeRule = DoNotCallEnumerableCastOrOfTypeWithIncompatibleTypesAnalyzer.OfTypeRule;

        [Fact]
        public async Task CanCastRoundtripStructToInterface()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

interface IInterface {}

readonly struct Implementation : IInterface {}

class C
{
    void M()
    {
        IEnumerable<Implementation> e = default;
        var to = e.Cast<IInterface>();
        _ = to.Cast<Implementation>();
    }
}
");
        }

        [Fact]
        public async Task OnlyWellKnownIEnumerable()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace System.Linq
{ 
    interface IEnumerable {}
    // this 'IEnumerable<T>' isn't 'well known', so the analyzer won't fire
    interface IEnumerable<T> : IEnumerable {} 

    static class Enumerable
    {
        public static IEnumerable<T> OfType<T>(this IEnumerable ienum) => null;
    }

    class C
    {
        void M()
        {
            _ = Enumerable.OfType<string>(default(IEnumerable<int>));
            _ = default(IEnumerable<int>).OfType<string>();
        }
    }
}
");
        }

        [Fact()]
        public async Task UnrelatedMethodsDontTrigger()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections;
using System.Collections.Generic;

namespace System.Linq
{
    static class Enumerable
    {
        // missing 'this'
        public static IEnumerable<T> Cast<T>(IEnumerable ienum) => null;
        // wrong parameter type
        public static IEnumerable<T> Cast<T>(this IEnumerable<int> ienum) => null;
        // too many parameters
        public static IEnumerable<T> Cast<T>(this IEnumerable ienum, object distraction) => null;
        // too many type parameters
        public static IEnumerable<T> Cast<T, T2>(this IEnumerable ienum) => null;
    }

    class C
    {
        void M()
        {
            IEnumerable<int> e = default;
            _ = Enumerable.Cast<string>(e);
            _ = e.Cast<string>();
            _ = e.Cast<string>(null);
            _ = e.Cast<string, object>();
        }
    }
}
");
        }

        [Fact]
        public async Task DynamicCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Linq;

class C
{
    public void M()
    {
        dynamic x = null;
        _ = x.ToString();
        int[] numbers = new int[] { 1, 2, 3 };
        var v = from dynamic d in numbers select (int)d;
        _ = v.ToArray();
    }
}
");
        }

        [Fact]
        public async Task ValueTupleCasesCSharp()
        {
            var x = from (int min, int max) pair in new[] { (1, 2), (-10, -3) } select pair;
            _ = x.ToArray();

            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Linq;

class C
{
    public void M()
    {
        var x = from (int min, int max) pair in new[] { (1, 2), (-10, -3) } select pair;
        _= x.ToArray();
    }
}");
        }

        [Fact]
        public async Task RegExCasesCSharp()
        {
            var actualSet = new HashSet<(int start, int end)>(
                       Regex.Match(".", "abc").Groups
                       .Cast<Group>()
                       .Select(g => (start: g.Index, end: g.Index + g.Length)));

            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

class C
{
    public void M()
    {
            var actualSet = new HashSet<(int start, int end)>(
                       Regex.Match(""."", ""abc"").Groups
                       .Cast<Group>()
                       .Select(g => (start: g.Index, end: g.Index + g.Length)));
    }
}");
        }

        [Fact]
        public async Task MultipleTypesCasesCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public sealed class MultiContainer : IEnumerable<int>, IEnumerable<long>
{
    IEnumerator IEnumerable.GetEnumerator() => null;
    IEnumerator<int> IEnumerable<int>.GetEnumerator() => null;
    IEnumerator<long> IEnumerable<long>.GetEnumerator() => null;

    public static void M()
    {
        _ = new MultiContainer().OfType<int>();
        _ = new MultiContainer().OfType<long>();
        _ = new MultiContainer().OfType<string>();
    }
}");
        }

        [Fact]
        public async Task NonGenericCasesCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Linq;
class Fruit {}
class Apple : Fruit {}
class Shoe {}

interface ICar {}

interface IPlant {}
interface ITree : IPlant {}
interface IGrass : IPlant {}

class Plant : IPlant {}
class Tree : ITree {}
sealed class Grass : IGrass {}

class C
{
    public void M()
    {
        _ = (new int[0]).OfType<object>();
        _ = {|#10:(new int[0]).OfType<string>()|};
        _ = (new object[0]).OfType<string>();

        // we don't look through extra casts
        _ = (new System.Uri[0] as object[]).OfType<string>();
        _ = ((object[])new System.Uri[0]).OfType<string>();

        // expression syntax
        _ = from string s in new object[0]
            select s;
        _ = {|#11:from int i in new string[0]|}
            select i;
        _ = {|#12:Enumerable.Cast<string>(new int[0])|};

        // interfaces
        _ = (new ICar[0]).Cast<ICar>();
        _ = (new ICar[0]).Cast<IPlant>();
        _ = (new IPlant[0]).Cast<IPlant>();
        _ = (new IPlant[0]).Cast<IPlant>();

        // classes
        _ = (new Fruit[0]).Cast<Fruit>(); // identity
        _ = (new Fruit[0]).Cast<Apple>(); // upcast
        _ = {|#40:(new Fruit[0]).Cast<Shoe>()|}; // error
        _ = (new Apple[0]).Cast<Fruit>(); // downcast
        _ = (new Apple[0]).Cast<Apple>(); // identity
        _ = {|#41:(new Apple[0]).Cast<Shoe>()|}; // error
        _ = {|#42:(new Shoe[0]).Cast<Fruit>()|}; // error
        _ = {|#43:(new Shoe[0]).Cast<Apple>()|}; // error

        // interface to class
        _ = (new ICar[0]).Cast<Plant>(); // subclass of Plant could implement ICar
        _ = (new ICar[0]).Cast<Tree>(); // subclass of Tree could implement ICar
        _ = {|#50:(new ICar[0]).Cast<Grass>()|}; // subclass of Grass could not implement ICar, as Grass is sealed
        _ = (new ICar[0]).Cast<Shoe>(); // subclass of Shoe could implement ICar

        _ = (new IPlant[0]).Cast<Plant>();
        _ = (new IPlant[0]).Cast<Tree>();
        _ = (new IPlant[0]).Cast<Grass>();
        _ = (new IPlant[0]).Cast<Shoe>();

        _ = (new ITree[0]).Cast<Plant>();
        _ = (new ITree[0]).Cast<Tree>();
        _ = {|#51:(new ITree[0]).Cast<Grass>()|}; // subclass of Grass could not implement ICar, as Grass is sealed
        _ = (new ITree[0]).Cast<Shoe>();

        _ = (new IGrass[0]).Cast<Plant>();
        _ = (new IGrass[0]).Cast<Tree>();
        _ = (new IGrass[0]).Cast<Grass>();
        _ = (new IGrass[0]).Cast<Shoe>();

        // class to interface
        _ = (new Plant[0]).Cast<ICar>();
        _ = (new Tree[0]).Cast<ICar>();
        _ = {|#52:(new Grass[0]).Cast<ICar>()|}; // grass doesn't implement ICar, and is sealed so a subclass couldn't either
        _ = (new Shoe[0]).Cast<ICar>();

        _ = (new Plant[0]).Cast<IPlant>();
        _ = (new Tree[0]).Cast<IPlant>();
        _ = (new Grass[0]).Cast<IPlant>();
        _ = (new Shoe[0]).Cast<IPlant>();

        _ = (new Plant[0]).Cast<ITree>();
        _ = (new Tree[0]).Cast<ITree>();
        _ = {|#53:(new Grass[0]).Cast<ITree>()|};
        _ = (new Shoe[0]).Cast<ITree>();

        _ = (new Plant[0]).Cast<IGrass>();
        _ = (new Tree[0]).Cast<IGrass>();
        _ = (new Grass[0]).Cast<IGrass>();
        _ = (new Shoe[0]).Cast<IGrass>();
    }
}
",

VerifyCS.Diagnostic(ofTypeRule).WithLocation(10).WithArguments("int", "string"),
VerifyCS.Diagnostic(castRule).WithLocation(11).WithArguments("string", "int"),
VerifyCS.Diagnostic(castRule).WithLocation(12).WithArguments("int", "string"),

VerifyCS.Diagnostic(castRule).WithLocation(40).WithArguments("Fruit", "Shoe"),
VerifyCS.Diagnostic(castRule).WithLocation(41).WithArguments("Apple", "Shoe"),
VerifyCS.Diagnostic(castRule).WithLocation(42).WithArguments("Shoe", "Fruit"),
VerifyCS.Diagnostic(castRule).WithLocation(43).WithArguments("Shoe", "Apple"),

VerifyCS.Diagnostic(castRule).WithLocation(50).WithArguments("ICar", "Grass"),
VerifyCS.Diagnostic(castRule).WithLocation(51).WithArguments("ITree", "Grass"),
VerifyCS.Diagnostic(castRule).WithLocation(52).WithArguments("Grass", "ICar"),
VerifyCS.Diagnostic(castRule).WithLocation(53).WithArguments("Grass", "ITree")
);
        }

        [Fact]
        public async Task ArrayCSharp()
        {
            Assert.Throws<InvalidCastException>(()
                => (new int[][] { new int[] { 1 } }).Cast<object[]>().ToArray());
            Assert.Throws<InvalidCastException>(()
                => (new[] { 1 }).Cast<string>().ToArray());

            Assert.Throws<InvalidCastException>(()
                => (new object[][] { Array.Empty<object>() }).Cast<int[]>().ToArray());

            Assert.Throws<InvalidCastException>(()
                => (new ValueType[][] { Array.Empty<ValueType>() }).Cast<int[]>().ToArray());

            Assert.Throws<InvalidCastException>(()
                => (new object[][] { Array.Empty<ValueType>() }).Cast<int[]>().ToArray());

            Assert.Throws<InvalidCastException>(()
                => (new ValueType[][] { Array.Empty<ValueType>() }).Cast<int[]>().ToArray());

            Assert.Throws<InvalidCastException>(()
                => (new int[][] { Array.Empty<int>() }).Cast<ValueType[]>().ToArray());

            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Linq;

class C
{
    void M()
    {
        Enumerable.OfType<object[]>(new int[0][]);
        Enumerable.OfType<object[]>(new string[0][]);
        Enumerable.OfType<object[]>(new object[0][]);

        // these should all be errors, but not all implemented
        Enumerable.OfType<int[]>(new object[0][]);
        Enumerable.OfType<string[]>(new object[0][]);

        {|#12:Enumerable.OfType<string[]>(new int[0][])|};
        {|#13:Enumerable.OfType<string>(new int[0][])|};

        // multidimensional arrays don't implement IEnumberable<T>

        // IEnumerable<string[,]> != string[,]
        {|#20:Enumerable.OfType<string[,]>(new string[0,0])|}; 

        // these will throw
        {|#21:Enumerable.OfType<string[]>(new int[0,0])|};
        {|#22:Enumerable.OfType<string[,]>(new int[0,0])|};

        // arrays of multidimensional arrays are checked, including rank
        {|#30:Enumerable.OfType<string[]>(new string[0][,])|};
        {|#31:Enumerable.OfType<string[,]>(new string[0][])|};
        {|#32:Enumerable.OfType<string[]>(new int[0][,])|};
        {|#33:Enumerable.OfType<string[,]>(new int[0][])|};

        // all arrays can be cast to and from System.Array
        Enumerable.OfType<Array>(Enumerable.Empty<Array>());

        Enumerable.OfType<Array>(Enumerable.Empty<object[]>());
        Enumerable.OfType<Array>(Enumerable.Empty<object[]>());
        Enumerable.OfType<object[]>(Enumerable.Empty<Array>());
        Enumerable.OfType<string[]>(Enumerable.Empty<Array>());

        Enumerable.OfType<Array>(Enumerable.Empty<ValueType[]>());
        Enumerable.OfType<ValueType[]>(Enumerable.Empty<Array>());

        // but not non-arrays
        {|#41:Enumerable.OfType<Array>(Array.Empty<int>())|};
        Enumerable.OfType<Array>(Array.Empty<object>());
        {|#42:Enumerable.OfType<Array>(Array.Empty<string>())|};
        {|#43:Enumerable.OfType<Array>(Array.Empty<ValueType>())|};
        {|#44:Enumerable.OfType<Array>(Array.Empty<Enum>())|};

        {|#51:Enumerable.OfType<int>(Array.Empty<Array>())|};
        Enumerable.OfType<object>(Array.Empty<Array>());
        {|#52:Enumerable.OfType<string>(Array.Empty<Array>())|};
        {|#53:Enumerable.OfType<ValueType>(Array.Empty<Array>())|};
        {|#54:Enumerable.OfType<Enum>(Array.Empty<Array>())|};

        // this might work
        Enumerable.OfType<int[]>(Enumerable.Empty<ValueType[]>());
        // this is not allowed, but not implemented yet
        Enumerable.OfType<ValueType[]>(Enumerable.Empty<int[]>());
    }
}",
    //   VerifyCS.Diagnostic(ofTypeRule).WithLocation(10).WithArguments("int[]", "object[]"),
    //   VerifyCS.Diagnostic(ofTypeRule).WithLocation(11).WithArguments("string[]", "object[]"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(12).WithArguments("int[]", "string[]"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(13).WithArguments("int[]", "string"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(20).WithArguments("string", "string[*,*]"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(21).WithArguments("int", "string[]"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(22).WithArguments("int", "string[*,*]"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(30).WithArguments("string[*,*]", "string[]"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(31).WithArguments("string[]", "string[*,*]"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(32).WithArguments("int[*,*]", "string[]"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(33).WithArguments("int[]", "string[*,*]"),

    VerifyCS.Diagnostic(ofTypeRule).WithLocation(41).WithArguments("int", "System.Array"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(42).WithArguments("string", "System.Array"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(43).WithArguments("System.ValueType", "System.Array"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(44).WithArguments("System.Enum", "System.Array"),

    VerifyCS.Diagnostic(ofTypeRule).WithLocation(51).WithArguments("System.Array", "int"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(52).WithArguments("System.Array", "string"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(53).WithArguments("System.Array", "System.ValueType"),
    VerifyCS.Diagnostic(ofTypeRule).WithLocation(54).WithArguments("System.Array", "System.Enum")

//  VerifyCS.Diagnostic(ofTypeRule).WithLocation(60).WithArguments("ValueType[]", "int[]")
);
        }

        [Fact]
        public async Task EnumCasesCSharp()
        {
            Assert.Throws<InvalidCastException>(() => new int[] { 1 }.Cast<Enum>().ToArray());

            // this is ok!
            _ = new StringComparison[] { StringComparison.OrdinalIgnoreCase }.Cast<Enum>().ToArray();
            _ = new StringComparison[] { StringComparison.OrdinalIgnoreCase }.Cast<ValueType>().ToArray();
            _ = new ValueType[] { StringComparison.OrdinalIgnoreCase }.Cast<Enum>().ToArray();
            _ = new Enum[] { StringComparison.OrdinalIgnoreCase }.Cast<ValueType>().ToArray();
            _ = new ValueType[] { StringComparison.OrdinalIgnoreCase }.Cast<StringComparison>().ToArray();
            _ = new Enum[] { StringComparison.OrdinalIgnoreCase }.Cast<StringComparison>().ToArray();

            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Linq;

enum ByteEnum : byte {}
enum IntEnum {}
enum IntEnum2 {}
enum UIntEnum : uint {}
enum LongEnum : long {}

class C
{
    void M()
    {
        // object is everything
        _ = (new object[0]).Cast<IntEnum>();
        _ = (new IntEnum[0]).Cast<object>();

        // base class
        _ = (new Enum[0]).Cast<IntEnum>();
        _ = (new IntEnum[0]).Cast<Enum>();
        _ = {|#10:(new int[0]).Cast<Enum>()|};

        // value type
        _ = Array.Empty<ValueType>().Cast<IntEnum>();
        _ = (new IntEnum[0]).Cast<ValueType>();
        _ = (new ValueType[0]).Cast<Enum>();
        _ = (new Enum[0]).Cast<ValueType>();

        // Enums with the same underlying type are OK
        _ = (new IntEnum[0]).Cast<IntEnum2>();
        _ = (new IntEnum2[0]).Cast<IntEnum>();

        // to and from the underlying type are ok
        _ = (new ByteEnum[0]).Cast<byte>();
        _ = (new IntEnum[0]).Cast<int>();
        _ = (new UIntEnum[0]).Cast<uint>();
        _ = (new LongEnum[0]).Cast<long>();

        _ = Enumerable.Empty<byte>().Cast<ByteEnum>();
        _ = Enumerable.Empty<int>().Cast<IntEnum>();
        _ = Enumerable.Empty<uint>().Cast<UIntEnum>();
        _ = Enumerable.Empty<long>().Cast<LongEnum>();

        // enums with different underlying types are not
        _ = {|#20:(new IntEnum[0]).Cast<ByteEnum>()|};
        _ = {|#21:(new IntEnum[0]).Cast<UIntEnum>()|};

        _ = {|#22:(new int[0]).Cast<ByteEnum>()|};
        _ = {|#23:(new int[0]).Cast<UIntEnum>()|};

        _ = {|#24:(new IntEnum[0]).Cast<LongEnum>()|};
        _ = {|#25:(new int[0]).Cast<LongEnum>()|};

        _ = {|#26:(new ByteEnum[0]).Cast<IntEnum>()|};
        _ = {|#27:(new UIntEnum[0]).Cast<IntEnum>()|};

        _ = {|#28:(new ByteEnum[0]).Cast<int>()|};
        _ = {|#29:(new UIntEnum[0]).Cast<int>()|};

        _ = {|#30:(new LongEnum[0]).Cast<IntEnum>()|};
        _ = {|#31:(new LongEnum[0]).Cast<int>()|};
    }
}",
    VerifyCS.Diagnostic(castRule).WithLocation(10).WithArguments("int", "System.Enum"),
    VerifyCS.Diagnostic(castRule).WithLocation(20).WithArguments("IntEnum", "ByteEnum"),
    VerifyCS.Diagnostic(castRule).WithLocation(21).WithArguments("IntEnum", "UIntEnum"),
    VerifyCS.Diagnostic(castRule).WithLocation(22).WithArguments("int", "ByteEnum"),
    VerifyCS.Diagnostic(castRule).WithLocation(23).WithArguments("int", "UIntEnum"),
    VerifyCS.Diagnostic(castRule).WithLocation(24).WithArguments("IntEnum", "LongEnum"),
    VerifyCS.Diagnostic(castRule).WithLocation(25).WithArguments("int", "LongEnum"),
    VerifyCS.Diagnostic(castRule).WithLocation(26).WithArguments("ByteEnum", "IntEnum"),
    VerifyCS.Diagnostic(castRule).WithLocation(27).WithArguments("UIntEnum", "IntEnum"),
    VerifyCS.Diagnostic(castRule).WithLocation(28).WithArguments("ByteEnum", "int"),
    VerifyCS.Diagnostic(castRule).WithLocation(29).WithArguments("UIntEnum", "int"),
    VerifyCS.Diagnostic(castRule).WithLocation(30).WithArguments("LongEnum", "IntEnum"),
    VerifyCS.Diagnostic(castRule).WithLocation(31).WithArguments("LongEnum", "int")
);
        }

        [Fact]
        public async Task NullableValueTypeCasesCSharp()
        {
            _ = Enumerable.Range(1, 5).Cast<int?>().ToArray();
            _ = new int?[] { 123 }.Cast<int>().ToArray();
            _ = new ValueType[] { StringComparison.OrdinalIgnoreCase, null }.Cast<StringComparison?>().ToArray();

            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Linq;

enum ByteEnum : byte {}
enum IntEnum {}
enum IntEnum2 {}
enum UIntEnum : uint {}
enum LongEnum : long {}

class C
{
    void M()
    {
        // object is everything
        _ = (new object[0]).Cast<IntEnum?>();
        _ = (new IntEnum?[0]).Cast<object>();

        // nullable value types
        _ = (new bool[0]).Cast<bool?>();
        _ = (new bool?[0]).Cast<bool>();
        _ = (new byte[0]).Cast<byte?>();
        _ = (new byte?[0]).Cast<byte>();
        _ = (new short[0]).Cast<short?>();
        _ = (new short?[0]).Cast<short>();
        _ = Enumerable.Range(1, 5).Cast<int?>();
        _ = (new int?[0]).Cast<int>();
        _ = (new long[0]).Cast<long?>();
        _ = (new long?[0]).Cast<long>();
        _ = (new float[0]).Cast<float?>();
        _ = (new float?[0]).Cast<float>();
        _ = (new double[0]).Cast<double?>();
        _ = (new double?[0]).Cast<double>();


        // Nullable<T>
        _ = (new bool[0]).Cast<bool?>();
        _ = (new Nullable<bool>[0]).Cast<bool>();
        _ = (new byte[0]).Cast<Nullable<byte>>();
        _ = (new Nullable<byte>[0]).Cast<byte>();
        _ = (new short[0]).Cast<Nullable<short>>();
        _ = (new Nullable<short>[0]).Cast<short>();
        _ = Enumerable.Range(1, 5).Cast<Nullable<int>>();
        _ = (new Nullable<int>[0]).Cast<int>();
        _ = (new long[0]).Cast<Nullable<long>>();
        _ = (new Nullable<long>[0]).Cast<long>();
        _ = (new float[0]).Cast<Nullable<float>>();
        _ = (new Nullable<float>[0]).Cast<float>();
        _ = (new double[0]).Cast<Nullable<double>>();
        _ = (new Nullable<double>[0]).Cast<double>();

        // nullable value types
        _ = (new byte[0]).Cast<byte?>();
        _ = (new byte?[0]).Cast<byte>();
        _ = (new short[0]).Cast<short?>();
        _ = (new short?[0]).Cast<short>();
        _ = Enumerable.Range(1, 5).Cast<int?>();
        _ = (new int?[0]).Cast<int>();
        _ = (new long[0]).Cast<long?>();
        _ = (new long?[0]).Cast<long>();

        // base class
        _ = (new Enum[0]).Cast<IntEnum?>();
        _ = (new IntEnum?[0]).Cast<Enum>();
        _ = {|#10:(new int?[0]).Cast<Enum>()|};
        _ = {|#11:(new Enum[0]).Cast<int?>()|};

        // System.ValueType
        _ = (new Enum[0]).Cast<ValueType>();
        _ = Array.Empty<ValueType>().Cast<IntEnum?>();
        _ = (new IntEnum?[0]).Cast<ValueType>();
        _ = (new ValueType[0]).Cast<Enum>();

        // Enums with the same underlying type are OK
        _ = (new IntEnum?[0]).Cast<IntEnum2?>();
        _ = (new IntEnum2?[0]).Cast<IntEnum?>();

        // to and from the underlying type are ok
        _ = (new ByteEnum?[0]).Cast<byte?>();
        _ = (new IntEnum?[0]).Cast<int?>();
        _ = (new UIntEnum?[0]).Cast<uint?>();
        _ = (new LongEnum?[0]).Cast<long?>();

        _ = Enumerable.Empty<byte?>().Cast<ByteEnum?>();
        _ = Enumerable.Empty<int?>().Cast<IntEnum?>();
        _ = Enumerable.Empty<uint?>().Cast<UIntEnum?>();
        _ = Enumerable.Empty<long?>().Cast<LongEnum?>();

        // enums with different underlying types are not
        _ = {|#20:(new IntEnum?[0]).Cast<ByteEnum?>()|};
        _ = {|#21:(new IntEnum?[0]).Cast<UIntEnum?>()|};

        _ = {|#22:(new int?[0]).Cast<ByteEnum?>()|};
        _ = {|#23:(new int?[0]).Cast<UIntEnum?>()|};

        _ = {|#24:(new IntEnum?[0]).Cast<LongEnum?>()|};
        _ = {|#25:(new int?[0]).Cast<LongEnum?>()|};

        _ = {|#26:(new ByteEnum?[0]).Cast<IntEnum?>()|};
        _ = {|#27:(new UIntEnum?[0]).Cast<IntEnum?>()|};

        _ = {|#28:(new ByteEnum?[0]).Cast<int?>()|};
        _ = {|#29:(new UIntEnum?[0]).Cast<int?>()|};

        _ = {|#30:(new LongEnum?[0]).Cast<IntEnum?>()|};
        _ = {|#31:(new LongEnum?[0]).Cast<int?>()|};
    }
}",
    VerifyCS.Diagnostic(castRule).WithLocation(10).WithArguments("int?", "System.Enum"),
    VerifyCS.Diagnostic(castRule).WithLocation(11).WithArguments("System.Enum", "int?"),

    VerifyCS.Diagnostic(castRule).WithLocation(20).WithArguments("IntEnum?", "ByteEnum?"),
    VerifyCS.Diagnostic(castRule).WithLocation(21).WithArguments("IntEnum?", "UIntEnum?"),
    VerifyCS.Diagnostic(castRule).WithLocation(22).WithArguments("int?", "ByteEnum?"),
    VerifyCS.Diagnostic(castRule).WithLocation(23).WithArguments("int?", "UIntEnum?"),
    VerifyCS.Diagnostic(castRule).WithLocation(24).WithArguments("IntEnum?", "LongEnum?"),
    VerifyCS.Diagnostic(castRule).WithLocation(25).WithArguments("int?", "LongEnum?"),
    VerifyCS.Diagnostic(castRule).WithLocation(26).WithArguments("ByteEnum?", "IntEnum?"),
    VerifyCS.Diagnostic(castRule).WithLocation(27).WithArguments("UIntEnum?", "IntEnum?"),
    VerifyCS.Diagnostic(castRule).WithLocation(28).WithArguments("ByteEnum?", "int?"),
    VerifyCS.Diagnostic(castRule).WithLocation(29).WithArguments("UIntEnum?", "int?"),
    VerifyCS.Diagnostic(castRule).WithLocation(30).WithArguments("LongEnum?", "IntEnum?"),
    VerifyCS.Diagnostic(castRule).WithLocation(31).WithArguments("LongEnum?", "int?")
);
        }

        [Fact]
        public async Task GenericCastsCSharp()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = LanguageVersion.CSharp8,
                TestCode = @"
using System;
using System.Linq;

#nullable enable

struct Struct : IInterface {}
interface IInterface {}
interface IInterface2 {}

sealed class S : IInterface {}
sealed class Z : IInterface, IInterface2 {}

class C : IInterface
{
    void M<T>()
    {
        (new T[0]).Cast<int>(); // T could be anything
        (new int[0]).Cast<T>(); // T could be anything
    }

    void MClass<TClass>() where TClass : class
    {
        (new TClass[0]).Cast<int>(); // T could be object
        (new int[0]).Cast<TClass>(); // T could be object
    }

    void MclassC<TClassC>() where TClassC : C
    {
        (new TClassC[0]).Cast<object>();
        (new object[0]).Cast<TClassC>();

        (new TClassC[0]).Cast<C>();
        (new TClassC[0]).Cast<IInterface>(); 

        {|#10:(new TClassC[0]).Cast<string>()|}; // C is not string
        {|#11:(new string[0]).Cast<TClassC>()|}; // string is not C

        (new C[0]).Cast<TClassC>();
        (new IInterface[0]).Cast<TClassC>();

        {|#12:(new TClassC[0]).Cast<int>()|}; // error, subclass of C can't be int
        {|#13:(new int[0]).Cast<TClassC>()|}; // error, int can't be subclass of C
        {|#14:(new TClassC[0]).Cast<string>()|}; // error, subclass of C can't be int
        {|#15:(new string[0]).Cast<TClassC>()|}; // error, int can't be subclass of C
    }

    void MInterface<TInterface>() where TInterface : IInterface
    {
        (new TInterface[0]).Cast<IInterface>();
        (new IInterface[0]).Cast<TInterface>();

        (new TInterface[0]).Cast<object>();
        (new object[0]).Cast<TInterface>();

        {|#20:(new TInterface[0]).Cast<int>()|};
        {|#21:(new int[0]).Cast<TInterface>()|};

        {|#22:(new TInterface[0]).Cast<string>()|};
        {|#23:(new string[0]).Cast<TInterface>()|};
    }

    void MInterface2<TInterface>() where TInterface : IInterface, IInterface2
    {
        (new TInterface[0]).Cast<IInterface>();
        (new IInterface[0]).Cast<TInterface>();

        (new TInterface[0]).Cast<IInterface2>();
        (new IInterface2[0]).Cast<TInterface>();

        // does implement both interfaces
        (new Z[0]).Cast<TInterface>();
        (new TInterface[0]).Cast<Z>();

        // doesn't implement IInterface2, but a subclass could
        (new C[0]).Cast<TInterface>();
        (new TInterface[0]).Cast<C>();

        // sealed class doesn't implement IInterface2
        {|#30:(new S[0]).Cast<TInterface>()|};
        {|#31:(new TInterface[0]).Cast<S>()|};

        // struct class doesn't implement IInterface2
        {|#32:(new Struct[0]).Cast<TInterface>()|};
        {|#33:(new TInterface[0]).Cast<Struct>()|};

        // ok
        (new TInterface[0]).Cast<object>();
        (new object[0]).Cast<TInterface>();

        {|#34:(new TInterface[0]).Cast<int>()|};
        {|#35:(new int[0]).Cast<TInterface>()|};

        {|#36:(new TInterface[0]).Cast<string>()|};
        {|#37:(new string[0]).Cast<TInterface>()|};
    }

    void MStructInterface<TStructInterface>() where TStructInterface : struct, IInterface
    {
        {|#40:(new TStructInterface[0]).Cast<int>()|}; // error, int doesn't implement I
        {|#41:(new int[0]).Cast<TStructInterface>()|}; // error, I can't be cast to int
    }

    void Mstruct<TStruct>() where TStruct : struct
    {
        (new TStruct[0]).Cast<int>(); // int is a struct
        (new TStruct[0]).Cast<object>(); // can always cast to object
        {|#50:(new TStruct[0]).Cast<string>()|}; // string is not struct
        {|#51:(new TStruct[0]).Cast<string?>()|};

        (new int[0]).Cast<TStruct>(); // int is a struct
        (new object[0]).Cast<TStruct>(); // can always cast from object
        {|#52:(new string[0]).Cast<TStruct>()|}; // string is not struct
        {|#53:(new string?[0]).Cast<TStruct>()|};

        (new Nullable<TStruct>[0]).Cast<TStruct>();
        (new TStruct[0]).Cast<Nullable<TStruct>>();
        (new Nullable<TStruct>[0]).Cast<int>(); // int is a struct
        (new Nullable<TStruct>[0]).Cast<int?>();
        (new Nullable<TStruct>[0]).Cast<object>(); // can always cast to object
        (new Nullable<TStruct>[0]).Cast<object?>();
        {|#54:(new Nullable<TStruct>[0]).Cast<string>()|}; // string is not struct
        {|#55:(new Nullable<TStruct>[0]).Cast<string?>()|};

        (new int[0]).Cast<TStruct?>(); // int is a struct
        (new object[0]).Cast<TStruct?>(); // can always cast from object
        {|#56:(new string[0]).Cast<TStruct?>()|}; // string is not struct
    }
}",
                ExpectedDiagnostics = {
    VerifyCS.Diagnostic(castRule).WithLocation(10).WithArguments("TClassC", "string"),
    VerifyCS.Diagnostic(castRule).WithLocation(11).WithArguments("string", "TClassC"),

    VerifyCS.Diagnostic(castRule).WithLocation(12).WithArguments("TClassC", "int"),
    VerifyCS.Diagnostic(castRule).WithLocation(13).WithArguments("int", "TClassC"),
    VerifyCS.Diagnostic(castRule).WithLocation(14).WithArguments("TClassC", "string"),
    VerifyCS.Diagnostic(castRule).WithLocation(15).WithArguments("string", "TClassC"),

    VerifyCS.Diagnostic(castRule).WithLocation(20).WithArguments("TInterface", "int"),
    VerifyCS.Diagnostic(castRule).WithLocation(21).WithArguments("int", "TInterface"),

    VerifyCS.Diagnostic(castRule).WithLocation(22).WithArguments("TInterface", "string"),
    VerifyCS.Diagnostic(castRule).WithLocation(23).WithArguments("string", "TInterface"),

    VerifyCS.Diagnostic(castRule).WithLocation(30).WithArguments("S", "TInterface"),
    VerifyCS.Diagnostic(castRule).WithLocation(31).WithArguments("TInterface", "S"),
    VerifyCS.Diagnostic(castRule).WithLocation(32).WithArguments("Struct", "TInterface"),
    VerifyCS.Diagnostic(castRule).WithLocation(33).WithArguments("TInterface", "Struct"),

    VerifyCS.Diagnostic(castRule).WithLocation(34).WithArguments("TInterface", "int"),
    VerifyCS.Diagnostic(castRule).WithLocation(35).WithArguments("int", "TInterface"),
    VerifyCS.Diagnostic(castRule).WithLocation(36).WithArguments("TInterface", "string"),
    VerifyCS.Diagnostic(castRule).WithLocation(37).WithArguments("string", "TInterface"),

    VerifyCS.Diagnostic(castRule).WithLocation(40).WithArguments("TStructInterface", "int"),
    VerifyCS.Diagnostic(castRule).WithLocation(41).WithArguments("int", "TStructInterface"),

    VerifyCS.Diagnostic(castRule).WithLocation(50).WithArguments("TStruct", "string"),
    VerifyCS.Diagnostic(castRule).WithLocation(51).WithArguments("TStruct", "string?"),
    VerifyCS.Diagnostic(castRule).WithLocation(52).WithArguments("string", "TStruct"),
    VerifyCS.Diagnostic(castRule).WithLocation(53).WithArguments("string?", "TStruct"),
    VerifyCS.Diagnostic(castRule).WithLocation(54).WithArguments("TStruct?", "string"),
    VerifyCS.Diagnostic(castRule).WithLocation(55).WithArguments("TStruct?", "string?"),
    VerifyCS.Diagnostic(castRule).WithLocation(56).WithArguments("string", "TStruct?"),
                }
            }.RunAsync();
        }

        [Fact, WorkItem(7153, "https://github.com/dotnet/roslyn-analyzers/issues/7153")]
        public async Task GenericRecordsAndInterfaces()
        {
            var test = new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                LanguageVersion = LanguageVersion.CSharp10,
                TestCode = @"
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public static class Program
{
    public static void Main()
    {
        var nodeChanges = new List<INodeUpdate<GraphNode>> { new DataNodeUpdate(new DataNode(0, 0)) };

        //warning CA2021: This call will always result in an empty sequence because type 'INodeUpdate<GraphNode>' is incompatible with type 'DataNodeUpdate'
        var nodeChangesFiltered = nodeChanges.OfType<DataNodeUpdate>();

        Debug.Assert(nodeChangesFiltered.Count() == 1);
    }
}

public abstract record class GraphNode(long Id);
public sealed record class DataNode(long Id, long Value) : GraphNode(Id);

public interface INodeUpdate<out T>
{
    T Updated { get; }
}

public abstract record class NodeUpdate<T>(T Updated) : INodeUpdate<T> where T : GraphNode;
public sealed record class DataNodeUpdate(DataNode Updated) : NodeUpdate<DataNode>(Updated);"
            };
            await test.RunAsync();
        }

        [Fact, WorkItem(7357, "https://github.com/dotnet/roslyn-analyzers/issues/7357")]
        public async Task GenericDerivedType()
        {
            var test = new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                LanguageVersion = LanguageVersion.CSharp10,
                TestCode = @"
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public static class Program
{
    public static void Main()
    {
        // works as expected
        new List<Base>() { new Derived() }.Cast<Derived>().ToList();

        // same code but generic, fails
        // CastTest\Program.cs(7,1,7,77): warning CA2021: Type 'GenericBase<int>' is incompatible with type 'GenericDerived' and cast attempts will throw InvalidCastException at runtime (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2021)
        new List<GenericBase<int>>() { new GenericDerived() }.Cast<GenericDerived>().ToList();
    }
}

class Base
{
}

class Derived : Base
{
}

class GenericBase<T>
{
}

class GenericDerived : GenericBase<int>
{
}"
            };
            await test.RunAsync();
        }

        [Fact]
        public async Task NonGenericCasesVB()
        {
            var castRule = DoNotCallEnumerableCastOrOfTypeWithIncompatibleTypesAnalyzer.CastRule;

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Linq

Interface IApple
End Interface

Public Class Fruit
End Class

Public Class Orange
    Inherits Fruit
End Class

Class Apple
    Inherits Fruit
    Implements IApple
End Class

NotInheritable Class Salad
End Class

Module M    
    Sub S
        Dim a1 = (New Integer(){}).Cast(Of Object)
        Dim a2 = (New Object(){}).Cast(Of String)
        Dim a3 = (New Object(){}).Cast(Of Integer)
        Dim a4 = {|#11:(New Integer(){}).Cast(Of String)|}
        Dim a5 = {|#12:(New String(){}).Cast(Of Integer)|}
        
        Dim b1 = (New Object(){}).Cast(Of Fruit)
        Dim b2 = (New Object(){}).Cast(Of Orange)
        Dim b3 = (New Object(){}).Cast(Of IApple)
        Dim b4 = (New Object(){}).Cast(Of Apple)
        Dim b5 = (New Object(){}).Cast(Of Salad)

        Dim c1 = (New Fruit(){}).Cast(Of Fruit)
        Dim c2 = (New Fruit(){}).Cast(Of Orange)
        Dim c3 = (New Fruit(){}).Cast(Of IApple)
        Dim c4 = (New Fruit(){}).Cast(Of Apple)
        Dim c5 = {|#15:(New Fruit(){}).Cast(Of Salad)|}

        Dim d1 = (New Orange(){}).Cast(Of Fruit)
        Dim d2 = (New Orange(){}).Cast(Of Orange)
        Dim d3 = (New Orange(){}).Cast(Of IApple) ' subclass of Orange could implement IApple
        Dim d4 = {|#21:(New Orange(){}).Cast(Of Apple)|}
        Dim d5 = {|#22:(New Orange(){}).Cast(Of Salad)|}
        
        Dim e1 = (New IApple(){}).Cast(Of Fruit)
        Dim e2 = (New IApple(){}).Cast(Of Orange) ' subclass of Orange could implement IApple
        Dim e3 = (New IApple(){}).Cast(Of IApple)
        Dim e4 = (New IApple(){}).Cast(Of Apple)
        Dim e5 = {|#30:(New IApple(){}).Cast(Of Salad)|}
        
        Dim f1 = (New Apple(){}).Cast(Of Fruit)
        Dim f2 = {|#40:(New Apple(){}).Cast(Of Orange)|}
        Dim f3 = (New Apple(){}).Cast(Of IApple)
        Dim f4 = (New Apple(){}).Cast(Of Apple)
        Dim f5 = {|#41:(New Apple(){}).Cast(Of Salad)|}
    End Sub
End Module
",
    VerifyVB.Diagnostic(castRule).WithLocation(11).WithArguments("Integer", "String"),
    VerifyVB.Diagnostic(castRule).WithLocation(12).WithArguments("String", "Integer"),

    VerifyVB.Diagnostic(castRule).WithLocation(15).WithArguments("Fruit", "Salad"),

    VerifyVB.Diagnostic(castRule).WithLocation(21).WithArguments("Orange", "Apple"),
    VerifyVB.Diagnostic(castRule).WithLocation(22).WithArguments("Orange", "Salad"),

    VerifyVB.Diagnostic(castRule).WithLocation(30).WithArguments("IApple", "Salad"),

    VerifyVB.Diagnostic(castRule).WithLocation(40).WithArguments("Apple", "Orange"),
    VerifyVB.Diagnostic(castRule).WithLocation(41).WithArguments("Apple", "Salad")
   );
        }
    }
}
