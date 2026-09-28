# Porting Managed Code Analysis Rules to Roslyn

Visual Studio 2015 shipped with over 300 Code Analysis rules for managed code. These rules were written using an MSIL-based analysis engine. Historically, this was valuable because it enabled the rules to apply to assemblies built from any managed language, including C#, VB, and managed C++. We are now engaged in an effort to rewrite some of these rules as Roslyn analyzers. Roslyn covers only C# and VB, but it has the following benefits:

* You get live analysis as you type in VS.

* Roslyn analyzers can be accompanied by fixers.

However, we do not envision the new Roslyn-based managed analysis rules as a strict port of the FxCop rules, for various reasons:

* FxCop includes rules related to a variety of quality concerns (standardization of public API conventions, correct usage of core BCL classes, internationalization, performance, security, etc.). This suggests that we can profitably unbundle the FxCop rules into a collection of packages, each serving a clearly defined purpose, and allow developers to select the packages that meet their needs.

* To many people, the name "FxCop" means _nothing_. VS's customers just see a mass of Code Analysis rules, and there's no mention of FxCop anywhere in VS. (The only place the name appears is in the name of the command line tool FxCopCmd.exe.) Customers care mostly about getting some guidance from static analysis. But they can't figure out which of the 300+ rules matter to them, because the rules are not arranged in useful groups (other than the category, which is rather generic).

* Most of the rules in VS today were written about 10 years ago. Platforms and guidelines have evolved since then. Many of the rules either don't make sense or aren't that valuable any more. For example, the introduction of generics has rendered many of the rules obsolete, as has the deprecation of CAS (Code Access Security) Policy and Security-Transparent Code. Experience has shown that other rules provide limited value and/or are a source of noise (false positives).

For these reasons, we stopped thinking about these rules as "FxCop analyzers". Instead, we looked at the inventory of all the rules that exist today, and factored them according to the APIs they relate to and the purposes they serve. As part of this exercise, we identified the rules that provided the highest value. We chose to implement only those rules as analyzers, and not to re-implement low-value rules. In addition, we are adding new rules to fill the gaps that have appeared in the last 10 years, for example, rules related to `async` or `ImmutableCollections`.

In the remainder of this document, we explain the principles we used to decide how to factor the new Roslyn-based analyzers, enumerate the specific NuGet packages into which the analyzers will be factored, and describe in a little more detail how we decided which FxCop rules to port.

## Factoring principles

