#compdef my-app

autoload -U is-at-least

_my-app() {
    typeset -A opt_args
    typeset -a _arguments_options
    local ret=1

    if is-at-least 5.2; then
        _arguments_options=(-s -S -C)
    else
        _arguments_options=(-s -C)
    fi

    local context curcontext="$curcontext" state state_descr line
    _arguments "${_arguments_options[@]}" : \
        '--static=[]: :->dotnet_dynamic_complete' \
        ':--dynamic:->dotnet_dynamic_complete' \
        && ret=0
        case $state in
            (dotnet_dynamic_complete)
                local completions=()
                local result=$(dotnet complete -- "${original_args[@]}")
                for line in ${(f)result}; do
                    completions+=(${(q)line})
                done
                _describe 'completions' $completions && ret=0
            ;;
        esac
    local original_args="my-app ${line[@]}" 
}

(( $+functions[_my-app_commands] )) ||
_my-app_commands() {
    local commands; commands=()
    _describe -t commands 'my-app commands' commands "$@"
}

if [ "$funcstack[1]" = "_my-app" ]; then
    _my-app "$@"
else
    compdef _my-app my-app
fi
