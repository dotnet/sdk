# Microsoft.CodeAnalysis.NetAnalyzers

## [CA1000](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1000): Do not declare static members on generic types

When a static member of a generic type is called, the type argument must be specified for the type. When a generic instance member that does not support inference is called, the type argument must be specified for the member. In these two cases, the syntax for specifying the type argument is different and easily confused.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1001](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1001): Types that own disposable fields should be disposable

A class declares and implements an instance field that is a System.IDisposable type, and the class does not implement IDisposable. A class that declares an IDisposable field indirectly owns an unmanaged resource and should implement the IDisposable interface.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Hidden|
|CodeFix|True|
---

## [CA1002](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1002): Do not expose generic lists

System.Collections.Generic.List\<T> is a generic collection that's designed for performance and not inheritance. List\<T> does not contain virtual members that make it easier to change the behavior of an inherited class.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1003](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1003): Use generic event handler instances

A type contains an event that declares an EventHandler delegate that returns void, whose signature contains two parameters (the first an object and the second a type that is assignable to EventArgs), and the containing assembly targets Microsoft .NET Framework?2.0.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1005](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1005): Avoid excessive parameters on generic types

The more type parameters a generic type contains, the more difficult it is to know and remember what each type parameter represents.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1008](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1008): Enums should have zero value

The default value of an uninitialized enumeration, just as other value types, is zero. A nonflags-attributed enumeration should define a member by using the value of zero so that the default value is a valid value of the enumeration. If an enumeration that has the FlagsAttribute attribute applied defines a zero-valued member, its name should be ""None"" to indicate that no values have been set in the enumeration.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1010](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1010): Generic interface should also be implemented

To broaden the usability of a type, implement one of the generic interfaces. This is especially true for collections as they can then be used to populate generic collection types.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1012](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1012): Abstract types should not have public constructors

Constructors on abstract types can be called only by derived types. Because public constructors create instances of a type, and you cannot create instances of an abstract type, an abstract type that has a public constructor is incorrectly designed.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1014](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1014): Mark assemblies with CLSCompliant

The Common Language Specification (CLS) defines naming restrictions, data types, and rules to which assemblies must conform if they will be used across programming languages. Good design dictates that all assemblies explicitly indicate CLS compliance by using CLSCompliantAttribute . If this attribute is not present on an assembly, the assembly is not compliant.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1016](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1016): Mark assemblies with assembly version

The .NET Framework uses the version number to uniquely identify an assembly, and to bind to types in strongly named assemblies. The version number is used together with version and publisher policy. By default, applications run only with the assembly version with which they were built.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1017](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1017): Mark assemblies with ComVisible

ComVisibleAttribute determines how COM clients access managed code. Good design dictates that assemblies explicitly indicate COM visibility. COM visibility can be set for the whole assembly and then overridden for individual types and type members. If this attribute is not present, the contents of the assembly are visible to COM clients.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1018](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1018): Mark attributes with AttributeUsageAttribute

Specify AttributeUsage on {0}

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1019](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1019): Define accessors for attribute arguments

Remove the property setter from {0} or reduce its accessibility because it corresponds to positional argument {1}

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1021](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1021): Avoid out parameters

Passing types by reference (using 'out' or 'ref') requires experience with pointers, understanding how value types and reference types differ, and handling methods with multiple return values. Also, the difference between 'out' and 'ref' parameters is not widely understood.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1024](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1024): Use properties where appropriate

A public or protected method has a name that starts with ""Get"", takes no parameters, and returns a value that is not an array. The method might be a good candidate to become a property.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1027](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1027): Mark enums with FlagsAttribute

An enumeration is a value type that defines a set of related named constants. Apply FlagsAttribute to an enumeration when its named constants can be meaningfully combined.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1028](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1028): Enum Storage should be Int32

An enumeration is a value type that defines a set of related named constants. By default, the System.Int32 data type is used to store the constant value. Although you can change this underlying type, it is not required or recommended for most scenarios.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1030](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1030): Use events where appropriate

This rule detects methods that have names that ordinarily would be used for events. If a method is called in response to a clearly defined state change, the method should be invoked by an event handler. Objects that call the method should raise events instead of calling the method directly.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1031](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1031): Do not catch general exception types

A general exception such as System.Exception or System.SystemException or a disallowed exception type is caught in a catch statement, or a general catch clause is used. General and disallowed exceptions should not be caught.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1032](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1032): Implement standard exception constructors

Failure to provide the full set of constructors can make it difficult to correctly handle exceptions.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1033](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1033): Interface methods should be callable by child types

An unsealed externally visible type provides an explicit method implementation of a public interface and does not provide an alternative externally visible method that has the same name.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1034](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1034): Nested types should not be visible

A nested type is a type that is declared in the scope of another type. Nested types are useful to encapsulate private implementation details of the containing type. Used for this purpose, nested types should not be externally visible.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1036](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1036): Override methods on comparable types

A public or protected type implements the System.IComparable interface. It does not override Object.Equals nor does it overload the language-specific operator for equality, inequality, less than, less than or equal, greater than or greater than or equal.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Hidden|
|CodeFix|True|
---

## [CA1040](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1040): Avoid empty interfaces

Interfaces define members that provide a behavior or usage contract. The functionality that is described by the interface can be adopted by any type, regardless of where the type appears in the inheritance hierarchy. A type implements an interface by providing implementations for the members of the interface. An empty interface does not define any members; therefore, it does not define a contract that can be implemented.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1041](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1041): Provide ObsoleteAttribute message

A type or member is marked by using a System.ObsoleteAttribute attribute that does not have its ObsoleteAttribute.Message property specified. When a type or member that is marked by using ObsoleteAttribute is compiled, the Message property of the attribute is displayed. This gives the user information about the obsolete type or member.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1043](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1043): Use Integral Or String Argument For Indexers

Indexers, that is, indexed properties, should use integer or string types for the index. These types are typically used for indexing data structures and increase the usability of the library. Use of the Object type should be restricted to those cases where the specific integer or string type cannot be specified at design time. If the design requires other types for the index, reconsider whether the type represents a logical data store. If it does not represent a logical data store, use a method.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1044](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1044): Properties should not be write only

Although it is acceptable and often necessary to have a read-only property, the design guidelines prohibit the use of write-only properties. This is because letting a user set a value, and then preventing the user from viewing that value, does not provide any security. Also, without read access, the state of shared objects cannot be viewed, which limits their usefulness.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1045](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1045): Do not pass types by reference

Passing types by reference (using out or ref) requires experience with pointers, understanding how value types and reference types differ, and handling methods that have multiple return values. Also, the difference between out and ref parameters is not widely understood.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1046](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1046): Do not overload equality operator on reference types

For reference types, the default implementation of the equality operator is almost always correct. By default, two references are equal only if they point to the same object. If the operator is providing meaningful value equality, the type should implement the generic 'System.IEquatable' interface.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1047](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1047): Do not declare protected member in sealed type

Types declare protected members so that inheriting types can access or override the member. By definition, you cannot inherit from a sealed type, which means that protected methods on sealed types cannot be called.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1050](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1050): Declare types in namespaces

Types are declared in namespaces to prevent name collisions and as a way to organize related types in an object hierarchy.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1051](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1051): Do not declare visible instance fields

The primary use of a field should be as an implementation detail. Fields should be private or internal and should be exposed by using properties.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1052](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1052): Static holder types should be Static or NotInheritable

Type '{0}' is a static holder type but is neither static nor NotInheritable

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1054](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1054): URI-like parameters should not be strings

This rule assumes that the parameter represents a Uniform Resource Identifier (URI). A string representation or a URI is prone to parsing and encoding errors, and can lead to security vulnerabilities. 'System.Uri' class provides these services in a safe and secure manner.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1055](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1055): URI-like return values should not be strings

This rule assumes that the method returns a URI. A string representation of a URI is prone to parsing and encoding errors, and can lead to security vulnerabilities. The System.Uri class provides these services in a safe and secure manner.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1056](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1056): URI-like properties should not be strings

This rule assumes that the property represents a Uniform Resource Identifier (URI). A string representation of a URI is prone to parsing and encoding errors, and can lead to security vulnerabilities. The System.Uri class provides these services in a safe and secure manner.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1058](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1058): Types should not extend certain base types

An externally visible type extends certain base types. Use one of the alternatives.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1060](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1060): Move pinvokes to native methods class

Platform Invocation methods, such as those that are marked by using the System.Runtime.InteropServices.DllImportAttribute attribute, or methods that are defined by using the Declare keyword in Visual Basic, access unmanaged code. These methods should be of the NativeMethods, SafeNativeMethods, or UnsafeNativeMethods class.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1061](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1061): Do not hide base class methods

A method in a base type is hidden by an identically named method in a derived type when the parameter signature of the derived method differs only by types that are more weakly derived than the corresponding types in the parameter signature of the base method.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1062](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1062): Validate arguments of public methods

An externally visible method dereferences one of its reference arguments without verifying whether that argument is 'null' ('Nothing' in Visual Basic). All reference arguments that are passed to externally visible methods should be checked against 'null'. If appropriate, throw an 'ArgumentNullException' when the argument is 'null'. If the method is designed to be called only by known assemblies, you should make the method internal.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1063](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1063): Implement IDisposable Correctly

