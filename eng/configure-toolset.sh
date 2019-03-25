# We can't use already installed dotnet cli since we need to install additional shared runtimes.
# We could potentially try to find an existing installation that has all the required runtimes,
# but it's unlikely one will be available.

useInstalledDotNetCli="false"

# To further isolate between each build. It has sential files and local tools resolver cache
export DOTNET_CLI_HOME=$repo_root/artifacts/DotnetCliHome