# zsh parameter completion for the dotnet CLI

_dotnet_zsh_complete() 
{
  # If command is dotnet new, display the list of installed templates
  if [ $CURRENT -eq 3 ]; then
    case ${words[2]} in
      "new")
      compadd -X ".NET Installed Templates" $(dotnet new -l | tail -n +21 | sed 's/  \+/:/g' | cut -d : -f 2)
      return
      ;;
    esac
  fi

  local completions=("$(dotnet complete "$words")")

  # If the completion list is empty, just continue with filename selection
  if [ -z "$completions" ]
  then
    _arguments '*::arguments: _normal'
    return
  fi

  # This is not a variable assigment, don't remove spaces!
  _values = "${(ps:\n:)completions}"
}

compdef _dotnet_zsh_complete dotnet