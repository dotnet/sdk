# Documenting your analyzers

We recommend that you provide reference documentation for each of your analyzers, as follows:

1. Create a directory `docs` at the root of your analyzers project.

2. Create a subdirectory `docs\reference`.

    The rationale for this suggestion is that you might have other documents you want to put in your `docs` directory. Keeping the reference pages together in their own subdirectory makes them easier to distinguish from your other documentation. The more analyzer project authors that follow this convention, the easier it will be for analyzer users to find the documentation they need. It will also make it easier for tools that want to search,  aggregate, or otherwise process the documentation pages from multiple analyzer projects.  

3. Make a copy of the [Analyzer reference page template](https://github.com/dotnet/roslyn-analyzers/blob/master/docs/Analyzer%20Reference%20Page%20Template.md) in your _docs/reference_ directory, and name it according to the following convention:

    `<MessageId>_<Name>.md`

    For example, if your analyzer package Great Analyzers prefixes its rule ids with "`GA`", and you have a rule "code should not be evil", then your reference page file would be named

    `GA0001_CodeShouldNotBeEvil.md`

    The template is based on the format of the MSDN reference pages for the Code Analysis rules.

4. Fill in the template with information about your analyzer.