All IDisposable types should implement the Dispose pattern correctly.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1064](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1064): Exceptions should be public

An internal exception is visible only inside its own internal scope. After the exception falls outside the internal scope, only the base exception can be used to catch the exception. If the internal exception is inherited from T:System.Exception, T:System.SystemException, or T:System.ApplicationException, the external code will not have sufficient information to know what to do with the exception.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1065](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1065): Do not raise exceptions in unexpected locations

A method that is not expected to throw exceptions throws an exception.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1066](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1066): Implement IEquatable when overriding Object.Equals

When a type T overrides Object.Equals(object), the implementation must cast the object argument to the correct type T before performing the comparison. If the type implements IEquatable\<T>, and therefore offers the method T.Equals(T), and if the argument is known at compile time to be of type T, then the compiler can call IEquatable\<T>.Equals(T) instead of Object.Equals(object), and no cast is necessary, improving performance.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1067](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1067): Override Object.Equals(object) when implementing IEquatable\<T>

When a type T implements the interface IEquatable\<T>, it suggests to a user who sees a call to the Equals method in source code that an instance of the type can be equated with an instance of any other type. The user might be confused if their attempt to equate the type with an instance of another type fails to compile. This violates the "principle of least surprise".

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1068](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1068): CancellationToken parameters must come last

Method '{0}' should take CancellationToken as the last parameter

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1069](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1069): Enums values should not be duplicated

The field reference '{0}' is duplicated in this bitwise initialization

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1070](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1070): Do not declare event fields as virtual

Do not declare virtual events in a base class. Overridden events in a derived class have undefined behavior. The C# compiler does not handle this correctly and it is unpredictable whether a subscriber to the derived event will actually be subscribing to the base class event.

|Item|Value|
|-|-|
|Category|Design|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1200](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1200): Avoid using cref tags with a prefix

Use of cref tags with prefixes should be avoided, since it prevents the compiler from verifying references and the IDE from updating references during refactorings. It is permissible to suppress this error at a single documentation site if the cref must use a prefix because the type being mentioned is not findable by the compiler. For example, if a cref is mentioning a special attribute in the full framework but you're in a file that compiles against the portable framework, or if you want to reference a type at higher layer of Roslyn, you should suppress the error. You should not suppress the error just because you want to take a shortcut and avoid using the full syntax.

|Item|Value|
|-|-|
|Category|Documentation|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1303](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1303): Do not pass literals as localized parameters

A method passes a string literal as a parameter to a constructor or method in the .NET Framework class library and that string should be localizable. To fix a violation of this rule, replace the string literal with a string retrieved through an instance of the ResourceManager class.

|Item|Value|
|-|-|
|Category|Globalization|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1304](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1304): Specify CultureInfo

A method or constructor calls a member that has an overload that accepts a System.Globalization.CultureInfo parameter, and the method or constructor does not call the overload that takes the CultureInfo parameter. When a CultureInfo or System.IFormatProvider object is not supplied, the default value that is supplied by the overloaded member might not have the effect that you want in all locales. If the result will be displayed to the user, specify 'CultureInfo.CurrentCulture' as the 'CultureInfo' parameter. Otherwise, if the result will be stored and accessed by software, such as when it is persisted to disk or to a database, specify 'CultureInfo.InvariantCulture'.

|Item|Value|
|-|-|
|Category|Globalization|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1305](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1305): Specify IFormatProvider

A method or constructor calls one or more members that have overloads that accept a System.IFormatProvider parameter, and the method or constructor does not call the overload that takes the IFormatProvider parameter. When a System.Globalization.CultureInfo or IFormatProvider object is not supplied, the default value that is supplied by the overloaded member might not have the effect that you want in all locales. If the result will be based on the input from/output displayed to the user, specify 'CultureInfo.CurrentCulture' as the 'IFormatProvider'. Otherwise, if the result will be stored and accessed by software, such as when it is loaded from disk/database and when it is persisted to disk/database, specify 'CultureInfo.InvariantCulture'.

|Item|Value|
|-|-|
|Category|Globalization|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1307](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1307): Specify StringComparison for clarity

A string comparison operation uses a method overload that does not set a StringComparison parameter. It is recommended to use the overload with StringComparison parameter for clarity of intent. If the result will be displayed to the user, such as when sorting a list of items for display in a list box, specify 'StringComparison.CurrentCulture' or 'StringComparison.CurrentCultureIgnoreCase' as the 'StringComparison' parameter. If comparing case-insensitive identifiers, such as file paths, environment variables, or registry keys and values, specify 'StringComparison.OrdinalIgnoreCase'. Otherwise, if comparing case-sensitive identifiers, specify 'StringComparison.Ordinal'.

|Item|Value|
|-|-|
|Category|Globalization|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1308](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1308): Normalize strings to uppercase

Strings should be normalized to uppercase. A small group of characters cannot make a round trip when they are converted to lowercase. To make a round trip means to convert the characters from one locale to another locale that represents character data differently, and then to accurately retrieve the original characters from the converted characters.

|Item|Value|
|-|-|
|Category|Globalization|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1309](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1309): Use ordinal string comparison

A string comparison operation that is nonlinguistic does not set the StringComparison parameter to either Ordinal or OrdinalIgnoreCase. By explicitly setting the parameter to either StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase, your code often gains speed, becomes more correct, and becomes more reliable.

|Item|Value|
|-|-|
|Category|Globalization|
|Enabled|True|
|Severity|Hidden|
|CodeFix|True|
---

## [CA1310](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1310): Specify StringComparison for correctness

A string comparison operation uses a method overload that does not set a StringComparison parameter, hence its behavior could vary based on the current user's locale settings. It is strongly recommended to use the overload with StringComparison parameter for correctness and clarity of intent. If the result will be displayed to the user, such as when sorting a list of items for display in a list box, specify 'StringComparison.CurrentCulture' or 'StringComparison.CurrentCultureIgnoreCase' as the 'StringComparison' parameter. If comparing case-insensitive identifiers, such as file paths, environment variables, or registry keys and values, specify 'StringComparison.OrdinalIgnoreCase'. Otherwise, if comparing case-sensitive identifiers, specify 'StringComparison.Ordinal'.

|Item|Value|
|-|-|
|Category|Globalization|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1311](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1311): Specify a culture or use an invariant version

Specify culture to help avoid accidental implicit dependency on current culture. Using an invariant version yields consistent results regardless of the culture of an application.

|Item|Value|
|-|-|
|Category|Globalization|
|Enabled|True|
|Severity|Hidden|
|CodeFix|True|
---

## [CA1401](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1401): P/Invokes should not be visible

A public or protected method in a public type has the System.Runtime.InteropServices.DllImportAttribute attribute (also implemented by the Declare keyword in Visual Basic). Such methods should not be exposed.

|Item|Value|
|-|-|
|Category|Interoperability|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1416](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1416): Validate platform compatibility

Using platform dependent API on a component makes the code no longer work across all platforms.

|Item|Value|
|-|-|
|Category|Interoperability|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA1417](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1417): Do not use 'OutAttribute' on string parameters for P/Invokes

String parameters passed by value with the 'OutAttribute' can destabilize the runtime if the string is an interned string.

|Item|Value|
|-|-|
|Category|Interoperability|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA1418](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1418): Use valid platform string

Platform compatibility analyzer requires a valid platform name and version.

|Item|Value|
|-|-|
|Category|Interoperability|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA1419](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1419): Provide a parameterless constructor that is as visible as the containing type for concrete types derived from 'System.Runtime.InteropServices.SafeHandle'

Providing a parameterless constructor that is as visible as the containing type for a type derived from 'System.Runtime.InteropServices.SafeHandle' enables better performance and usage with source-generated interop solutions.

|Item|Value|
|-|-|
|Category|Interoperability|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1420](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1420): Property, type, or attribute requires runtime marshalling

Using features that require runtime marshalling when runtime marshalling is disabled will result in runtime exceptions.

|Item|Value|
|-|-|
|Category|Interoperability|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA1421](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1421): This method uses runtime marshalling even when the 'DisableRuntimeMarshallingAttribute' is applied

This method uses runtime marshalling even when runtime marshalling is disabled, which can cause unexpected behavior differences at runtime due to different expectations of a type's native layout.

|Item|Value|
|-|-|
|Category|Interoperability|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1422](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1422): Validate platform compatibility

Using platform dependent API on a component makes the code no longer work across all platforms.

|Item|Value|
|-|-|
|Category|Interoperability|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA1501](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1501): Avoid excessive inheritance

Deeply nested type hierarchies can be difficult to follow, understand, and maintain. This rule limits analysis to hierarchies in the same module. To fix a violation of this rule, derive the type from a base type that is less deep in the inheritance hierarchy or eliminate some of the intermediate base types.

|Item|Value|
|-|-|
|Category|Maintainability|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1502](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1502): Avoid excessive complexity

Cyclomatic complexity measures the number of linearly independent paths through the method, which is determined by the number and complexity of conditional branches. A low cyclomatic complexity generally indicates a method that is easy to understand, test, and maintain. The cyclomatic complexity is calculated from a control flow graph of the method and is given as follows: `cyclomatic complexity = the number of edges - the number of nodes + 1`, where a node represents a logic branch point and an edge represents a line between nodes.

