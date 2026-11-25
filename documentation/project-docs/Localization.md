# Localization
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

When diagnosing a missing translation in the Native AOT CLI, compare the same
command with `DOTNET_CLI_ENABLEAOT=true` and `false` in the same complete SDK
installation. Set `DOTNET_CLI_UI_LANGUAGE` to the affected culture, and check a
translated string as well as output parity: both paths can otherwise fall back
to English and appear to match. For example, `dotnet build --help` with `zh-CN`
should show `.NET 生成器` (dotnet/sdk#55820). Also verify a localized first-run
message using a fresh CLI home with developer-certificate generation and global
tool PATH changes disabled.

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
