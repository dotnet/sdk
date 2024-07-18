<!--
########  ########    ###    ########     ######## ##     ## ####  ######
##     ## ##         ## ##   ##     ##       ##    ##     ##  ##  ##    ##
##     ## ##        ##   ##  ##     ##       ##    ##     ##  ##  ##
########  ######   ##     ## ##     ##       ##    #########  ##   ######
##   ##   ##       ######### ##     ##       ##    ##     ##  ##        ##
##    ##  ##       ##     ## ##     ##       ##    ##     ##  ##  ##    ##
##     ## ######## ##     ## ########        ##    ##     ## ####  ######
-->

This Codespace allows you to debug or make changes to the .NET SDK product. The build takes about
45 up to 75 minutes (depending on the machine and OS) and, after completion, produces an archived
.NET SDK located in `/workspaces/dotnet/artifacts/assets/Release`.

In case you selected the prebuilt-sdk Codespace configuration (which is only useful when building
from source), the built-from-source SDK will already be there.

## Build the SDK

To build the repository, run one of the following:
```bash
# Microsoft based build
./build.sh
```
or

```bash
# Building from source only
./prep-source-build.sh && ./build.sh -sb
```

> Please note that, at this time, the build modifies some of the checked-in sources so it might
be preferential to rebuild the Codespace between attempts (or reset the working tree changes).

For more details, see the instructions at https://github.com/dotnet/dotnet.

## Synchronize your changes in locally

When debugging the build, you have two options how to test your changes in this environment.

### Making changes to the VMR directly

You can make the changes directly to the local checkout of the VMR at `/workspaces/dotnet`. You
can then try to build the VMR and see if the change works for you.

### Pull changes into the Codespace from your fork

You can also make a fix in the individual source repository (e.g. `dotnet/runtime`) and push the
fix into a branch; can be in your fork too. Once you have the commit pushed, you can pull this
version of the repository into the Codespace by running:

```
/workspaces/synchronize-vmr.sh                \
  --repository <repo>:<commit, tag or branch> \
  --remote <repo>:<fork URI>
```

You can now proceed building the VMR in the Codespace using instructions above. You can repeat
this process and sync a new commit from your fork. Only note that, at this time, Source-Build
modifies some of the checked-in sources so you'll need to revert the working tree changes
between attempts.
