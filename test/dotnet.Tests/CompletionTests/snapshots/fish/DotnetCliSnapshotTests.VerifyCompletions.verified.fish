# fish parameter completion for the dotnet CLI
# add the following to your config.fish to enable completions

complete -f -c dotnet -a "(dotnet complete (commandline -cp))"