* In the spirit of [Code-Aware libraries](https://channel9.msdn.com/Events/Build/2015/3-725), if a rule is about the usage of a specific API, and the rule doesn't make sense if that API is not referenced, then that rule should ship with that API. For example, rules about `ImmutableArray` (which resides in System.Collections.Immutable.dll) should reside in an analyzer assembly System.Collections.Immutable.Analyzers.dll, which would be included in the System.Collections.Immutable NuGet package.

* Some types reside in different .NET assemblies, depending on which flavor of .NET you use. For example, in the .NET Framework, `IDisposable` resides in mscorlib.dll, whereas in [.NET Core](http://blogs.msdn.com/b/dotnet/archive/2014/11/12/net-core-is-open-source.aspx), it resides in System.Runtime.dll. Where should we place analyzers that examine uses of `IDisposable`: in mscorlib.Analyzers.dll or in System.Runtime.Analyzers.dll? We should choose the .NET Core version of the types; that is, we should place the `IDisposable` analyzers in System.Runtime.Analyzers.dll.

    The rationale for this choice is that developers using .NET Core, which is delivered as a set of NuGet packages, will automatically get exactly the API-specific analyzers they need. Developers using .NET Framework will still need to manually download the API-specific analyzers. For those developers, we might consider creating a consolidated NuGet package containing the analyzers for all types in the .NET framework. By doing these two things, we minimize the number of times developers have to search for and download API-specific analyzer packages.

* Rules that do not relate to the usage of specific APIs, but relate instead to more general coding guidelines, should be organized according to the intended purpose of those guidelines. For example, some rules might help API authors produce consistent public APIs, but those rules might not make sense for test assemblies. (We will package those analyzers in Microsoft.ApiDesignGuidelines.Analyzers.dll.) As another example, there might be some rules that restrict the expressiveness of the language (by discouraging the use of certain language features) in order to gain a performance advantage. Such rules would only apply in a specific context where that tradeoff is acceptable, and hence it would be useful to place them in a separate NuGet package.

## Analyzer packages

The list of all the rules that ship in VS, along with certain other FxCop/Roslyn rules that we know of, is captured in the file [RulesInventory.csv](https://github.com/dotnet/roslyn-analyzers/blob/main/docs/FxCopPort/RulesInventory.csv) file (which, thanks to GitHub, is searchable). That file also contains our proposed factoring of the analyzers (in the "Proposed Analyzer" column, which perhaps might have been better named "Proposed Analyzer Package").

### API analyzer packages

There are rules about types in the following contract assemblies:

* **System.Runtime.Analyzers** - This package already exists

* **System.Runtime.InteropServices.Analyzers** - Contains analyzers related to interop and marshalling. This package already exists.

* **System.Security.Cryptography.Algorithms.Analyzers** - Contains analyzers with guidelines for crypto algorithm usage. This is a new package.

* **System.Xml.Analyzers** - Contains analyzers for types dealing with XML across  the System.Xml.* contracts. This is a new package.

* **Desktop.Analyzers** - Contains analyzers for APIs that are present in the desktop .NET Framework but not in the new .NET Core API set. Since the .NET framework isn't available in a piecemeal fashion, there's not much value in breaking this down further.

* **Microsoft.CodeAnalysis.Analyzers** - Contains analyzers related to using the Roslyn APIs correctly. Analyzer authors would use these rules; we refer to them informally as "analyzer analyzers." This package already exists.

### Theme-based analyzer packages

* **Microsoft.ApiDesignGuidelines.Analyzers** - Contains guidelines for authoring libraries which contain public APIs. The advantage of factoring it out this way is that one could simply install this analyzer for projects that expose real public APIs, and not for executables and test projects, reducing noise significantly.

* **Microsoft.Maintainability.Analyzers** - Contains rules that contains metrics-based and heuristics-based rules to assess complexity, maintainability, and readability.

* **Microsoft.QualityGuidelines.Analyzers** - Contains miscellaneous rules related to code quality, which do not fall into any of the other packages.

* **Text.Analyzers** - Contains rules that analyze code as text. The existing rules check spelling errors in programming elements such as resource string names and identifiers. Future rules could do things such as flagging comments for inappropriate or deprecated terms.

* **Roslyn.Internal.Analyzers** - Contains rules about some internal types in the Roslyn code base, meant as guidelines for Roslyn contributors as opposed to Roslyn consumers.

## What to port?

In addition to specifying the name of the analyzer package into which each former FxCop rule will be placed, the .csv file also contains some information from telemetry that has been reported through VS about the number of violations and suppressions for many of the rules. We used that as one consideration in deciding whether a rule was high value. Of course some of those numbers might have been reported long ago, so a subjective evaluation of the usefulness of a rule was needed.

We were critical of rules at both ends of the "fire frequency" spectrum, throwing out some checks that never or rarely fire (some of these related to compiler fixes that actually prevent older, bad MSIL patterns from occurring) and deprioritizing some checks that are extremely noisy (which argues for an improved analysis spec, which was out of scope for this exercise).

We tended to favor rules that were not frequently suppressed. We did not automatically throw out rules that fired infrequently; there are several useful checks which don't fire often but always indicate a real issue.

We have populated the "Port?" column of the spreadsheet with our decisions. (NOTE: To see that column, you'll need to scroll to the bottom of the page and scroll horizontally.)

## Feedback

Although we are currently actively executing on this plan, please do provide feedback about the plan, the factoring, individual rules, rules that should be rewritten, rules that should be cut, and/or anything else.
