## Package Validation

With .NET Core & Xamarin we have made cross-platform a mainstream requirement for library authors. However, we lack validation tooling which can result in packages that don't work well which in turn hurts our ecosystem. This is especially problematic for emerging platforms where adoption isn't high enough to warrant special attention by library authors.

The tooling we provide as part of the SDK has close to zero validation that multi-targeted packages are well-formed. For example, a package that multi-targets for .NET 6.0 and .NET Standard 2.0 needs to ensure that code compiled against the .NET Standard 2.0 binary can run against the binary that is produced for .NET 6.0. We have seen this issue in the wild, even with 1st parties, for example, the Azure AD libraries.

Another common issue in the .NET ecosystem is that binary breaking changes aren't well understood. Developers tend to think of "does the source still compile" as the bar to determine whether an API is compatible. However, at the API level certain changes that are fine in C# aren't compatible at the level of IL, for example, adding a defaulted parameter or changing the value of a constant.

Package Validation tooling will allow library developers to validate that their packages are well-formed and they they didn't make unintentional breaking changes.

# How To Add Package Validation To Your Projects

Package Validation is currently being shipped as a msbuild sdk package which can be consumed in the csproj. It is set of tasks and targets that run after the dotnet pack
The csproj will look like

```C#
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net6.0</TargetFrameworks>
  </PropertyGroup>

  <Sdk Name="Microsoft.DotNet.PackageValidation" Version="1.0.0-preview4.21121.3" />
</Project>
```

## Scenarios and User Experience

The first preview version of the package validation covers the basic scenarios for package validation. They are as follows:-

## Validation Among Compatible Frameworks

Package containing compatible frameworks need to ensure that code compiled against one can run against other. Examples of compatible pairs will be .NET Standard2.0 and .NET 6.0, .NET 5.0 and .NET 6.0 . In both of these cases, the users can compile against .NET Standard 2.0 or NET 5.0 and try to run on .NET 6.0, if the binaries are not compatible the users might end up with runtime errors.

Package Validation will catch these errors at pack time. eg.

Finley is working on a game which do a lot of string manipulation. They need to support both .NET Framework and .NET Core customers. They started with just targeting .NET Standard 2.0 but now they realize they want to take advantage of the spans in .NET Core 3.0 and above to avoid unnecessary string allocations. In order to do that, they are multi-targeting for NET Standard2.0 and .NET 6.0,.

Finley has written the following code:
```c#
#if NET6_0_OR_GREATER
    public void DoStringManipulation(ReadOnlySpan<char> input)
    {
        // use spans to do string operations.
    }
#else
    public void DoStringManipulation(string input)
    {
        // Do some string operations.
    }
#endif
```

When Finley packs the project it fails with the following error:

```c#
error : .NETStandard,Version=v2.0 assembly api surface area should be compatible with net6.0 assembly surface area so we can compile against .NETStandard,Version=v2.0 and run on net6.0 .framework.
error : API Compatibility errors between lib/netstandard2.0/A.dll (left) and lib/net6.0/A.dll (right):
error CP0002: Member 'A.B.DoStringManipulation(string)' exists on the left but not on the right.
```

Finley understands that they shouldn't exclude ```DoStringManipulation(string)``` but instead just provide an additional ```DoStringManipulation(ReadOnlySpan<char>)``` method for .NET 6.0 and changes the code accordingly:

```c#
#if NET6_0_OR_GREATER
    public void DoStringManipulation(ReadOnlySpan<char> input)
    {
        // use spans to do string operations.
    }
#endif
    public void DoStringManipulation(string input)
    {
        // Do some string operations.
    }
```

## Validation Against Baseline Package Version

Package Validation helps to validate the latest version of the package with a previous released stable version of the package. This is an opt in feature. The user will need to add the ```EnablePackageBaselineValidation``` to the csproj.

Package validation will detect any breaking changes on any of the shipped target frameworks and will also detect if any target framework support has been dropped.

eg Tom works on the AdventureWorks.Client NuGet package. They want to make sure that they don't accidentally make breaking changes so they configure their project to instruct the package validation tooling to run API compatibility on the previous version of the package.

```csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <PackageVersion>2.0.0</PackageVersion>
    <EnablePackageBaselineValidation>true</EnablePackageBaselineValidation>
  </PropertyGroup>

  <Sdk Name="Microsoft.DotNet.PackageValidation" Version="1.0.0-preview4.21111.2" />
</Project>
```

A few weeks later, Tom is tasked with adding support for a connection timeout to their library. The Connect method looks like this right now:

```C#
public static HttpClient Connect(string url)
{
    // ...
}
```

Since a connection timeout is an advanced configuration setting they reckon they can just add an optional parameter:

```c#
public static HttpClient Connect(string url, TimeSpan? timeout = default)
{
    // ...
}
```

However, when they rebuild the package they are getting an error:
```c#
error : There are breaking changes between the versions. Please add or modify the apis in the recent version or suppress the intentional breaking changes. 
error : API compatibility errors in between lib/net6.0/A.dll (left) and lib/net6.0/A.dll (right) for versions 1.0.0 and 2.0.0 respectively:
error CP0002: Member 'A.B.Connect(string)' exists on the left but not on the right 
```

Tom realizes that while this is not a source breaking change, it's a binary breaking change. They solve this problem by adding an overload instead:
```
public static HttpClient Connect(string url)
{
    return HttpClient(url, Timeout.InfiniteTimeSpan);
}

public static HttpClient Connect(string url, TimeSpan timeout)
{
    // ...
}
```

## Validation Against Different Runtimes

.Net Packages can contain different implementation assemblies for different runtimes. The developer needs to make sure that these assemblies are compatible with the compile time assemblies.

Rohan is working on a library involving some interop calls to unix and windows api respectively. The source code looks like

```c#
#if Unix
    public static void Open(string path, bool securityDescriptor)
    {
        // call unix specific stuff
    }
#else
    public static void Open(string path)
    {
        // call windows specific stuff
    }
#endif
```

The resuting package structure looks like

```
lib -> net6.0 -> A.dll 
runtimes -> unix -> lib -> net6.0 -> A.dll
```

```lib\net6.0\A.dll``` will always be used at compile time irrespective of the underlying operating system. ```lib\net6.0\A.dll``` will be used at runtime for Non-Unix systems and ```runtimes\unix\lib\net6.0\A.dll``` will be used at runtime for Unix system.

When Rohan tries to pack this project, he gets an error:

```
error : The compile time assemblies public api surface area should be compatible with the runtime assemblies for all target frameworks and RIDs. Please add the following apis to the runtime assemblies.
error : API Compatibility errors between lib/net6.0/A.dll (left) and runtimes/unix/lib/net6.0/A.dll (right): 
error CP0002: Member 'A.B.Open(string)' exists on the left but not on the right.
```

Rohan quickly realizes his mistake and add ```A.B.Open(string)``` to the unix runtime as well.

```c#
#if Unix
    public static void Open(string path, bool securityDescriptor)
    {
        // call unix specific stuff
    }
    
    public static void Open(string path)
    {
        // throw not supported exception
    }
#else
    public static void Open(string path)
    {
        // call windows specific stuff
    }
#endif
```

## Whats Next For Package Validation

- Error Suppression for Package Validation Errors eg Intentional breaking changes between versions.
- More Compatibilty Rules eg compatible assembly versions, nullability.
- Validating Package Dependencies.