|Item|Value|
|-|-|
|Category|Maintainability|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1505](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1505): Avoid unmaintainable code

The maintainability index is calculated by using the following metrics: lines of code, program volume, and cyclomatic complexity. Program volume is a measure of the difficulty of understanding of a symbol that is based on the number of operators and operands in the code. Cyclomatic complexity is a measure of the structural complexity of the type or method. A low maintainability index indicates that code is probably difficult to maintain and would be a good candidate to redesign.

|Item|Value|
|-|-|
|Category|Maintainability|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1506](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1506): Avoid excessive class coupling

This rule measures class coupling by counting the number of unique type references that a symbol contains. Symbols that have a high degree of class coupling can be difficult to maintain. It is a good practice to have types and methods that exhibit low coupling and high cohesion. To fix this violation, try to redesign the code to reduce the number of types to which it is coupled.

|Item|Value|
|-|-|
|Category|Maintainability|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1507](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1507): Use nameof to express symbol names

Using nameof helps keep your code valid when refactoring.

|Item|Value|
|-|-|
|Category|Maintainability|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1508](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1508): Avoid dead conditional code

'{0}' is never '{1}'. Remove or refactor the condition(s) to avoid dead code.

|Item|Value|
|-|-|
|Category|Maintainability|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1509](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1509): Invalid entry in code metrics rule specification file

Invalid entry in code metrics rule specification file.

|Item|Value|
|-|-|
|Category|Maintainability|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1510](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1510): Use ArgumentNullException throw helper

Throw helpers are simpler and more efficient than an if block constructing a new exception instance.

|Item|Value|
|-|-|
|Category|Maintainability|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1511](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1511): Use ArgumentException throw helper

Throw helpers are simpler and more efficient than an if block constructing a new exception instance.

|Item|Value|
|-|-|
|Category|Maintainability|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1512](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1512): Use ArgumentOutOfRangeException throw helper

Throw helpers are simpler and more efficient than an if block constructing a new exception instance.

|Item|Value|
|-|-|
|Category|Maintainability|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1513](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1513): Use ObjectDisposedException throw helper

Throw helpers are simpler and more efficient than an if block constructing a new exception instance.

|Item|Value|
|-|-|
|Category|Maintainability|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1514](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1514): Avoid redundant length argument

An explicit length calculation can be error-prone and can be avoided when slicing to end of the buffer.

|Item|Value|
|-|-|
|Category|Maintainability|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1515](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1515): Consider making public types internal

Unlike a class library, an application's API isn't typically referenced publicly, so types can be marked internal.

|Item|Value|
|-|-|
|Category|Maintainability|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1700](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1700): Do not name enum values 'Reserved'

This rule assumes that an enumeration member that has a name that contains "reserved" is not currently used but is a placeholder to be renamed or removed in a future version. Renaming or removing a member is a breaking change.

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1707](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1707): Identifiers should not contain underscores

By convention, identifier names do not contain the underscore (_) character. This rule checks namespaces, types, members, and parameters.

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|True|
|Severity|Hidden|
|CodeFix|True|
---

## [CA1708](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1708): Identifiers should differ by more than case

Identifiers for namespaces, types, members, and parameters cannot differ only by case because languages that target the common language runtime are not required to be case-sensitive.

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1710](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1710): Identifiers should have correct suffix

By convention, the names of types that extend certain base types or that implement certain interfaces, or types that are derived from these types, have a suffix that is associated with the base type or interface.

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1711](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1711): Identifiers should not have incorrect suffix

By convention, only the names of types that extend certain base types or that implement certain interfaces, or types that are derived from these types, should end with specific reserved suffixes. Other type names should not use these reserved suffixes.

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1712](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1712): Do not prefix enum values with type name

An enumeration's values should not start with the type name of the enumeration.

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1713](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1713): Events should not have 'Before' or 'After' prefix

Event names should describe the action that raises the event. To name related events that are raised in a specific sequence, use the present or past tense to indicate the relative position in the sequence of actions. For example, when naming a pair of events that is raised when closing a resource, you might name it 'Closing' and 'Closed', instead of 'BeforeClose' and 'AfterClose'.

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1715](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1715): Identifiers should have correct prefix

The name of an externally visible interface does not start with an uppercase ""I"". The name of a generic type parameter on an externally visible type or method does not start with an uppercase ""T"".

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1716](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1716): Identifiers should not match keywords

A namespace name or a type name matches a reserved keyword in a programming language. Identifiers for namespaces and types should not match keywords that are defined by languages that target the common language runtime.

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1720](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1720): Identifier contains type name

Names of parameters and members are better used to communicate their meaning than to describe their type, which is expected to be provided by development tools. For names of members, if a data type name must be used, use a language-independent name instead of a language-specific one.

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1721](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1721): Property names should not match get methods

The name of a public or protected member starts with ""Get"" and otherwise matches the name of a public or protected property. ""Get"" methods and properties should have names that clearly distinguish their function.

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1724](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1724): Type names should not match namespaces

Type names should not match the names of namespaces that are defined in the .NET Framework class library. Violating this rule can reduce the usability of the library.

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1725](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1725): Parameter names should match base declaration

Consistent naming of parameters in an override hierarchy increases the usability of the method overrides. A parameter name in a derived method that differs from the name in the base declaration can cause confusion about whether the method is an override of the base method or a new overload of the method.

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|True|
|Severity|Hidden|
|CodeFix|True|
---

## [CA1727](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1727): Use PascalCase for named placeholders

Use PascalCase for named placeholders in the logging message template.

|Item|Value|
|-|-|
|Category|Naming|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1802](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1802): Use literals where appropriate

A field is declared static and read-only (Shared and ReadOnly in Visual Basic), and is initialized by using a value that is computable at compile time. Because the value that is assigned to the targeted field is computable at compile time, change the declaration to a const (Const in Visual Basic) field so that the value is computed at compile time instead of at runtime.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1805](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1805): Do not initialize unnecessarily

The .NET runtime initializes all fields of reference types to their default values before running the constructor. In most cases, explicitly initializing a field to its default value in a constructor is redundant, adding maintenance costs and potentially degrading performance (such as with increased assembly size), and the explicit initialization can be removed.  In some cases, such as with static readonly fields that permanently retain their default value, consider instead changing them to be constants or properties.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Hidden|
|CodeFix|True|
---

## [CA1806](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1806): Do not ignore method results

A new object is created but never used; or a method that creates and returns a new string is called and the new string is never used; or a COM or P/Invoke method returns an HRESULT or error code that is never used.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1810](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1810): Initialize reference type static fields inline

A reference type declares an explicit static constructor. To fix a violation of this rule, initialize all static data when it is declared and remove the static constructor.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1812](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1812): Avoid uninstantiated internal classes

An instance of an assembly-level type is not created by code in the assembly.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1813](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1813): Avoid unsealed attributes

The .NET Framework class library provides methods for retrieving custom attributes. By default, these methods search the attribute inheritance hierarchy. Sealing the attribute eliminates the search through the inheritance hierarchy and can improve performance.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1814](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1814): Prefer jagged arrays over multidimensional

A jagged array is an array whose elements are arrays. The arrays that make up the elements can be of different sizes, leading to less wasted space for some sets of data.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1815](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1815): Override equals and operator equals on value types

For value types, the inherited implementation of Equals uses the Reflection library and compares the contents of all fields. Reflection is computationally expensive, and comparing every field for equality might be unnecessary. If you expect users to compare or sort instances, or to use instances as hash table keys, your value type should implement Equals.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1816](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1816): Dispose methods should call SuppressFinalize

A method that is an implementation of Dispose does not call GC.SuppressFinalize; or a method that is not an implementation of Dispose calls GC.SuppressFinalize; or a method calls GC.SuppressFinalize and passes something other than this (Me in Visual Basic).

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1819](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1819): Properties should not return arrays

Arrays that are returned by properties are not write-protected, even when the property is read-only. To keep the array tamper-proof, the property must return a copy of the array. Typically, users will not understand the adverse performance implications of calling such a property.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1820](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1820): Test for empty strings using string length

Comparing strings by using the String.Length property or the String.IsNullOrEmpty method is significantly faster than using Equals.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1821](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1821): Remove empty Finalizers

Finalizers should be avoided where possible, to avoid the additional performance overhead involved in tracking object lifetime.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1822](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1822): Mark members as static

Members that do not access instance data or call instance methods can be marked as static. After you mark the methods as static, the compiler will emit nonvirtual call sites to these members. This can give you a measurable performance gain for performance-sensitive code.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1823](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1823): Avoid unused private fields

Private fields were detected that do not appear to be accessed in the assembly.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA1824](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1824): Mark assemblies with NeutralResourcesLanguageAttribute

The NeutralResourcesLanguage attribute informs the ResourceManager of the language that was used to display the resources of a neutral culture for an assembly. This improves lookup performance for the first resource that you load and can reduce your working set.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1825](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1825): Avoid zero-length array allocations

Avoid unnecessary zero-length array allocations.  Use {0} instead.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1826](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1826): Do not use Enumerable methods on indexable collections

This collection is directly indexable. Going through LINQ here causes unnecessary allocations and CPU work.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1827](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1827): Do not use Count() or LongCount() when Any() can be used

