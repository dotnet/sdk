# Proposed FxCop rule changes in Roslyn

As we reimplement a subset of the existing FxCop rules as Roslyn analyzers, we follow the existing FxCop implementations as closely as possible. This has two benefits:

* It minimizes the friction experienced by developers who are considering changing their process to run the new Roslyn analyzers instead of FxCop. It would hinder their adoption if the Roslyn implementation of a rule they relied on started producing new warnings.

* It facilitates testing the new Roslyn analyzers by making it possible to simply compare the diagnostics produced by the analyzers with the diagnostics produced by FxCop.

Nonetheless, the porting effort raises questions about certain implementation choices in the FxCop rules. We will capture those questions here and review them with the framework API designers to decide whether to allow Roslyn analyzers to behave differently from their FxCop counterparts.

To be clear: In the first release of the FxCop analyzer equivalents, their behavior will be identical to FxCop as far as possible.
We would make changes only in subsequent releases.

## CA1304: Nested types should not be visible

The .NET Framework Design Guidelines for [nested types](https://msdn.microsoft.com/en-us/library/ms229027(v=vs.110).aspx) specifically mentions enumerations:

> For example, an enum passed to a method defined on a class should not be defined as a nested type in the class.

But the [MSDN documentation](https://msdn.microsoft.com/en-us/library/ms182162.aspx) for this rule says:

> Nested enumerations ... are exempt from this rule

... and the FxCop implementation conforms to the documentation by allowing nested enums.

@michaelcfanning explains that this exemption was made for the sake of the `Environment.SpecialFolders` enumeration.

At present, the Roslyn analyzer for CA1034 follows the FxCop implementation. Do we want to change it (and the documentation) to prohibit nested public enums?

### Conclusion

Yes, this is a good change. The .NET team would mark `Environment.SpecialFolders` to suppress this warning.
That has the advantage of making it clear that this wasn't a good design choice,
and will discourage others from emulating it.

## CA1716: Identifiers should not match keywords

@sharwell made the following suggestions:

> 1. The rule is defined according to "reserved identifiers". I believe it makes sense to expand this to include context-sensitive keywords where the identifier is visible in that context. For example, this rule should report a field named value as a violation because fields are visible in property setters, but it should not report a violation for a parameter or local variable named value because they can never be visible in the same scope where value is a keyword.
>
> 2. The set of languages and keywords are not defined. This makes expanding the rule in the future difficult. For example, some users may want new languages (e.g. Boo) for interoperability reasons while other users will not. I encourage this rule to be split into one rule for each programming language.
>
> 3. It makes sense to check publicly-exposed identifiers against multiple programming languages, but identifiers which are only internally visible only need to be checked against the current programming language. This separation should apply whether or not the advice from the second item is taken.

These are good suggestions.

With regard to item #2, the [MSDN documentation](https://msdn.microsoft.com/en-us/library/ms182248.aspx) for the rule actually does define the set of languages to which it applies:

> This rule checks against keywords in the following languages:
>
> * Visual Basic
> * C#
> * C++
> * C++/CLI

... and of course the Roslyn replacements would only apply to the Roslyn languages C# and VB.
