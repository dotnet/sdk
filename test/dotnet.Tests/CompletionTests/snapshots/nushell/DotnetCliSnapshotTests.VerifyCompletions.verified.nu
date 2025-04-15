# Add the following content to your config.nu file:

let external_completer = { |spans|
    {
        dotnet: { ||
            dotnet complete (
                $spans | skip 1 | str join " "
            ) | lines
        }
    } | get $spans.0 | each { || do $in }
}

# And then in the config record, find the completions section and add the
# external_completer that was defined earlier to external:

let-env config = {
    # your options here
    completions: {
        # your options here
        external: {
            # your options here
            completer: $external_completer # add it here
        }
    }
}