For non-empty collections, Count() and LongCount() enumerate the entire sequence, while Any() stops at the first item or the first item that satisfies a condition.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1828](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1828): Do not use CountAsync() or LongCountAsync() when AnyAsync() can be used

For non-empty collections, CountAsync() and LongCountAsync() enumerate the entire sequence, while AnyAsync() stops at the first item or the first item that satisfies a condition.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1829](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1829): Use Length/Count property instead of Count() when available

Enumerable.Count() potentially enumerates the sequence while a Length/Count property is a direct access.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1830](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1830): Prefer strongly-typed Append and Insert method overloads on StringBuilder

StringBuilder.Append and StringBuilder.Insert provide overloads for multiple types beyond System.String.  When possible, prefer the strongly-typed overloads over using ToString() and the string-based overload.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1831](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1831): Use AsSpan or AsMemory instead of Range-based indexers when appropriate

The Range-based indexer on string values produces a copy of requested portion of the string. This copy is usually unnecessary when it is implicitly used as a ReadOnlySpan or ReadOnlyMemory value. Use the AsSpan method to avoid the unnecessary copy.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Warning|
|CodeFix|True|
---

## [CA1832](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1832): Use AsSpan or AsMemory instead of Range-based indexers when appropriate

The Range-based indexer on array values produces a copy of requested portion of the array. This copy is usually unnecessary when it is implicitly used as a ReadOnlySpan or ReadOnlyMemory value. Use the AsSpan method to avoid the unnecessary copy.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1833](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1833): Use AsSpan or AsMemory instead of Range-based indexers when appropriate

The Range-based indexer on array values produces a copy of requested portion of the array. This copy is often unwanted when it is implicitly used as a Span or Memory value. Use the AsSpan method to avoid the copy.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1834](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1834): Consider using 'StringBuilder.Append(char)' when applicable

'StringBuilder.Append(char)' is more efficient than 'StringBuilder.Append(string)' when the string is a single character. When calling 'Append' with a constant, prefer using a constant char rather than a constant string containing one character.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1835](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1835): Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'

'Stream' has a 'ReadAsync' overload that takes a 'Memory\<Byte>' as the first argument, and a 'WriteAsync' overload that takes a 'ReadOnlyMemory\<Byte>' as the first argument. Prefer calling the memory based overloads, which are more efficient.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1836](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1836): Prefer IsEmpty over Count

For determining whether the object contains or not any items, prefer using 'IsEmpty' property rather than retrieving the number of items from the 'Count' property and comparing it to 0 or 1.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1837](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1837): Use 'Environment.ProcessId'

'Environment.ProcessId' is simpler and faster than 'Process.GetCurrentProcess().Id'.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1838](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1838): Avoid 'StringBuilder' parameters for P/Invokes

Marshalling of 'StringBuilder' always creates a native buffer copy, resulting in multiple allocations for one marshalling operation.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1839](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1839): Use 'Environment.ProcessPath'

'Environment.ProcessPath' is simpler and faster than 'Process.GetCurrentProcess().MainModule.FileName'.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1840](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1840): Use 'Environment.CurrentManagedThreadId'

'Environment.CurrentManagedThreadId' is simpler and faster than 'Thread.CurrentThread.ManagedThreadId'.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1841](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1841): Prefer Dictionary.Contains methods

Many dictionary implementations lazily initialize the Values collection. To avoid unnecessary allocations, prefer 'ContainsValue' over 'Values.Contains'.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1842](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1842): Do not use 'WhenAll' with a single task

Using 'WhenAll' with a single task may result in performance loss, await or return the task instead.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1843](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1843): Do not use 'WaitAll' with a single task

Using 'WaitAll' with a single task may result in performance loss, await or return the task instead.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1844](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1844): Provide memory-based overrides of async methods when subclassing 'Stream'

To improve performance, override the memory-based async methods when subclassing 'Stream'. Then implement the array-based methods in terms of the memory-based methods.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1845](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1845): Use span-based 'string.Concat'

It is more efficient to use 'AsSpan' and 'string.Concat', instead of 'Substring' and a concatenation operator.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1846](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1846): Prefer 'AsSpan' over 'Substring'

'AsSpan' is more efficient than 'Substring'. 'Substring' performs an O(n) string copy, while 'AsSpan' does not and has a constant cost.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1847](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1847): Use char literal for a single character lookup

'string.Contains(char)' is available as a better performing overload for single char lookup.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1848](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1848): Use the LoggerMessage delegates

For improved performance, use the LoggerMessage delegates.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1849](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1849): Call async methods when in an async method

When inside a Task-returning method, use the async version of methods, if they exist.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1850](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1850): Prefer static 'HashData' method over 'ComputeHash'

It is more efficient to use the static 'HashData' method over creating and managing a HashAlgorithm instance to call 'ComputeHash'.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1851](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1851): Possible multiple enumerations of 'IEnumerable' collection

Possible multiple enumerations of 'IEnumerable' collection. Consider using an implementation that avoids multiple enumerations.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1852](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1852): Seal internal types

When a type is not accessible outside its assembly and has no subtypes within its containing assembly, it can be safely sealed. Sealing types can improve performance.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Hidden|
|CodeFix|True|
---

## [CA1853](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1853): Unnecessary call to 'Dictionary.ContainsKey(key)'

Do not guard 'Dictionary.Remove(key)' with 'Dictionary.ContainsKey(key)'. The former already checks whether the key exists, and will not throw if it does not.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1854](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1854): Prefer the 'IDictionary.TryGetValue(TKey, out TValue)' method

Prefer a 'TryGetValue' call over a Dictionary indexer access guarded by a 'ContainsKey' check. 'ContainsKey' and the indexer both would lookup the key under the hood, so using 'TryGetValue' removes the extra lookup.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1855](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1855): Prefer 'Clear' over 'Fill'

It is more efficient to use 'Clear', instead of 'Fill' with default value.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1856](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1856): Incorrect usage of ConstantExpected attribute

ConstantExpected attribute is not applied correctly on the parameter.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Error|
|CodeFix|False|
---

## [CA1857](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1857): A constant is expected for the parameter

The parameter expects a constant for optimal performance.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA1858](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1858): Use 'StartsWith' instead of 'IndexOf'

It is both clearer and faster to use 'StartsWith' instead of comparing the result of 'IndexOf' to zero.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1859](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1859): Use concrete types when possible for improved performance

Using concrete types avoids virtual or interface call overhead and enables inlining.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1860](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1860): Avoid using 'Enumerable.Any()' extension method

Prefer using 'IsEmpty', 'Count' or 'Length' properties whichever available, rather than calling 'Enumerable.Any()'. The intent is clearer and it is more performant than using 'Enumerable.Any()' extension method.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1861](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1861): Avoid constant arrays as arguments

Constant arrays passed as arguments are not reused when called repeatedly, which implies a new array is created each time. Consider extracting them to 'static readonly' fields to improve performance if the passed array is not mutated within the called method.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1862](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1862): Use the 'StringComparison' method overloads to perform case-insensitive string comparisons

Avoid calling 'ToLower', 'ToUpper', 'ToLowerInvariant' and 'ToUpperInvariant' to perform case-insensitive string comparisons, as in 'string.ToLower() == string.ToLower()', because they lead to an allocation. Instead, use 'string.Equals(string, StringComparison)' to perform case-insensitive comparisons. Switching to using an overload that takes a 'StringComparison' might cause subtle changes in behavior, so it's important to conduct thorough testing after applying the suggestion. Additionally, if a culturally sensitive comparison is not required, consider using 'StringComparison.OrdinalIgnoreCase'.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1863](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1863): Use 'CompositeFormat'

Cache and use a 'CompositeFormat' instance as the argument to this formatting operation, rather than passing in the original format string. This reduces the cost of the formatting operation.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA1864](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1864): Prefer the 'IDictionary.TryAdd(TKey, TValue)' method

Prefer a 'TryAdd' call over an 'Add' call guarded by a 'ContainsKey' check. 'TryAdd' behaves the same as 'Add', except that when the specified key already exists, it returns 'false' instead of throwing an exception.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1865](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1865): Use char overload

The char overload is a better performing overload than a string with a single char.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1866](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1866): Use char overload

The char overload is a better performing overload than a string with a single char.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1867](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1867): Use char overload

The char overload is a better performing overload than a string with a single char.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA1868](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1868): Unnecessary call to 'Contains(item)'

Do not guard 'Add(item)' or 'Remove(item)' with 'Contains(item)' for the set. The former two already check whether the item exists and will return if it was added or removed.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA1869](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1869): Cache and reuse 'JsonSerializerOptions' instances

Avoid creating a new 'JsonSerializerOptions' instance for every serialization operation. Cache and reuse instances instead. Single use 'JsonSerializerOptions' instances can substantially degrade the performance of your application.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA1870](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1870): Use a cached 'SearchValues' instance

Using a cached 'SearchValues' instance is more efficient than passing values to 'IndexOfAny'/'ContainsAny' directly.

|Item|Value|
|-|-|
|Category|Performance|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA2000](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2000): Dispose objects before losing scope

If a disposable object is not explicitly disposed before all references to it are out of scope, the object will be disposed at some indeterminate time when the garbage collector runs the finalizer of the object. Because an exceptional event might occur that will prevent the finalizer of the object from running, the object should be explicitly disposed instead.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2002](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2002): Do not lock on objects with weak identity

