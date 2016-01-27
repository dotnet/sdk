# Proposed FxCop rule changes in Roslyn

As we reimplement a subset of the existing FxCop rules as Roslyn analyzers, we follow the existing FxCop implementations as closely as possible. This has two benefits:

* It minimizes the friction experienced by developers who are considering changing their process to run the new Roslyn analyzers instead of FxCop. It would hinder their adoption if the Roslyn implementation of a rule they relied on started producing new warnings.

* It facilitates testing the new Roslyn analyzers by making it possible to simply compare the diagnostics produced by the analyzers with the diagnostics produced by FxCop.

Nonetheless, the porting effort raises questions about certain implementation choices in the FxCop rules. We will capture those questions here and review them with the framework API designers to decide whether to allow Roslyn analyzers to behave differently from their FxCop counterparts.

## CA1304: Nested types should not be visible

The .NET Framework Design Guidelines for [nested types](https://msdn.microsoft.com/en-us/library/ms229027(v=vs.110).aspx) specifically mentions enumerations:

> For example, an enum passed to a method defined on a class should not be defined as a nested type in the class.

But the [MSDN documentation](https://msdn.microsoft.com/en-us/library/ms182162.aspx) for this rule says:

> Nested enumerations ... are exempt from this rule

... and the FxCop implementation conforms to the documentation by allowing nested enums.

@michaelcfanning explains that this exemption was made for the sake of the `Environment.SpecialFolders` enumeration.

At present, the Roslyn analyzer for CA1034 follows the FxCop implementation. Do we want to change it (and the documentation) to prohibit nested public enums?