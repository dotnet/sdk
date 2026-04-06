# fish completions for mycommand

function _mycommand
    set -l tokens (commandline -opc)
    set -l current (commandline -ct)
    
    set -l state 0
    set -l i 2
    while test $i -le (count $tokens)
        set -l word $tokens[$i]
        switch $state
            case 0
                switch $word
                    case --dynamic
                        set i (math $i + 1)
                end
        end
        set i (math $i + 1)
    end
    
    if set -q tokens[2]
        set -l prev $tokens[-1]
        switch $state
            case 0
                switch $prev
                    case --dynamic
                        command $tokens[1] complete --position (commandline -C) (commandline -cp) 2>/dev/null
                        return
                end
        end
    end
    
    switch $state
        case 0
            printf '%s\n' '--dynamic'
            command $tokens[1] complete --position (commandline -C) (commandline -cp) 2>/dev/null
    end
end

complete -c mycommand -f -a '(_mycommand)'