An object is said to have a weak identity when it can be directly accessed across application domain boundaries. A thread that tries to acquire a lock on an object that has a weak identity can be blocked by a second thread in a different application domain that has a lock on the same object.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2007](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2007): Consider calling ConfigureAwait on the awaited task

When an asynchronous method awaits a Task directly, continuation occurs in the same thread that created the task. Consider calling Task.ConfigureAwait(Boolean) to signal your intention for continuation. Call ConfigureAwait(false) on the task to schedule continuations to the thread pool, thereby avoiding a deadlock on the UI thread. Passing false is a good option for app-independent libraries. Calling ConfigureAwait(true) on the task has the same behavior as not explicitly calling ConfigureAwait. By explicitly calling this method, you're letting readers know you intentionally want to perform the continuation on the original synchronization context.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA2008](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2008): Do not create tasks without passing a TaskScheduler

Do not create tasks unless you are using one of the overloads that takes a TaskScheduler. The default is to schedule on TaskScheduler.Current, which would lead to deadlocks. Either use TaskScheduler.Default to schedule on the thread pool, or explicitly pass TaskScheduler.Current to make your intentions clear.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2009](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2009): Do not call ToImmutableCollection on an ImmutableCollection value

Do not call {0} on an {1} value

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA2011](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2011): Avoid infinite recursion

Do not assign the property within its setter. This call might result in an infinite recursion.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA2012](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2012): Use ValueTasks correctly

ValueTasks returned from member invocations are intended to be directly awaited.  Attempts to consume a ValueTask multiple times or to directly access one's result before it's known to be completed may result in an exception or corruption.  Ignoring such a ValueTask is likely an indication of a functional bug and may degrade performance.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA2013](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2013): Do not use ReferenceEquals with value types

Value type typed arguments are uniquely boxed for each call to this method, therefore the result can be unexpected.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA2014](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2014): Do not use stackalloc in loops

Stack space allocated by a stackalloc is only released at the end of the current method's invocation.  Using it in a loop can result in unbounded stack growth and eventual stack overflow conditions.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA2015](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2015): Do not define finalizers for types derived from MemoryManager\<T>

Adding a finalizer to a type derived from MemoryManager\<T> may permit memory to be freed while it is still in use by a Span\<T>.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA2016](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2016): Forward the 'CancellationToken' parameter to methods

Forward the 'CancellationToken' parameter to methods to ensure the operation cancellation notifications gets properly propagated, or pass in 'CancellationToken.None' explicitly to indicate intentionally not propagating the token.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA2017](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2017): Parameter count mismatch

Number of parameters supplied in the logging message template do not match the number of named placeholders.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA2018](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2018): 'Buffer.BlockCopy' expects the number of bytes to be copied for the 'count' argument

'Buffer.BlockCopy' expects the number of bytes to be copied for the 'count' argument. Using 'Array.Length' may not match the number of bytes that needs to be copied.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA2019](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2019): Improper 'ThreadStatic' field initialization

'ThreadStatic' fields should be initialized lazily on use, not with inline initialization nor explicitly in a static constructor, which would only initialize the field on the thread that runs the type's static constructor.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA2020](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2020): Prevent behavioral change

Some built-in operators added in .NET 7 behave differently when overflowing than did the corresponding user-defined operators in .NET 6 and earlier versions. Some operators that previously threw in an unchecked context now don't throw unless wrapped within a checked context. Also, some operators that did not previously throw in a checked context now throw unless wrapped in an unchecked context.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA2021](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2021): Do not call Enumerable.Cast\<T> or Enumerable.OfType\<T> with incompatible types

Enumerable.Cast\<T> and Enumerable.OfType\<T> require compatible types to function expectedly.  

The generic cast (IL 'unbox.any') used by the sequence returned by Enumerable.Cast\<T> will throw InvalidCastException at runtime on elements of the types specified.  

The generic type check (C# 'is' operator/IL 'isinst') used by Enumerable.OfType\<T> will never succeed with elements of types specified, resulting in an empty sequence.  

Widening and user defined conversions are not supported with generic types.

|Item|Value|
|-|-|
|Category|Reliability|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA2100](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2100): Review SQL queries for security vulnerabilities

SQL queries that directly use user input can be vulnerable to SQL injection attacks. Review this SQL query for potential vulnerabilities, and consider using a parameterized SQL query.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2101](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2101): Specify marshaling for P/Invoke string arguments

A platform invoke member allows partially trusted callers, has a string parameter, and does not explicitly marshal the string. This can cause a potential security vulnerability.

|Item|Value|
|-|-|
|Category|Globalization|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA2119](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2119): Seal methods that satisfy private interfaces

An inheritable public type provides an overridable method implementation of an internal (Friend in Visual Basic) interface. To fix a violation of this rule, prevent the method from being overridden outside the assembly.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA2153](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2153): Do Not Catch Corrupted State Exceptions

Catching corrupted state exceptions could mask errors (such as access violations), resulting in inconsistent state of execution or making it easier for attackers to compromise system. Instead, catch and handle a more specific set of exception type(s) or re-throw the exception.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2200](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2200): Rethrow to preserve stack details

Re-throwing caught exception changes stack information

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Warning|
|CodeFix|True|
---

## [CA2201](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2201): Do not raise reserved exception types

An exception of type that is not sufficiently specific or reserved by the runtime should never be raised by user code. This makes the original error difficult to detect and debug. If this exception instance might be thrown, use a different exception type.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA2207](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2207): Initialize value type static fields inline

A value type declares an explicit static constructor. To fix a violation of this rule, initialize all static data when it is declared and remove the static constructor.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2208](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2208): Instantiate argument exceptions correctly

A call is made to the default (parameterless) constructor of an exception type that is or derives from ArgumentException, or an incorrect string argument is passed to a parameterized constructor of an exception type that is or derives from ArgumentException.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA2211](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2211): Non-constant fields should not be visible

Static fields that are neither constants nor read-only are not thread-safe. Access to such a field must be carefully controlled and requires advanced programming techniques to synchronize access to the class object.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA2213](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2213): Disposable fields should be disposed

A type that implements System.IDisposable declares fields that are of types that also implement IDisposable. The Dispose method of the field is not called by the Dispose method of the declaring type. To fix a violation of this rule, call Dispose on fields that are of types that implement IDisposable if you are responsible for allocating and releasing the unmanaged resources held by the field.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2214](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2214): Do not call overridable methods in constructors

Virtual methods defined on the class should not be called from constructors. If a derived class has overridden the method, the derived class version will be called (before the derived class constructor is called).

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2215](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2215): Dispose methods should call base class dispose

A type that implements System.IDisposable inherits from a type that also implements IDisposable. The Dispose method of the inheriting type does not call the Dispose method of the parent type. To fix a violation of this rule, call base.Dispose in your Dispose method.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Hidden|
|CodeFix|True|
---

## [CA2216](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2216): Disposable types should declare finalizer

A type that implements System.IDisposable and has fields that suggest the use of unmanaged resources does not implement a finalizer, as described by Object.Finalize.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2217](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2217): Do not mark enums with FlagsAttribute

An externally visible enumeration is marked by using FlagsAttribute, and it has one or more values that are not powers of two or a combination of the other defined values on the enumeration.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA2218](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2218): Override GetHashCode on overriding Equals

GetHashCode returns a value, based on the current instance, that is suited for hashing algorithms and data structures such as a hash table. Two objects that are the same type and are equal must return the same hash code.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA2219](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2219): Do not raise exceptions in finally clauses

When an exception is raised in a finally clause, the new exception hides the active exception. This makes the original error difficult to detect and debug.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA2224](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2224): Override Equals on overloading operator equals

A public type implements the equality operator but does not override Object.Equals.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA2225](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2225): Operator overloads have named alternates

An operator overload was detected, and the expected named alternative method was not found. The named alternative member provides access to the same functionality as the operator and is provided for developers who program in languages that do not support overloaded operators.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA2226](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2226): Operators should have symmetrical overloads

A type implements the equality or inequality operator and does not implement the opposite operator.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA2227](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2227): Collection properties should be read only

A writable collection property allows a user to replace the collection with a different collection. A read-only property stops the collection from being replaced but still allows the individual members to be set.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2231](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2231): Overload operator equals on overriding value type Equals

In most programming languages there is no default implementation of the equality operator (==) for value types. If your programming language supports operator overloads, you should consider implementing the equality operator. Its behavior should be identical to that of Equals.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA2234](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2234): Pass system uri objects instead of strings

A call is made to a method that has a string parameter whose name contains "uri", "URI", "urn", "URN", "url", or "URL". The declaring type of the method contains a corresponding method overload that has a System.Uri parameter.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2235](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2235): Mark all non-serializable fields

An instance field of a type that is not serializable is declared in a type that is serializable.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA2237](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2237): Mark ISerializable types with serializable

To be recognized by the common language runtime as serializable, types must be marked by using the SerializableAttribute attribute even when the type uses a custom serialization routine through implementation of the ISerializable interface.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|False|
|Severity|Warning|
|CodeFix|True|
---

## [CA2241](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2241): Provide correct arguments to formatting methods

The format argument that is passed to System.String.Format does not contain a format item that corresponds to each object argument, or vice versa.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA2242](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2242): Test for NaN correctly

