# Localization
## Summary
The .NET SDK is translated into 14 languages. In our codebase, you can see the primary resx file lists the strings to be translated. 

### Making changes
The local dev build automatically generates updates to the xlf files that contain the translations. You can see the UpdateXlf task in the binlog to see that in action. 

When making string changes, update the resx, build, and check in all xlf file changes. Developers should never need to update the xlf files directly and should always rely on the local build for updates to those files. This will leave the files in english initially and they will get translated eventually.

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

This is controlled https://github.com/dotnet/sdk/blob/main/eng/pipelines/templates/jobs/sdk-job-matrix.yml#L86 and requires a change both here and in the loc system to align branches.

### Translation directives
There are a ton of translations directives our localization system understands. Here are some of the most common in this repo:

#### Locking translations
If a string or partial string should not be translated, add `{Locked=""}` with the details in the appropriate resx files. If `{Locked}` is used the entire string is locked. This can be specified multiple times, each locking different sections.

#### Contextual comments
You can use `<comment>` elements inside the `<data>` tags to provide context to translators.

### String formatting constraints
You can use `{StrBegins=}`, `{StrContains=}` and `{StrEnds=}` to ensure that after translation the string contains the specified values
