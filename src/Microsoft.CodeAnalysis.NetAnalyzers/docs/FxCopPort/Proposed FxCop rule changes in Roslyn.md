# Proposed FxCop rule changes in Roslyn

As we reimplement a subset of the existing FxCop rules as Roslyn analyzers, we follow the existing FxCop implementations as closely as possible. This has two benefits:

* It minimizes the friction experienced by developers who are considering changing their process to run the new Roslyn analyzers instead of FxCop. It would hinder their adoption if the Roslyn implementation of a rule they relied on started producing new warnings.

* It facilitates testing the new Roslyn analyzers by making it possible to simply compare the diagnostics produced by the analyzers with the diagnostics produced by FxCop.

Nonetheless, the porting effort raises questions about certain implementation choices in the FxCop rules. We will capture those questions here and review them with the framework API designers to decide whether to allow Roslyn analyzers to behave differently from their FxCop counterparts.

To be clear: In the first release of the FxCop analyzer equivalents, their behavior will be identical to FxCop as far as possible.
We would make changes only in subsequent releases.

In addition to implementation details of the analyzers we have decided to port, there will be some feedback from the community regarding the rules we have decided _not_ to port. We will track that here as well, and consider this feedback as we revisit our decisions about rules to cut.

## CA1034: Nested types should not be visible

The .NET Framework Design Guidelines for [nested types](https://docs.microsoft.com/dotnet/standard/design-guidelines/nested-types) specifically mentions enumerations:

> For example, an enum passed to a method defined on a class should not be defined as a nested type in the class.

But the [documentation](https://docs.microsoft.com/visualstudio/code-quality/ca1034-nested-types-should-not-be-visible) for this rule says:

> Nested enumerations ... are exempt from this rule

... and the FxCop implementation conforms to the documentation by allowing nested enums.

@michaelcfanning explains that this exemption was made for the sake of the `Environment.SpecialFolders` enumeration.

At present, the Roslyn analyzer for CA1034 follows the FxCop implementation. Do we want to change it (and the documentation) to prohibit nested public enums?

### Conclusion

Yes, this is a good change. The .NET team would mark `Environment.SpecialFolders` to suppress this warning.
That has the advantage of making it clear that this wasn't a good design choice,
and will discourage others from emulating it.

## CA1716: Identifiers should not match keywords

* @sharwell made the following suggestions:

> 1. The rule is defined according to "reserved identifiers". I believe it makes sense to expand this to include context-sensitive keywords where the identifier is visible in that context. For example, this rule should report a field named value as a violation because fields are visible in property setters, but it should not report a violation for a parameter or local variable named value because they can never be visible in the same scope where value is a keyword.
>
> 2. The set of languages and keywords are not defined. This makes expanding the rule in the future difficult. For example, some users may want new languages (e.g. Boo) for interoperability reasons while other users will not. I encourage this rule to be split into one rule for each programming language.
>
> 3. It makes sense to check publicly-exposed identifiers against multiple programming languages, but identifiers which are only internally visible only need to be checked against the current programming language. This separation should apply whether or not the advice from the second item is taken.

These are good suggestions.

With regard to item #2, the [documentation](https://docs.microsoft.com/visualstudio/code-quality/ca1716-identifiers-should-not-match-keywords) for the rule actually does define the set of languages to which it applies:

> This rule checks against keywords in the following languages:
>
> * Visual Basic
> * C#
> * C++/CLI

... and of course the Roslyn replacements would only apply to the Roslyn languages C# and VB.

* @nguerrera: Consider adding `stackalloc` to the list of C# keywords we check.

* @nguerrera, @lgolding, @srivatsn: Why did FxCop CA1716 limit itself to virtual/interface members? The error message says
that it will be hard to implement a virtual method if you name it with a keyword. But it's just as hard to _invoke_ it.
Why shouldn't all publicly visible methods follow this rule?

## CA1812: Avoid uninstantiated internal classes

* @mavasani suggests:

> ... you probably want to ignore types with the MEF export attributes - they wouldn't have an explicit instantiation. And Roslyn code is full of such types.

## CA2213: Disposable fields should be disposed

We decided not to port this because of a high false positive rate, and our opinion that it was not of high value. We have had the following pushback on this decision:

> @stilgarSCA: :-1: on this decision. Despite the fact that this causes a lot of false positives, I think it's worth keeping the rule for the correctly identified issues. End users always have the option of disabling rules for which they find no value.
>
> Several others have also argued for reversing this decision, as can be seen in the comments of [issue #695](https://github.com/dotnet/roslyn-analyzers/issues/695) and [issue #291](https://github.com/dotnet/roslyn-analyzers/issues/291).