This expression tests a value against Single.Nan or Double.Nan. Use Single.IsNan(Single) or Double.IsNan(Double) to test the value.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA2243](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2243): Attribute string literals should parse correctly

The string literal parameter of an attribute does not parse correctly for a URL, a GUID, or a version.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2244](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2244): Do not duplicate indexed element initializations

Indexed elements in objects initializers must initialize unique elements. A duplicate index might overwrite a previous element initialization.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA2245](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2245): Do not assign a property to itself

The property {0} should not be assigned to itself

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA2246](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2246): Assigning symbol and its member in the same statement

Assigning to a symbol and its member (field/property) in the same statement is not recommended. It is not clear if the member access was intended to use symbol's old value prior to the assignment or new value from the assignment in this statement. For clarity, consider splitting the assignments into separate statements.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA2247](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2247): Argument passed to TaskCompletionSource constructor should be TaskCreationOptions enum instead of TaskContinuationOptions enum

TaskCompletionSource has constructors that take TaskCreationOptions that control the underlying Task, and constructors that take object state that's stored in the task.  Accidentally passing a TaskContinuationOptions instead of a TaskCreationOptions will result in the call treating the options as state.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Warning|
|CodeFix|True|
---

## [CA2248](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2248): Provide correct 'enum' argument to 'Enum.HasFlag'

'Enum.HasFlag' method expects the 'enum' argument to be of the same 'enum' type as the instance on which the method is invoked and that this 'enum' is marked with 'System.FlagsAttribute'. If these are different 'enum' types, an unhandled exception will be thrown at runtime. If the 'enum' type is not marked with 'System.FlagsAttribute' the call will always return 'false' at runtime.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA2249](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2249): Consider using 'string.Contains' instead of 'string.IndexOf'

Calls to 'string.IndexOf' where the result is used to check for the presence/absence of a substring can be replaced by 'string.Contains'.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA2250](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2250): Use 'ThrowIfCancellationRequested'

'ThrowIfCancellationRequested' automatically checks whether the token has been canceled, and throws an 'OperationCanceledException' if it has.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|True|
---

## [CA2251](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2251): Use 'string.Equals'

It is both clearer and likely faster to use 'string.Equals' instead of comparing the result of 'string.Compare' to zero.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Hidden|
|CodeFix|True|
---

## [CA2252](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2252): This API requires opting into preview features

An assembly has to opt into preview features before using them.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Error|
|CodeFix|False|
---

## [CA2253](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2253): Named placeholders should not be numeric values

Named placeholders in the logging message template should not be comprised of only numeric characters.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA2254](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2254): Template should be a static expression

The logging message template should not vary between calls.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA2255](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2255): The 'ModuleInitializer' attribute should not be used in libraries

Module initializers are intended to be used by application code to ensure an application's components are initialized before the application code begins executing. If library code declares a method with the 'ModuleInitializerAttribute', it can interfere with application initialization and also lead to limitations in that application's trimming abilities. Instead of using methods marked with 'ModuleInitializerAttribute', the library should expose methods that can be used to initialize any components within the library and allow the application to invoke the method during application initialization.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA2256](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2256): All members declared in parent interfaces must have an implementation in a DynamicInterfaceCastableImplementation-attributed interface

Types attributed with 'DynamicInterfaceCastableImplementationAttribute' act as an interface implementation for a type that implements the 'IDynamicInterfaceCastable' type. As a result, it must provide an implementation of all of the members defined in the inherited interfaces, because the type that implements 'IDynamicInterfaceCastable' will not provide them otherwise.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Warning|
|CodeFix|True|
---

## [CA2257](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2257): Members defined on an interface with the 'DynamicInterfaceCastableImplementationAttribute' should be 'static'

Since a type that implements 'IDynamicInterfaceCastable' may not implement a dynamic interface in metadata, calls to an instance interface member that is not an explicit implementation defined on this type are likely to fail at runtime. Mark new interface members 'static' to avoid runtime errors.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Warning|
|CodeFix|True|
---

## [CA2258](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2258): Providing a 'DynamicInterfaceCastableImplementation' interface in Visual Basic is unsupported

Providing a functional 'DynamicInterfaceCastableImplementationAttribute'-attributed interface requires the Default Interface Members feature, which is unsupported in Visual Basic.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA2259](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2259): 'ThreadStatic' only affects static fields

'ThreadStatic' only affects static fields. When applied to instance fields, it has no impact on behavior.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA2260](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2260): Use correct type parameter

Generic math interfaces require the derived type itself to be used for the self recurring type parameter.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA2261](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2261): Do not use ConfigureAwaitOptions.SuppressThrowing with Task\<TResult>

The ConfigureAwaitOptions.SuppressThrowing option is only supported with the non-generic Task, not a Task\<TResult>.  To use it with a Task\<TResult>, first cast to the base Task.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Warning|
|CodeFix|False|
---

## [CA2262](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2262): Set 'MaxResponseHeadersLength' properly

The property 'MaxResponseHeadersLength' is measured in kilobytes, not in bytes. That mean the provided value will be multiplied by 1024, the result might be too high than your intended value.

|Item|Value|
|-|-|
|Category|Usage|
|Enabled|True|
|Severity|Info|
|CodeFix|False|
---

## [CA2300](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2300): Do not use insecure deserializer BinaryFormatter

The method '{0}' is insecure when deserializing untrusted data.  If you need to instead detect BinaryFormatter deserialization without a SerializationBinder set, then disable rule CA2300, and enable rules CA2301 and CA2302.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2301](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2301): Do not call BinaryFormatter.Deserialize without first setting BinaryFormatter.Binder

The method '{0}' is insecure when deserializing untrusted data without a SerializationBinder to restrict the type of objects in the deserialized object graph.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2302](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2302): Ensure BinaryFormatter.Binder is set before calling BinaryFormatter.Deserialize

The method '{0}' is insecure when deserializing untrusted data without a SerializationBinder to restrict the type of objects in the deserialized object graph.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2305](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2305): Do not use insecure deserializer LosFormatter

The method '{0}' is insecure when deserializing untrusted data.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2310](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2310): Do not use insecure deserializer NetDataContractSerializer

The method '{0}' is insecure when deserializing untrusted data.  If you need to instead detect NetDataContractSerializer deserialization without a SerializationBinder set, then disable rule CA2310, and enable rules CA2311 and CA2312.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2311](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2311): Do not deserialize without first setting NetDataContractSerializer.Binder

The method '{0}' is insecure when deserializing untrusted data without a SerializationBinder to restrict the type of objects in the deserialized object graph.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2312](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2312): Ensure NetDataContractSerializer.Binder is set before deserializing

The method '{0}' is insecure when deserializing untrusted data without a SerializationBinder to restrict the type of objects in the deserialized object graph.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2315](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2315): Do not use insecure deserializer ObjectStateFormatter

The method '{0}' is insecure when deserializing untrusted data.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2321](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2321): Do not deserialize with JavaScriptSerializer using a SimpleTypeResolver

The method '{0}' is insecure when deserializing untrusted data with a JavaScriptSerializer initialized with a SimpleTypeResolver. Initialize JavaScriptSerializer without a JavaScriptTypeResolver specified, or initialize with a JavaScriptTypeResolver that limits the types of objects in the deserialized object graph.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2322](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2322): Ensure JavaScriptSerializer is not initialized with SimpleTypeResolver before deserializing

The method '{0}' is insecure when deserializing untrusted data with a JavaScriptSerializer initialized with a SimpleTypeResolver. Ensure that the JavaScriptSerializer is initialized without a JavaScriptTypeResolver specified, or initialized with a JavaScriptTypeResolver that limits the types of objects in the deserialized object graph.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2326](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2326): Do not use TypeNameHandling values other than None

Deserializing JSON when using a TypeNameHandling value other than None can be insecure.  If you need to instead detect Json.NET deserialization when a SerializationBinder isn't specified, then disable rule CA2326, and enable rules CA2327, CA2328, CA2329, and CA2330.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2327](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2327): Do not use insecure JsonSerializerSettings

When deserializing untrusted input, allowing arbitrary types to be deserialized is insecure.  When using JsonSerializerSettings, use TypeNameHandling.None, or for values other than None, restrict deserialized types with a SerializationBinder.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2328](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2328): Ensure that JsonSerializerSettings are secure

When deserializing untrusted input, allowing arbitrary types to be deserialized is insecure.  When using JsonSerializerSettings, ensure TypeNameHandling.None is specified, or for values other than None, ensure a SerializationBinder is specified to restrict deserialized types.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2329](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2329): Do not deserialize with JsonSerializer using an insecure configuration

When deserializing untrusted input, allowing arbitrary types to be deserialized is insecure. When using deserializing JsonSerializer, use TypeNameHandling.None, or for values other than None, restrict deserialized types with a SerializationBinder.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2330](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2330): Ensure that JsonSerializer has a secure configuration when deserializing

When deserializing untrusted input, allowing arbitrary types to be deserialized is insecure. When using deserializing JsonSerializer, use TypeNameHandling.None, or for values other than None, restrict deserialized types with a SerializationBinder.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2350](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2350): Do not use DataTable.ReadXml() with untrusted data

The method '{0}' is insecure when deserializing untrusted data

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2351](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2351): Do not use DataSet.ReadXml() with untrusted data

