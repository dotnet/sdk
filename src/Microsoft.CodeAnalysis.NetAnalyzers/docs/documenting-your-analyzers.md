# Documenting your analyzers

We recommend that you provide reference documentation for each of your analyzers, as follows:

1. Create a directory `docs` at the root of your analyzers project.

2. Create a subdirectory `docs\reference`.

    The rationale for this suggestion is that you might have other documents you want to put in your `docs` directory. Keeping the reference pages together in their own subdirectory makes them easier to distinguish from your other documentation. The more analyzer project authors that follow this convention, the easier it will be for analyzer users to find the documentation they need. It will also make it easier for tools that want to search,  aggregate, or otherwise process the documentation pages from multiple analyzer projects.  

3. Make a copy of the [Rule reference page template](https://github.com/Microsoft/sarif-sdk/blob/main/docs/Rule%20reference%20page%20template.md) in your `docs/reference` directory, and name it according to the following convention:

    `<MessageId>_<Name>.md`

    For example, if your analyzer package Great Analyzers prefixes its rule ids with "`GA`", and you have a rule "code should not be evil", then your reference page file would be named

    `GA0001_CodeShouldNotBeEvil.md`

    The template is based on the format of the MSDN reference pages for the Code Analysis rules.

4. Fill in the template with information about your analyzer.

5. **Recommended**: Provide a stable URI for each reference page.
If you use a URI that points directly into your GitHub repo, then the URI will
change whenever you rearrange your source tree, or rename your repo.
Remember that this URI will be baked into your analyzer (see Step 6 below).

    Use the stable URI everywhere, both in your analyzer (Step 6) and in any
cross-references you create in the **Related rules** sections of your reference pages.

    Commercial providers of stable URIs include bit.ly and tinyurl.com,
but you can use any source for the stable URI, including (for example)
any facility your company might provide for registering URIs.

6. In your analyzers, set the value of the `HelpLinkUri` property of
your `DiagnosticDescriptor`  to the (preferably stable) URI you provided.

**Note** Some analyzers produce diagnostics with more than one rule id.
For example, the [`EquatableAnalyzer`](https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.ApiDesignGuidelines.Analyzers/Core/EquatableAnalyzer.cs) in [`Microsoft.ApiDesignGuidelines.Analyzers`](https://github.com/dotnet/roslyn-analyzers/tree/main/src/Microsoft.ApiDesignGuidelines.Analyzers)
produces diagnostics with two rule ids:
`CA1066` ("Implement IEquatable\<T> when overriding Object.Equals")
and `CA1067` ("Override Object.Equals when implementing IEquatable\<T>").
In such a case, create a separate reference page for each rule id.
In this case, we would have `CA1066_ImplementIEquatableOfTWhenOverridingObjectEquals.md`
and `CA1067_OverrideObjectEqualsWhenImplementingIEquatableOfT.md`.
