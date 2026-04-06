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
                    case --verbosity
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
                    case --verbosity
                        printf '%s\n' 'detailed'
                        printf '%s\n' 'diagnostic'
                        printf '%s\n' 'minimal'
                        printf '%s\n' 'normal'
                        printf '%s\n' 'quiet'
                        return
                end
        end
    end
    
    switch $state
        case 0
            printf '%s\n' '--verbosity'
    end
end

complete -c mycommand -f -a '(_mycommand)'