The method '{0}' is insecure when deserializing untrusted data

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2352](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2352): Unsafe DataSet or DataTable in serializable type can be vulnerable to remote code execution attacks

When deserializing untrusted input with an IFormatter-based serializer, deserializing a {0} object is insecure. '{1}' either is or derives from {0}.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2353](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2353): Unsafe DataSet or DataTable in serializable type

When deserializing untrusted input, deserializing a {0} object is insecure. '{1}' either is or derives from {0}

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2354](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2354): Unsafe DataSet or DataTable in deserialized object graph can be vulnerable to remote code execution attacks

When deserializing untrusted input, deserializing a {0} object is insecure. '{1}' either is or derives from {0}

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2355](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2355): Unsafe DataSet or DataTable type found in deserializable object graph

When deserializing untrusted input, deserializing a {0} object is insecure. '{1}' either is or derives from {0}

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2356](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2356): Unsafe DataSet or DataTable type in web deserializable object graph

When deserializing untrusted input, deserializing a {0} object is insecure. '{1}' either is or derives from {0}

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2361](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2361): Ensure auto-generated class containing DataSet.ReadXml() is not used with untrusted data

The method '{0}' is insecure when deserializing untrusted data. Make sure that auto-generated class containing the '{0}' call is not deserialized with untrusted data.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA2362](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2362): Unsafe DataSet or DataTable in auto-generated serializable type can be vulnerable to remote code execution attacks

When deserializing untrusted input with an IFormatter-based serializer, deserializing a {0} object is insecure. '{1}' either is or derives from {0}. Ensure that the auto-generated type is never deserialized with untrusted data.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA3001](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3001): Review code for SQL injection vulnerabilities

Potential SQL injection vulnerability was found where '{0}' in method '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA3002](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3002): Review code for XSS vulnerabilities

Potential cross-site scripting (XSS) vulnerability was found where '{0}' in method '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA3003](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3003): Review code for file path injection vulnerabilities

Potential file path injection vulnerability was found where '{0}' in method '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA3004](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3004): Review code for information disclosure vulnerabilities

Potential information disclosure vulnerability was found where '{0}' in method '{1}' may contain unintended information from '{2}' in method '{3}'.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA3005](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3005): Review code for LDAP injection vulnerabilities

Potential LDAP injection vulnerability was found where '{0}' in method '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA3006](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3006): Review code for process command injection vulnerabilities

Potential process command injection vulnerability was found where '{0}' in method '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA3007](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3007): Review code for open redirect vulnerabilities

Potential open redirect vulnerability was found where '{0}' in method '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA3008](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3008): Review code for XPath injection vulnerabilities

Potential XPath injection vulnerability was found where '{0}' in method '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA3009](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3009): Review code for XML injection vulnerabilities

Potential XML injection vulnerability was found where '{0}' in method '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA3010](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3010): Review code for XAML injection vulnerabilities

Potential XAML injection vulnerability was found where '{0}' in method '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA3011](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3011): Review code for DLL injection vulnerabilities

Potential DLL injection vulnerability was found where '{0}' in method '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA3012](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3012): Review code for regex injection vulnerabilities

Potential regex injection vulnerability was found where '{0}' in method '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA3061](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3061): Do Not Add Schema By URL

This overload of XmlSchemaCollection.Add method internally enables DTD processing on the XML reader instance used, and uses UrlResolver for resolving external XML entities. The outcome is information disclosure. Content from file system or network shares for the machine processing the XML can be exposed to attacker. In addition, an attacker can use this as a DoS vector.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA3075](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3075): Insecure DTD processing in XML

Using XmlTextReader.Load(), creating an insecure XmlReaderSettings instance when invoking XmlReader.Create(), setting the InnerXml property of the XmlDocument and enabling DTD processing using XmlUrlResolver insecurely can lead to information disclosure. Replace it with a call to the Load() method overload that takes an XmlReader instance, use XmlReader.Create() to accept XmlReaderSettings arguments or consider explicitly setting secure values. The DataViewSettingCollectionString property of DataViewManager should always be assigned from a trusted source, the DtdProcessing property should be set to false, and the XmlResolver property should be changed to XmlSecureResolver or null.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA3076](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3076): Insecure XSLT script processing

Providing an insecure XsltSettings instance and an insecure XmlResolver instance to XslCompiledTransform.Load method is potentially unsafe as it allows processing script within XSL, which on an untrusted XSL input may lead to malicious code execution. Either replace the insecure XsltSettings argument with XsltSettings.Default or an instance that has disabled document function and script execution, or replace the XmlResolver argument with null or an XmlSecureResolver instance. This message may be suppressed if the input is known to be from a trusted source and external resource resolution from locations that are not known in advance must be supported.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA3077](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3077): Insecure Processing in API Design, XmlDocument and XmlTextReader

Enabling DTD processing on all instances derived from XmlTextReader or  XmlDocument and using XmlUrlResolver for resolving external XML entities may lead to information disclosure. Ensure to set the XmlResolver property to null, create an instance of XmlSecureResolver when processing untrusted input, or use XmlReader.Create method with a secure XmlReaderSettings argument. Unless you need to enable it, ensure the DtdProcessing property is set to false.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA3147](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3147): Mark Verb Handlers With Validate Antiforgery Token

Missing ValidateAntiForgeryTokenAttribute on controller action {0}

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5350](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5350): Do Not Use Weak Cryptographic Algorithms

Cryptographic algorithms degrade over time as attacks become for advances to attacker get access to more computation. Depending on the type and application of this cryptographic algorithm, further degradation of the cryptographic strength of it may allow attackers to read enciphered messages, tamper with enciphered  messages, forge digital signatures, tamper with hashed content, or otherwise compromise any cryptosystem based on this algorithm. Replace encryption uses with the AES algorithm (AES-256, AES-192 and AES-128 are acceptable) with a key length greater than or equal to 128 bits. Replace hashing uses with a hashing function in the SHA-2 family, such as SHA-2 512, SHA-2 384, or SHA-2 256.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5351](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5351): Do Not Use Broken Cryptographic Algorithms

An attack making it computationally feasible to break this algorithm exists. This allows attackers to break the cryptographic guarantees it is designed to provide. Depending on the type and application of this cryptographic algorithm, this may allow attackers to read enciphered messages, tamper with enciphered  messages, forge digital signatures, tamper with hashed content, or otherwise compromise any cryptosystem based on this algorithm. Replace encryption uses with the AES algorithm (AES-256, AES-192 and AES-128 are acceptable) with a key length greater than or equal to 128 bits. Replace hashing uses with a hashing function in the SHA-2 family, such as SHA512, SHA384, or SHA256. Replace digital signature uses with RSA with a key length greater than or equal to 2048-bits, or ECDSA with a key length greater than or equal to 256 bits.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5358](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5358): Review cipher mode usage with cryptography experts

These cipher modes might be vulnerable to attacks. Consider using recommended modes (CBC, CTS).

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5359](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5359): Do Not Disable Certificate Validation

A certificate can help authenticate the identity of the server. Clients should validate the server certificate to ensure requests are sent to the intended server. If the ServerCertificateValidationCallback always returns 'true', any certificate will pass validation.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5360](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5360): Do Not Call Dangerous Methods In Deserialization

Insecure Deserialization is a vulnerability which occurs when untrusted data is used to abuse the logic of an application, inflict a Denial-of-Service (DoS) attack, or even execute arbitrary code upon it being deserialized. It’s frequently possible for malicious users to abuse these deserialization features when the application is deserializing untrusted data which is under their control. Specifically, invoke dangerous methods in the process of deserialization. Successful insecure deserialization attacks could allow an attacker to carry out attacks such as DoS attacks, authentication bypasses, and remote code execution.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5361](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5361): Do Not Disable SChannel Use of Strong Crypto

Starting with the .NET Framework 4.6, the System.Net.ServicePointManager and System.Net.Security.SslStream classes are recommended to use new protocols. The old ones have protocol weaknesses and are not supported. Setting Switch.System.Net.DontEnableSchUseStrongCrypto with true will use the old weak crypto check and opt out of the protocol migration.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5362](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5362): Potential reference cycle in deserialized object graph

Review code that processes untrusted deserialized data for handling of unexpected reference cycles. An unexpected reference cycle should not cause the code to enter an infinite loop. Otherwise, an unexpected reference cycle can allow an attacker to DOS or exhaust the memory of the process when deserializing untrusted data.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5363](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5363): Do Not Disable Request Validation

Request validation is a feature in ASP.NET that examines HTTP requests and determines whether they contain potentially dangerous content. This check adds protection from markup or code in the URL query string, cookies, or posted form values that might have been added for malicious purposes. So, it is generally desirable and should be left enabled for defense in depth.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5364](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5364): Do Not Use Deprecated Security Protocols

Using a deprecated security protocol rather than the system default is risky.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5365](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5365): Do Not Disable HTTP Header Checking

HTTP header checking enables encoding of the carriage return and newline characters, \r and \n, that are found in response headers. This encoding can help to avoid injection attacks that exploit an application that echoes untrusted data contained by the header.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5366](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5366): Use XmlReader for 'DataSet.ReadXml()'

Processing XML from untrusted data may load dangerous external references, which should be restricted by using an XmlReader with a secure resolver or with DTD processing disabled.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5367](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5367): Do Not Serialize Types With Pointer Fields

