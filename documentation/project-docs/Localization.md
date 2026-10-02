# Localization

## Satellite resources from NuGet packages

The SDK copies package satellites for the Windows pseudo-locales `qps-ploc`,
`qps-plocm`, and `qps-ploca` into lowercase culture directories during build and
publish. These names are recognized case-insensitively and do not produce the
NETSDK1187 casing diagnostic, because Windows NLS and ICU disagree on their casing.
The original package paths, satellite assembly culture metadata, and embedded resource
names are preserved; the output culture and destination metadata use lowercase.

Other cultures retain their existing behavior: casing corrections such as `ru-ru`
to `ru-RU` produce NETSDK1187 (a low-importance message for targets before .NET 7),
while identifier replacements such as NLS's `ckb` to `ku` are not applied.
See [resource loading](https://learn.microsoft.com/dotnet/core/dependency-loading/loading-resources)
and the [satellite compatibility tests](../../test/Microsoft.NET.Build.Tests/GivenThatWeWantToPreserveSatelliteCultures.cs).

## Summary
The .NET SDK is translated into 14 languages. In our codebase, you can see the primary resx file lists the strings to be translated. 

### Making changes
The local dev build automatically generates updates to the xlf files that contain the translations. You can see the UpdateXlf task in the binlog to see that in action. 

When making string changes, update the resx, build, and check in all xlf file changes. Developers should never need to update the xlf files directly and should always rely on the local build for updates to those files. This will leave the files in english initially and they will get translated eventually.

#### Automated XLF Updates via GitHub Actions

If you've modified `.resx` files in a pull request and need to update the corresponding `.xlf` files but don't want to clone the branch locally, you can use the automated GitHub Action:

**Comment `/updatexlf` on your pull request** and the workflow will:
1. Check out your PR branch
2. Run the UpdateXlf build target
3. Commit any updated `.xlf` files directly to your PR branch

This is particularly useful when CI is failing due to outdated XLF files.

For internal folks, see https://aka.ms/allaboutloc

### Loc issues
Never manually update the xlf file even if a translation is wrong. Report a bug instead.

External -- https://aka.ms/provide-feedback
Internal -- https://aka.ms/icxLocFeedback

### Loc Updates
These are triggered automatically by the loc system as new translations come in. We generally accept these unless we notice it removing translations.
https://github.com/dotnet/sdk/pulls?q=is%3Apr+author%3Adotnet-bot+onelocbuild

### Loc Builds
We typically only localize the primary development branch. We move to vNext once we get to RC1 and only then, localize all new strings introduced in that release. That way we can continue to add messages in the 4xx release of an SDK.

This is controlled by the OneLocBuild stage in [`.vsts-ci.yml`](../../.vsts-ci.yml) and requires a change both there and in the loc system to align branches.

### Translation directives
There are a ton of translations directives our localization system understands. Here are some of the most common in this repo:

#### Locking translations
If a string or partial string should not be translated, add `{Locked=""}` with the details in the appropriate resx files. If `{Locked}` is used the entire string is locked. This can be specified multiple times, each locking different sections.

#### Contextual comments
You can use `<comment>` elements inside the `<data>` tags to provide context to translators.

### String formatting constraints
You can use `{StrBegins=}`, `{StrContains=}` and `{StrEnds=}` to ensure that after translation the string contains the specified values