Pointers are not "type safe" in the sense that you cannot guarantee the correctness of the memory they point at. So, serializing types with pointer fields is dangerous, as it may allow an attacker to control the pointer.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5368](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5368): Set ViewStateUserKey For Classes Derived From Page

Setting the ViewStateUserKey property can help you prevent attacks on your application by allowing you to assign an identifier to the view-state variable for individual users so that they cannot use the variable to generate an attack. Otherwise, there will be cross-site request forgery vulnerabilities.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5369](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5369): Use XmlReader for 'XmlSerializer.Deserialize()'

Processing XML from untrusted data may load dangerous external references, which should be restricted by using an XmlReader with a secure resolver or with DTD processing disabled.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5370](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5370): Use XmlReader for XmlValidatingReader constructor

Processing XML from untrusted data may load dangerous external references, which should be restricted by using an XmlReader with a secure resolver or with DTD processing disabled.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5371](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5371): Use XmlReader for 'XmlSchema.Read()'

Processing XML from untrusted data may load dangerous external references, which should be restricted by using an XmlReader with a secure resolver or with DTD processing disabled.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5372](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5372): Use XmlReader for XPathDocument constructor

Processing XML from untrusted data may load dangerous external references, which should be restricted by using an XmlReader with a secure resolver or with DTD processing disabled.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5373](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5373): Do not use obsolete key derivation function

Password-based key derivation should use PBKDF2 with SHA-2. Avoid using PasswordDeriveBytes since it generates a PBKDF1 key. Avoid using Rfc2898DeriveBytes.CryptDeriveKey since it doesn't use the iteration count or salt.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5374](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5374): Do Not Use XslTransform

Do not use XslTransform. It does not restrict potentially dangerous external references.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5375](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5375): Do Not Use Account Shared Access Signature

Shared Access Signatures(SAS) are a vital part of the security model for any application using Azure Storage, they should provide limited and safe permissions to your storage account to clients that don't have the account key. All of the operations available via a service SAS are also available via an account SAS, that is, account SAS is too powerful. So it is recommended to use Service SAS to delegate access more carefully.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5376](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5376): Use SharedAccessProtocol HttpsOnly

HTTPS encrypts network traffic. Use HttpsOnly, rather than HttpOrHttps, to ensure network traffic is always encrypted to help prevent disclosure of sensitive data.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5377](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5377): Use Container Level Access Policy

No access policy identifier is specified, making tokens non-revocable.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5378](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5378): Do not disable ServicePointManagerSecurityProtocols

Do not set Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols to true.  Setting this switch limits Windows Communication Framework (WCF) to using Transport Layer Security (TLS) 1.0, which is insecure and obsolete.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5379](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5379): Ensure Key Derivation Function algorithm is sufficiently strong

Some implementations of the Rfc2898DeriveBytes class allow for a hash algorithm to be specified in a constructor parameter or overwritten in the HashAlgorithm property. If a hash algorithm is specified, then it should be SHA-256 or higher.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5380](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5380): Do Not Add Certificates To Root Store

By default, the Trusted Root Certification Authorities certificate store is configured with a set of public CAs that has met the requirements of the Microsoft Root Certificate Program. Since all trusted root CAs can issue certificates for any domain, an attacker can pick a weak or coercible CA that you install by yourself to target for an attack - and a single vulnerable, malicious or coercible CA undermines the security of the entire system. To make matters worse, these attacks can go unnoticed quite easily.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5381](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5381): Ensure Certificates Are Not Added To Root Store

By default, the Trusted Root Certification Authorities certificate store is configured with a set of public CAs that has met the requirements of the Microsoft Root Certificate Program. Since all trusted root CAs can issue certificates for any domain, an attacker can pick a weak or coercible CA that you install by yourself to target for an attack - and a single vulnerable, malicious or coercible CA undermines the security of the entire system. To make matters worse, these attacks can go unnoticed quite easily.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5382](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5382): Use Secure Cookies In ASP.NET Core

Applications available over HTTPS must use secure cookies.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5383](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5383): Ensure Use Secure Cookies In ASP.NET Core

Applications available over HTTPS must use secure cookies.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5384](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5384): Do Not Use Digital Signature Algorithm (DSA)

DSA is too weak to use.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5385](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5385): Use Rivest-Shamir-Adleman (RSA) Algorithm With Sufficient Key Size

Encryption algorithms are vulnerable to brute force attacks when too small a key size is used.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5386](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5386): Avoid hardcoding SecurityProtocolType value

Avoid hardcoding SecurityProtocolType {0}, and instead use SecurityProtocolType.SystemDefault to allow the operating system to choose the best Transport Layer Security protocol to use.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5387](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5387): Do Not Use Weak Key Derivation Function With Insufficient Iteration Count

When deriving cryptographic keys from user-provided inputs such as password, use sufficient iteration count (at least 100k).

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5388](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5388): Ensure Sufficient Iteration Count When Using Weak Key Derivation Function

When deriving cryptographic keys from user-provided inputs such as password, use sufficient iteration count (at least 100k).

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5389](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5389): Do Not Add Archive Item's Path To The Target File System Path

When extracting files from an archive and using the archive item's path, check if the path is safe. Archive path can be relative and can lead to file system access outside of the expected file system target path, leading to malicious config changes and remote code execution via lay-and-wait technique.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5390](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5390): Do not hard-code encryption key

SymmetricAlgorithm's .Key property, or a method's rgbKey parameter, should never be a hard-coded value.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5391](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5391): Use antiforgery tokens in ASP.NET Core MVC controllers

Handling a POST, PUT, PATCH, or DELETE request without validating an antiforgery token may be vulnerable to cross-site request forgery attacks. A cross-site request forgery attack can send malicious requests from an authenticated user to your ASP.NET Core MVC controller.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5392](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5392): Use DefaultDllImportSearchPaths attribute for P/Invokes

By default, P/Invokes using DllImportAttribute probe a number of directories, including the current working directory for the library to load. This can be a security issue for certain applications, leading to DLL hijacking.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5393](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5393): Do not use unsafe DllImportSearchPath value

There could be a malicious DLL in the default DLL search directories. Or, depending on where your application is run from, there could be a malicious DLL in the application's directory. Use a DllImportSearchPath value that specifies an explicit search path instead. The DllImportSearchPath flags that this rule looks for can be configured in .editorconfig.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5394](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5394): Do not use insecure randomness

Using a cryptographically weak pseudo-random number generator may allow an attacker to predict what security-sensitive value will be generated. Use a cryptographically strong random number generator if an unpredictable value is required, or ensure that weak pseudo-random numbers aren't used in a security-sensitive manner.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5395](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5395): Miss HttpVerb attribute for action methods

All the methods that create, edit, delete, or otherwise modify data do so in the [HttpPost] overload of the method, which needs to be protected with the anti forgery attribute from request forgery. Performing a GET operation should be a safe operation that has no side effects and doesn't modify your persisted data.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5396](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5396): Set HttpOnly to true for HttpCookie

As a defense in depth measure, ensure security sensitive HTTP cookies are marked as HttpOnly. This indicates web browsers should disallow scripts from accessing the cookies. Injected malicious scripts are a common way of stealing cookies.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5397](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5397): Do not use deprecated SslProtocols values

Older protocol versions of Transport Layer Security (TLS) are less secure than TLS 1.2 and TLS 1.3, and are more likely to have new vulnerabilities. Avoid older protocol versions to minimize risk.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|True|
|Severity|Hidden|
|CodeFix|False|
---

## [CA5398](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5398): Avoid hardcoded SslProtocols values

Current Transport Layer Security protocol versions may become deprecated if vulnerabilities are found. Avoid hardcoding SslProtocols values to keep your application secure. Use 'None' to let the Operating System choose a version.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5399](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5399): HttpClients should enable certificate revocation list checks

Using HttpClient without providing a platform specific handler (WinHttpHandler or CurlHandler or HttpClientHandler) where the CheckCertificateRevocationList property is set to true, will allow revoked certificates to be accepted by the HttpClient as valid.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5400](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5400): Ensure HttpClient certificate revocation list check is not disabled

Using HttpClient without providing a platform specific handler (WinHttpHandler or CurlHandler or HttpClientHandler) where the CheckCertificateRevocationList property is set to true, will allow revoked certificates to be accepted by the HttpClient as valid.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5401](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5401): Do not use CreateEncryptor with non-default IV

Symmetric encryption should always use a non-repeatable initialization vector to prevent dictionary attacks.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5402](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5402): Use CreateEncryptor with the default IV

Symmetric encryption should always use a non-repeatable initialization vector to prevent dictionary attacks.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5403](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5403): Do not hard-code certificate

Hard-coded certificates in source code are vulnerable to being exploited.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5404](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5404): Do not disable token validation checks

Token validation checks ensure that while validating tokens, all aspects are analyzed and verified. Turning off validation can lead to security holes by allowing untrusted tokens to make it through validation.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---

## [CA5405](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5405): Do not always skip token validation in delegates

By setting critical TokenValidationParameter validation delegates to true, important authentication safeguards are disabled which can lead to tokens from any issuer or expired tokens being wrongly validated.

|Item|Value|
|-|-|
|Category|Security|
|Enabled|False|
|Severity|Warning|
|CodeFix|False|
---
