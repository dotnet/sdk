#compdef testhost

autoload -U is-at-least

_testhost() {
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
        '--help[Show command line help.]' \
        '-h[Show command line help.]' \
        '--diagnostics[Enable diagnostic output.]' \
        '-d[Enable diagnostic output.]' \
        '--version[]' \
        '--info[]' \
        '--list-sdks[]' \
        '--list-runtimes[]' \
        ":: :_testhost_commands" \
        "*::: :->testhost" \
        && ret=0
    local original_args="testhost ${line[@]}" 
    case $state in
        (testhost)
            words=($line[1] "${words[@]}")
            (( CURRENT += 1 ))
            curcontext="${curcontext%:*:*}:testhost-command-$line[1]:"
            case $line[1] in
                (build)
                    _arguments "${_arguments_options[@]}" : \
                        '--use-current-runtime[Use current runtime as the target runtime.]' \
                        '--ucr[Use current runtime as the target runtime.]' \
                        '--framework=[The target framework to build for. The target framework must also be specified in the project file.]:FRAMEWORK:->dotnet_dynamic_complete' \
                        '-f=[The target framework to build for. The target framework must also be specified in the project file.]:FRAMEWORK:->dotnet_dynamic_complete' \
                        '--configuration=[The configuration to use for building the project. The default for most projects is '\''Debug'\''.]:CONFIGURATION:->dotnet_dynamic_complete' \
                        '-c=[The configuration to use for building the project. The default for most projects is '\''Debug'\''.]:CONFIGURATION:->dotnet_dynamic_complete' \
                        '--runtime=[The target runtime to build for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '-r=[The target runtime to build for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '--version-suffix=[Set the value of the \$(VersionSuffix) property to use when building the project.]:VERSION_SUFFIX: ' \
                        '--no-restore[Do not restore the project before building.]' \
                        '--interactive=[Allows the command to stop and wait for user input or action (for example to complete authentication).]: :((False\:"False" True\:"True" ))' \
                        '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '/v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '/verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--debug[]' \
                        '--output=[The output directory to place built artifacts in.]:OUTPUT_DIR: ' \
                        '-o=[The output directory to place built artifacts in.]:OUTPUT_DIR: ' \
                        '--artifacts-path=[The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.]:ARTIFACTS_DIR: ' \
                        '--no-incremental[Do not use incremental building.]' \
                        '--no-dependencies[Do not build project-to-project references and only build the specified project.]' \
                        '--nologo[Do not display the startup banner or the copyright message.]' \
                        '--self-contained=[Publish the .NET runtime with your application so the runtime doesn'\''t need to be installed on the target machine. The default is '\''false.'\'' However, when targeting .NET 7 or lower, the default is '\''true'\'' if a runtime identifier is specified.]: :((False\:"False" True\:"True" ))' \
                        '--sc=[Publish the .NET runtime with your application so the runtime doesn'\''t need to be installed on the target machine. The default is '\''false.'\'' However, when targeting .NET 7 or lower, the default is '\''true'\'' if a runtime identifier is specified.]: :((False\:"False" True\:"True" ))' \
                        '--no-self-contained[Publish your application as a framework dependent application. A compatible .NET runtime must be installed on the target machine to run your application.]' \
                        '--arch=[The target architecture.]:ARCH: ' \
                        '-a=[The target architecture.]:ARCH: ' \
                        '--os=[The target operating system.]:OS: ' \
                        '--disable-build-servers[Force the command to ignore any persistent build servers.]' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        '*::PROJECT | SOLUTION | FILE -- The project or solution or C# (file-based program) file to operate on. If a file is not specified, the command will search the current directory for a project or solution.: ' \
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
                    ;;
                (build-server)
                    _arguments "${_arguments_options[@]}" : \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        ":: :_testhost__build-server_commands" \
                        "*::: :->build-server" \
                        && ret=0
                        case $state in
                            (build-server)
                                words=($line[1] "${words[@]}")
                                (( CURRENT += 1 ))
                                curcontext="${curcontext%:*:*}:testhost-build-server-command-$line[1]:"
                                case $line[1] in
                                    (shutdown)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--msbuild[Shut down the MSBuild build server.]' \
                                            '--vbcscompiler[Shut down the VB/C# compiler build server.]' \
                                            '--razor[Shut down the Razor build server.]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            && ret=0
                                        ;;
                                esac
                            ;;
                        esac
                    ;;
                (clean)
                    _arguments "${_arguments_options[@]}" : \
                        '--framework=[The target framework to clean for. The target framework must also be specified in the project file.]:FRAMEWORK:->dotnet_dynamic_complete' \
                        '-f=[The target framework to clean for. The target framework must also be specified in the project file.]:FRAMEWORK:->dotnet_dynamic_complete' \
                        '--runtime=[The target runtime to clean for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '-r=[The target runtime to clean for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '--configuration=[The configuration to clean for. The default for most projects is '\''Debug'\''.]:CONFIGURATION:->dotnet_dynamic_complete' \
                        '-c=[The configuration to clean for. The default for most projects is '\''Debug'\''.]:CONFIGURATION:->dotnet_dynamic_complete' \
                        '--interactive=[Allows the command to stop and wait for user input or action (for example to complete authentication).]: :((False\:"False" True\:"True" ))' \
                        '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--output=[The directory containing the build artifacts to clean.]:OUTPUT_DIR: ' \
                        '-o=[The directory containing the build artifacts to clean.]:OUTPUT_DIR: ' \
                        '--artifacts-path=[The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.]:ARTIFACTS_DIR: ' \
                        '--nologo[Do not display the startup banner or the copyright message.]' \
                        '--disable-build-servers[Force the command to ignore any persistent build servers.]' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        '::PROJECT | SOLUTION | FILE -- The project or solution or C# (file-based program) file to operate on. If a file is not specified, the command will search the current directory for a project or solution.: ' \
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
                        case $state in
                            (clean)
                                words=($line[2] "${words[@]}")
                                (( CURRENT += 1 ))
                                curcontext="${curcontext%:*:*}:testhost-clean-command-$line[2]:"
                                case $line[2] in
                                esac
                            ;;
                        esac
                    ;;
                (format)
                    _arguments "${_arguments_options[@]}" : \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        '*::arguments: ' \
                        && ret=0
                    ;;
                (fsi)
                    _arguments "${_arguments_options[@]}" : \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        '*::arguments: ' \
                        && ret=0
                    ;;
                (msbuild)
                    _arguments "${_arguments_options[@]}" : \
                        '--disable-build-servers[Force the command to ignore any persistent build servers.]' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        '*::arguments: ' \
                        && ret=0
                    ;;
                (new)
                    _arguments "${_arguments_options[@]}" : \
                        '--output=[Location to place the generated output.]: :_files' \
                        '-o=[Location to place the generated output.]: :_files' \
                        '--name=[The name for the output being created. If no name is specified, the name of the output directory is used.]: : ' \
                        '-n=[The name for the output being created. If no name is specified, the name of the output directory is used.]: : ' \
                        '--dry-run=[Displays a summary of what would happen if the given command line were run if it would result in a template creation.]: :((False\:"False" True\:"True" ))' \
                        '--force=[Forces content to be generated even if it would change existing files.]: :((False\:"False" True\:"True" ))' \
                        '--no-update-check=[Disables checking for the template package updates when instantiating a template.]: :((False\:"False" True\:"True" ))' \
                        '--project=[The project that should be used for context evaluation.]: :_files' \
                        '--verbosity=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-v=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--diagnostics[Enables diagnostic output.]' \
                        '-d[Enables diagnostic output.]' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        ":: :_testhost__new_commands" \
                        "*::: :->new" \
                        && ret=0
                        case $state in
                            (new)
                                words=($line[1] "${words[@]}")
                                (( CURRENT += 1 ))
                                curcontext="${curcontext%:*:*}:testhost-new-command-$line[1]:"
                                case $line[1] in
                                    (create)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--output=[Location to place the generated output.]: :_files' \
                                            '-o=[Location to place the generated output.]: :_files' \
                                            '--name=[The name for the output being created. If no name is specified, the name of the output directory is used.]: : ' \
                                            '-n=[The name for the output being created. If no name is specified, the name of the output directory is used.]: : ' \
                                            '--dry-run=[Displays a summary of what would happen if the given command line were run if it would result in a template creation.]: :((False\:"False" True\:"True" ))' \
                                            '--force=[Forces content to be generated even if it would change existing files.]: :((False\:"False" True\:"True" ))' \
                                            '--no-update-check=[Disables checking for the template package updates when instantiating a template.]: :((False\:"False" True\:"True" ))' \
                                            '--project=[The project that should be used for context evaluation.]: :_files' \
                                            '--verbosity=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--diagnostics[Enables diagnostic output.]' \
                                            '-d[Enables diagnostic output.]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '::template-short-name -- A short name of the template to create.: ' \
                                            '*::template-args -- Template specific options to use.: ' \
                                            && ret=0
                                        ;;
                                    (install)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--interactive=[Allows the command to stop and wait for user input or action (for example to complete authentication).]: :((False\:"False" True\:"True" ))' \
                                            '*--add-source=[Specifies a NuGet source to use.]:nuget-source: ' \
                                            '*--nuget-source=[Specifies a NuGet source to use.]:nuget-source: ' \
                                            '--force=[Allows installing template packages from the specified sources even if they would override a template package from another source.]: :((False\:"False" True\:"True" ))' \
                                            '--verbosity=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--diagnostics[Enables diagnostic output.]' \
                                            '-d[Enables diagnostic output.]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::package -- NuGet package ID or path to folder or NuGet package to install.  To install the NuGet package of certain version, use <package ID>\:\:<version>. : ' \
                                            && ret=0
                                        ;;
                                    (uninstall)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--verbosity=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--diagnostics[Enables diagnostic output.]' \
                                            '-d[Enables diagnostic output.]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::package -- NuGet package ID (without version) or path to folder to uninstall.  If command is specified without the argument, it lists all the template packages installed.: ' \
                                            && ret=0
                                        ;;
                                    (update)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--interactive=[Allows the command to stop and wait for user input or action (for example to complete authentication).]: :((False\:"False" True\:"True" ))' \
                                            '*--add-source=[Specifies a NuGet source to use.]:nuget-source: ' \
                                            '*--nuget-source=[Specifies a NuGet source to use.]:nuget-source: ' \
                                            '--check-only[Only checks for updates and display the template packages to be updated without applying update.]' \
                                            '--dry-run[Only checks for updates and display the template packages to be updated without applying update.]' \
                                            '--verbosity=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--diagnostics[Enables diagnostic output.]' \
                                            '-d[Enables diagnostic output.]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            && ret=0
                                        ;;
                                    (search)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--author=[Filters the templates based on the template author.]: : ' \
                                            '--language=[Filters templates based on language.]: : ' \
                                            '-lang=[Filters templates based on language.]: : ' \
                                            '--type=[Filters templates based on available types. Predefined values are \"project\" and \"item\".]: : ' \
                                            '--tag=[Filters the templates based on the tag.]: : ' \
                                            '--package=[Filters the templates based on NuGet package ID.]: : ' \
                                            '--columns-all[Displays all columns in the output.]' \
                                            '*--columns=[Specifies the columns to display in the output. ]: :((author\:"author" language\:"language" tags\:"tags" type\:"type" ))' \
                                            '--verbosity=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--diagnostics[Enables diagnostic output.]' \
                                            '-d[Enables diagnostic output.]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '::template-name -- If specified, only the templates matching the name will be shown.: ' \
                                            && ret=0
                                        ;;
                                    (list)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--author=[Filters the templates based on the template author.]: : ' \
                                            '--language=[Filters templates based on language.]: : ' \
                                            '-lang=[Filters templates based on language.]: : ' \
                                            '--type=[Filters templates based on available types. Predefined values are \"project\" and \"item\".]: : ' \
                                            '--tag=[Filters the templates based on the tag.]: : ' \
                                            '--ignore-constraints[Disables checking if the template meets the constraints to be run.]' \
                                            '--output=[Location to place the generated output.]: :_files' \
                                            '-o=[Location to place the generated output.]: :_files' \
                                            '--project=[The project that should be used for context evaluation.]: :_files' \
                                            '--columns-all[Displays all columns in the output.]' \
                                            '*--columns=[Specifies the columns to display in the output. ]: :((author\:"author" language\:"language" tags\:"tags" type\:"type" ))' \
                                            '--verbosity=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--diagnostics[Enables diagnostic output.]' \
                                            '-d[Enables diagnostic output.]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '::template-name -- If specified, only the templates matching the name will be shown.: ' \
                                            && ret=0
                                        ;;
                                    (details)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--interactive=[Allows the command to stop and wait for user input or action (for example to complete authentication).]: :((False\:"False" True\:"True" ))' \
                                            '*--add-source=[Specifies a NuGet source to use.]:nuget-source: ' \
                                            '*--nuget-source=[Specifies a NuGet source to use.]:nuget-source: ' \
                                            '--verbosity=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Sets the verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--diagnostics[Enables diagnostic output.]' \
                                            '-d[Enables diagnostic output.]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            ':package-identifier -- Package identifier: ' \
                                            && ret=0
                                        ;;
                                esac
                            ;;
                        esac
                    ;;
                (nuget)
                    _arguments "${_arguments_options[@]}" : \
                        '--version[]' \
                        '--verbosity=[]: : ' \
                        '-v=[]: : ' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        ":: :_testhost__nuget_commands" \
                        "*::: :->nuget" \
                        && ret=0
                        case $state in
                            (nuget)
                                words=($line[1] "${words[@]}")
                                (( CURRENT += 1 ))
                                curcontext="${curcontext%:*:*}:testhost-nuget-command-$line[1]:"
                                case $line[1] in
                                    (delete)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--force-english-output[]' \
                                            '--source=[]: : ' \
                                            '-s=[]: : ' \
                                            '--non-interactive[]' \
                                            '--api-key=[]: : ' \
                                            '-k=[]: : ' \
                                            '--no-service-endpoint[]' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::package-paths: ' \
                                            && ret=0
                                        ;;
                                    (locals)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--force-english-output[]' \
                                            '--clear[]' \
                                            '-c[]' \
                                            '--list[]' \
                                            '-l[]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            ':folders:((all\:"all" global-packages\:"global-packages" http-cache\:"http-cache" plugins-cache\:"plugins-cache" temp\:"temp" ))' \
                                            && ret=0
                                        ;;
                                    (push)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--force-english-output[]' \
                                            '--source=[]: : ' \
                                            '-s=[]: : ' \
                                            '--symbol-source=[]: : ' \
                                            '-ss=[]: : ' \
                                            '--timeout=[]: : ' \
                                            '-t=[]: : ' \
                                            '--api-key=[]: : ' \
                                            '-k=[]: : ' \
                                            '--symbol-api-key=[]: : ' \
                                            '-sk=[]: : ' \
                                            '--disable-buffering[]' \
                                            '-d[]' \
                                            '--no-symbols[]' \
                                            '-n[]' \
                                            '--no-service-endpoint[]' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--skip-duplicate[]' \
                                            '--configfile=[]: : ' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::package-paths: ' \
                                            && ret=0
                                        ;;
                                    (verify)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--all[]' \
                                            '*--certificate-fingerprint=[]: : ' \
                                            '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::package-paths: ' \
                                            && ret=0
                                        ;;
                                    (trust)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--configfile=[]: : ' \
                                            '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            ":: :_testhost__nuget__trust_commands" \
                                            "*::: :->trust" \
                                            && ret=0
                                            case $state in
                                                (trust)
                                                    words=($line[1] "${words[@]}")
                                                    (( CURRENT += 1 ))
                                                    curcontext="${curcontext%:*:*}:testhost-nuget-trust-command-$line[1]:"
                                                    case $line[1] in
                                                        (list)
                                                            _arguments "${_arguments_options[@]}" : \
                                                                '--configfile=[]: : ' \
                                                                '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '--help[Show command line help.]' \
                                                                '-h[Show command line help.]' \
                                                                && ret=0
                                                            ;;
                                                        (author)
                                                            _arguments "${_arguments_options[@]}" : \
                                                                '--allow-untrusted-root[]' \
                                                                '--configfile=[]: : ' \
                                                                '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '--help[Show command line help.]' \
                                                                '-h[Show command line help.]' \
                                                                ':NAME: ' \
                                                                ':PACKAGE: ' \
                                                                && ret=0
                                                            ;;
                                                        (repository)
                                                            _arguments "${_arguments_options[@]}" : \
                                                                '--allow-untrusted-root[]' \
                                                                '--owners=[]: : ' \
                                                                '--configfile=[]: : ' \
                                                                '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '--help[Show command line help.]' \
                                                                '-h[Show command line help.]' \
                                                                ':NAME: ' \
                                                                ':PACKAGE: ' \
                                                                && ret=0
                                                            ;;
                                                        (source)
                                                            _arguments "${_arguments_options[@]}" : \
                                                                '--owners=[]: : ' \
                                                                '--source-url=[]: : ' \
                                                                '--configfile=[]: : ' \
                                                                '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '--help[Show command line help.]' \
                                                                '-h[Show command line help.]' \
                                                                ':NAME: ' \
                                                                && ret=0
                                                            ;;
                                                        (certificate)
                                                            _arguments "${_arguments_options[@]}" : \
                                                                '--allow-untrusted-root[]' \
                                                                '--algorithm=[]: :((SHA256\:"SHA256" SHA384\:"SHA384" SHA512\:"SHA512" ))' \
                                                                '--configfile=[]: : ' \
                                                                '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '--help[Show command line help.]' \
                                                                '-h[Show command line help.]' \
                                                                ':NAME: ' \
                                                                ':FINGERPRINT: ' \
                                                                && ret=0
                                                            ;;
                                                        (remove)
                                                            _arguments "${_arguments_options[@]}" : \
                                                                '--configfile=[]: : ' \
                                                                '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '--help[Show command line help.]' \
                                                                '-h[Show command line help.]' \
                                                                ':NAME: ' \
                                                                && ret=0
                                                            ;;
                                                        (sync)
                                                            _arguments "${_arguments_options[@]}" : \
                                                                '--configfile=[]: : ' \
                                                                '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                                                '--help[Show command line help.]' \
                                                                '-h[Show command line help.]' \
                                                                ':NAME: ' \
                                                                && ret=0
                                                            ;;
                                                    esac
                                                ;;
                                            esac
                                        ;;
                                    (sign)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--output=[]: : ' \
                                            '-o=[]: : ' \
                                            '--certificate-path=[]: : ' \
                                            '--certificate-store-name=[]: : ' \
                                            '--certificate-store-location=[]: : ' \
                                            '--certificate-subject-name=[]: : ' \
                                            '--certificate-fingerprint=[]: : ' \
                                            '--certificate-password=[]: : ' \
                                            '--hash-algorithm=[]: : ' \
                                            '--timestamper=[]: : ' \
                                            '--timestamp-hash-algorithm=[]: : ' \
                                            '--overwrite[]' \
                                            '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::package-paths: ' \
                                            && ret=0
                                        ;;
                                    (why)
                                        _arguments "${_arguments_options[@]}" : \
                                            '*--framework=[The target framework(s) for which dependency graphs are shown.]: : ' \
                                            '*-f=[The target framework(s) for which dependency graphs are shown.]: : ' \
                                            '--help[Show help and usage information]' \
                                            '-h[Show help and usage information]' \
                                            '*::PROJECT|SOLUTION -- A path to a project, solution file, or directory.: ' \
                                            ':PACKAGE -- The package name to lookup in the dependency graph.: ' \
                                            && ret=0
                                        ;;
                                esac
                            ;;
                        esac
                    ;;
                (pack)
                    _arguments "${_arguments_options[@]}" : \
                        '--output=[The output directory to place built packages in.]:OUTPUT_DIR: ' \
                        '-o=[The output directory to place built packages in.]:OUTPUT_DIR: ' \
                        '--artifacts-path=[The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.]:ARTIFACTS_DIR: ' \
                        '--no-build[Do not build the project before packing. Implies --no-restore.]' \
                        '--include-symbols[Include packages with symbols in addition to regular packages in output directory.]' \
                        '--include-source[Include PDBs and source files. Source files go into the '\''src'\'' folder in the resulting nuget package.]' \
                        '--serviceable[Set the serviceable flag in the package. See https\://aka.ms/nupkgservicing for more information.]' \
                        '-s[Set the serviceable flag in the package. See https\://aka.ms/nupkgservicing for more information.]' \
                        '--nologo[Do not display the startup banner or the copyright message.]' \
                        '--interactive=[Allows the command to stop and wait for user input or action (for example to complete authentication).]: :((False\:"False" True\:"True" ))' \
                        '--no-restore[Do not restore the project before building.]' \
                        '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '/v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '/verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--version-suffix=[Set the value of the \$(VersionSuffix) property to use when building the project.]:VERSION_SUFFIX: ' \
                        '--version=[The version of the package to create]:VERSION: ' \
                        '--configuration=[The configuration to use for building the package. The default is '\''Release'\''.]:CONFIGURATION:->dotnet_dynamic_complete' \
                        '-c=[The configuration to use for building the package. The default is '\''Release'\''.]:CONFIGURATION:->dotnet_dynamic_complete' \
                        '--disable-build-servers[Force the command to ignore any persistent build servers.]' \
                        '--use-current-runtime[Use current runtime as the target runtime.]' \
                        '--ucr[Use current runtime as the target runtime.]' \
                        '--runtime=[The target runtime to build for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '-r=[The target runtime to build for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        '*::PROJECT | SOLUTION | FILE -- The project or solution or C# (file-based program) file to operate on. If a file is not specified, the command will search the current directory for a project or solution.: ' \
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
                    ;;
                (package)
                    _arguments "${_arguments_options[@]}" : \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        ":: :_testhost__package_commands" \
                        "*::: :->package" \
                        && ret=0
                        case $state in
                            (package)
                                words=($line[1] "${words[@]}")
                                (( CURRENT += 1 ))
                                curcontext="${curcontext%:*:*}:testhost-package-command-$line[1]:"
                                case $line[1] in
                                    (search)
                                        _arguments "${_arguments_options[@]}" : \
                                            '*--source=[The package source to search. You can pass multiple \`--source\` options to search multiple package sources. Example\: \`--source https\://api.nuget.org/v3/index.json\`.]:Source: ' \
                                            '--take=[Number of results to return. Default 20.]:Take: ' \
                                            '--skip=[Number of results to skip, to allow pagination. Default 0.]:Skip: ' \
                                            '--exact-match[Require that the search term exactly match the name of the package. Causes \`--take\` and \`--skip\` options to be ignored.]' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--prerelease[Include prerelease packages.]' \
                                            '--configfile=[The NuGet configuration file. If specified, only the settings from this file will be used. If not specified, the hierarchy of configuration files from the current directory will be used. For more information, see https\://docs.microsoft.com/nuget/consume-packages/configuring-nuget-behavior]:ConfigFile: ' \
                                            '--format=[Format the output accordingly. Either \`table\`, or \`json\`. The default value is \`table\`.]:Format: ' \
                                            '--verbosity=[Display this amount of details in the output\: \`normal\`, \`minimal\`, \`detailed\`. The default is \`normal\`]:Verbosity: ' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '::SearchTerm -- Search term to filter package names, descriptions, and tags. Used as a literal value. Example\: \`dotnet package search some.package\`. See also \`--exact-match\`.: ' \
                                            && ret=0
                                        ;;
                                    (add)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--version=[The version of the package to add.]:VERSION:->dotnet_dynamic_complete' \
                                            '-v=[The version of the package to add.]:VERSION:->dotnet_dynamic_complete' \
                                            '--framework=[Add the reference only when targeting a specific framework.]:FRAMEWORK: ' \
                                            '-f=[Add the reference only when targeting a specific framework.]:FRAMEWORK: ' \
                                            '--no-restore[Add the reference without performing restore preview and compatibility check.]' \
                                            '-n[Add the reference without performing restore preview and compatibility check.]' \
                                            '--source=[The NuGet package source to use during the restore.]:SOURCE: ' \
                                            '-s=[The NuGet package source to use during the restore.]:SOURCE: ' \
                                            '--package-directory=[The directory to restore packages to.]:PACKAGE_DIR: ' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--prerelease[Allows prerelease packages to be installed.]' \
                                            '--project=[The project file to operate on. If a file is not specified, the command will search the current directory for one.]: : ' \
                                            '--file=[The file-based app to operate on.]: : ' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            ':packageId -- Package reference in the form of a package identifier like '\''Newtonsoft.Json'\'' or package identifier and version separated by '\''@'\'' like '\''Newtonsoft.Json@13.0.3'\''.:->dotnet_dynamic_complete' \
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
                                        ;;
                                    (list)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--outdated[Lists packages that have newer versions. Cannot be combined with '\''--deprecated'\'' or '\''--vulnerable'\'' options.]' \
                                            '--deprecated[Lists packages that have been deprecated. Cannot be combined with '\''--vulnerable'\'' or '\''--outdated'\'' options.]' \
                                            '--vulnerable[Lists packages that have known vulnerabilities. Cannot be combined with '\''--deprecated'\'' or '\''--outdated'\'' options.]' \
                                            '*--framework=[Chooses a framework to show its packages. Use the option multiple times for multiple frameworks.]:FRAMEWORK | FRAMEWORK\RID: ' \
                                            '*-f=[Chooses a framework to show its packages. Use the option multiple times for multiple frameworks.]:FRAMEWORK | FRAMEWORK\RID: ' \
                                            '--include-transitive[Lists transitive and top-level packages.]' \
                                            '--include-prerelease[Consider packages with prerelease versions when searching for newer packages. Requires the '\''--outdated'\'' option.]' \
                                            '--highest-patch[Consider only the packages with a matching major and minor version numbers when searching for newer packages. Requires the '\''--outdated'\'' option.]' \
                                            '--highest-minor[Consider only the packages with a matching major version number when searching for newer packages. Requires the '\''--outdated'\'' option.]' \
                                            '--config=[The path to the NuGet config file to use. Requires the '\''--outdated'\'', '\''--deprecated'\'' or '\''--vulnerable'\'' option.]:CONFIG_FILE: ' \
                                            '--configfile=[The path to the NuGet config file to use. Requires the '\''--outdated'\'', '\''--deprecated'\'' or '\''--vulnerable'\'' option.]:CONFIG_FILE: ' \
                                            '*--source=[The NuGet sources to use when searching for newer packages. Requires the '\''--outdated'\'', '\''--deprecated'\'' or '\''--vulnerable'\'' option.]:SOURCE: ' \
                                            '*-s=[The NuGet sources to use when searching for newer packages. Requires the '\''--outdated'\'', '\''--deprecated'\'' or '\''--vulnerable'\'' option.]:SOURCE: ' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--format=[Specifies the output format type for the list packages command.]: :((console\:"console" json\:"json" ))' \
                                            '--output-version=[Specifies the version of machine-readable output. Requires the '\''--format json'\'' option.]: : ' \
                                            '--no-restore[Do not restore before running the command.]' \
                                            '--project=[The project file to operate on. If a file is not specified, the command will search the current directory for one.]: : ' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            && ret=0
                                        ;;
                                    (remove)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--project=[The project file to operate on. If a file is not specified, the command will search the current directory for one.]: : ' \
                                            '--file=[The file-based app to operate on.]: : ' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::PACKAGE_NAME -- The package reference to remove.: ' \
                                            && ret=0
                                        ;;
                                    (update)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--project=[Path to a project or solution file, or a directory.]: : ' \
                                            '--interactive=[Allows the command to stop and wait for user input or action (for example to complete authentication).]: :((False\:"False" True\:"True" ))' \
                                            '--verbosity=[Set the verbosity level of the command. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]: :((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the verbosity level of the command. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]: :((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::packages -- Package reference in the form of a package identifier like '\''Newtonsoft.Json'\'' or package identifier and version separated by '\''@'\'' like '\''Newtonsoft.Json@13.0.3'\''.: ' \
                                            && ret=0
                                        ;;
                                esac
                            ;;
                        esac
                    ;;
                (project)
                    _arguments "${_arguments_options[@]}" : \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        ":: :_testhost__project_commands" \
                        "*::: :->project" \
                        && ret=0
                        case $state in
                            (project)
                                words=($line[1] "${words[@]}")
                                (( CURRENT += 1 ))
                                curcontext="${curcontext%:*:*}:testhost-project-command-$line[1]:"
                                case $line[1] in
                                    (convert)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--output=[Location to place the generated output.]: :_files' \
                                            '-o=[Location to place the generated output.]: :_files' \
                                            '--force[Force conversion even if there are malformed directives.]' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--dry-run[Determines changes without actually modifying the file system]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            ':file -- Path to the file-based program.: ' \
                                            && ret=0
                                        ;;
                                esac
                            ;;
                        esac
                    ;;
                (publish)
                    _arguments "${_arguments_options[@]}" : \
                        '--use-current-runtime[Use current runtime as the target runtime.]' \
                        '--ucr[Use current runtime as the target runtime.]' \
                        '--output=[The output directory to place the published artifacts in.]:OUTPUT_DIR: ' \
                        '-o=[The output directory to place the published artifacts in.]:OUTPUT_DIR: ' \
                        '--artifacts-path=[The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.]:ARTIFACTS_DIR: ' \
                        '*--manifest=[The path to a target manifest file that contains the list of packages to be excluded from the publish step.]:MANIFEST: ' \
                        '--no-build[Do not build the project before publishing. Implies --no-restore.]' \
                        '--self-contained=[Publish the .NET runtime with your application so the runtime doesn'\''t need to be installed on the target machine. The default is '\''false.'\'' However, when targeting .NET 7 or lower, the default is '\''true'\'' if a runtime identifier is specified.]: :((False\:"False" True\:"True" ))' \
                        '--sc=[Publish the .NET runtime with your application so the runtime doesn'\''t need to be installed on the target machine. The default is '\''false.'\'' However, when targeting .NET 7 or lower, the default is '\''true'\'' if a runtime identifier is specified.]: :((False\:"False" True\:"True" ))' \
                        '--no-self-contained[Publish your application as a framework dependent application. A compatible .NET runtime must be installed on the target machine to run your application.]' \
                        '--nologo[Do not display the startup banner or the copyright message.]' \
                        '--framework=[The target framework to publish for. The target framework has to be specified in the project file.]:FRAMEWORK:->dotnet_dynamic_complete' \
                        '-f=[The target framework to publish for. The target framework has to be specified in the project file.]:FRAMEWORK:->dotnet_dynamic_complete' \
                        '--runtime=[The target runtime to publish for. This is used when creating a self-contained deployment. The default is to publish a framework-dependent application.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '-r=[The target runtime to publish for. This is used when creating a self-contained deployment. The default is to publish a framework-dependent application.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '--configuration=[The configuration to publish for. The default is '\''Release'\'' for NET 8.0 projects and above, but '\''Debug'\'' for older projects.]:CONFIGURATION:->dotnet_dynamic_complete' \
                        '-c=[The configuration to publish for. The default is '\''Release'\'' for NET 8.0 projects and above, but '\''Debug'\'' for older projects.]:CONFIGURATION:->dotnet_dynamic_complete' \
                        '--version-suffix=[Set the value of the \$(VersionSuffix) property to use when building the project.]:VERSION_SUFFIX: ' \
                        '--interactive=[Allows the command to stop and wait for user input or action (for example to complete authentication).]: :((False\:"False" True\:"True" ))' \
                        '--no-restore[Do not restore the project before building.]' \
                        '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '/v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '/verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--arch=[The target architecture.]:ARCH: ' \
                        '-a=[The target architecture.]:ARCH: ' \
                        '--os=[The target operating system.]:OS: ' \
                        '--disable-build-servers[Force the command to ignore any persistent build servers.]' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        '*::PROJECT | SOLUTION | FILE -- The project or solution or C# (file-based program) file to operate on. If a file is not specified, the command will search the current directory for a project or solution.: ' \
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
                    ;;
                (reference)
                    _arguments "${_arguments_options[@]}" : \
                        '--project=[The project file to operate on. If a file is not specified, the command will search the current directory for one.]: : ' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        ":: :_testhost__reference_commands" \
                        "*::: :->reference" \
                        && ret=0
                        case $state in
                            (reference)
                                words=($line[1] "${words[@]}")
                                (( CURRENT += 1 ))
                                curcontext="${curcontext%:*:*}:testhost-reference-command-$line[1]:"
                                case $line[1] in
                                    (add)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--framework=[Add the reference only when targeting a specific framework.]:FRAMEWORK:->dotnet_dynamic_complete' \
                                            '-f=[Add the reference only when targeting a specific framework.]:FRAMEWORK:->dotnet_dynamic_complete' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--project=[The project file to operate on. If a file is not specified, the command will search the current directory for one.]: : ' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::PROJECT_PATH -- The paths to the projects to add as references.: ' \
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
                                        ;;
                                    (list)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--project=[The project file to operate on. If a file is not specified, the command will search the current directory for one.]: : ' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            && ret=0
                                        ;;
                                    (remove)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--framework=[Remove the reference only when targeting a specific framework.]:FRAMEWORK: ' \
                                            '-f=[Remove the reference only when targeting a specific framework.]:FRAMEWORK: ' \
                                            '--project=[The project file to operate on. If a file is not specified, the command will search the current directory for one.]: : ' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::PROJECT_PATH -- The paths to the referenced projects to remove.:->dotnet_dynamic_complete' \
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
                                        ;;
                                esac
                            ;;
                        esac
                    ;;
                (restore)
                    _arguments "${_arguments_options[@]}" : \
                        '--disable-build-servers[Force the command to ignore any persistent build servers.]' \
                        '*--source=[The NuGet package source to use for the restore.]:SOURCE: ' \
                        '*-s=[The NuGet package source to use for the restore.]:SOURCE: ' \
                        '--packages=[The directory to restore packages to.]:PACKAGES_DIR: ' \
                        '--use-current-runtime[Use current runtime as the target runtime.]' \
                        '--ucr[Use current runtime as the target runtime.]' \
                        '--disable-parallel[Prevent restoring multiple projects in parallel.]' \
                        '--configfile=[The NuGet configuration file to use.]:FILE: ' \
                        '--no-http-cache[Disable Http Caching for packages.]' \
                        '--ignore-failed-sources[Treat package source failures as warnings.]' \
                        '--force[Force all dependencies to be resolved even if the last restore was successful. This is equivalent to deleting project.assets.json.]' \
                        '-f[Force all dependencies to be resolved even if the last restore was successful. This is equivalent to deleting project.assets.json.]' \
                        '*--runtime=[The target runtime to restore packages for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '*-r=[The target runtime to restore packages for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '--no-dependencies[Do not restore project-to-project references and only restore the specified project.]' \
                        '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--interactive=[Allows the command to stop and wait for user input or action (for example to complete authentication).]: :((False\:"False" True\:"True" ))' \
                        '--artifacts-path=[The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.]:ARTIFACTS_DIR: ' \
                        '--use-lock-file[Enables project lock file to be generated and used with restore.]' \
                        '--locked-mode[Don'\''t allow updating project lock file.]' \
                        '--lock-file-path=[Output location where project lock file is written. By default, this is '\''PROJECT_ROOT\packages.lock.json'\''.]:LOCK_FILE_PATH: ' \
                        '--force-evaluate[Forces restore to reevaluate all dependencies even if a lock file already exists.]' \
                        '--arch=[The target architecture.]:ARCH: ' \
                        '-a=[The target architecture.]:ARCH: ' \
                        '--os=[The target operating system.]:OS: ' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        '*::PROJECT | SOLUTION | FILE -- The project or solution or C# (file-based program) file to operate on. If a file is not specified, the command will search the current directory for a project or solution.: ' \
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
                    ;;
                (run)
                    _arguments "${_arguments_options[@]}" : \
                        '--configuration=[The configuration to run for. The default for most projects is '\''Debug'\''.]:CONFIGURATION:->dotnet_dynamic_complete' \
                        '-c=[The configuration to run for. The default for most projects is '\''Debug'\''.]:CONFIGURATION:->dotnet_dynamic_complete' \
                        '--framework=[The target framework to run for. The target framework must also be specified in the project file.]:FRAMEWORK:->dotnet_dynamic_complete' \
                        '-f=[The target framework to run for. The target framework must also be specified in the project file.]:FRAMEWORK:->dotnet_dynamic_complete' \
                        '--runtime=[The target runtime to run for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '-r=[The target runtime to run for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '--project=[The path to the project file to run (defaults to the current directory if there is only one project).]:PROJECT_PATH: ' \
                        '--file=[The path to the file-based app to run (can be also passed as the first argument if there is no project in the current directory).]:FILE_PATH: ' \
                        '--launch-profile=[The name of the launch profile (if any) to use when launching the application.]:LAUNCH_PROFILE: ' \
                        '-lp=[The name of the launch profile (if any) to use when launching the application.]:LAUNCH_PROFILE: ' \
                        '--no-launch-profile[Do not attempt to use launchSettings.json or \[app\].run.json to configure the application.]' \
                        '--no-build[Do not build the project before running. Implies --no-restore.]' \
                        '--interactive=[Allows the command to stop and wait for user input or action (for example to complete authentication).]: :((False\:"False" True\:"True" ))' \
                        '--no-restore[Do not restore the project before building.]' \
                        '--no-cache[Skip up to date checks and always build the program before running.]' \
                        '--self-contained=[Publish the .NET runtime with your application so the runtime doesn'\''t need to be installed on the target machine. The default is '\''false.'\'' However, when targeting .NET 7 or lower, the default is '\''true'\'' if a runtime identifier is specified.]: :((False\:"False" True\:"True" ))' \
                        '--sc=[Publish the .NET runtime with your application so the runtime doesn'\''t need to be installed on the target machine. The default is '\''false.'\'' However, when targeting .NET 7 or lower, the default is '\''true'\'' if a runtime identifier is specified.]: :((False\:"False" True\:"True" ))' \
                        '--no-self-contained[Publish your application as a framework dependent application. A compatible .NET runtime must be installed on the target machine to run your application.]' \
                        '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '/v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '/verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--arch=[The target architecture.]:ARCH: ' \
                        '-a=[The target architecture.]:ARCH: ' \
                        '--os=[The target operating system.]:OS: ' \
                        '--disable-build-servers[Force the command to ignore any persistent build servers.]' \
                        '--artifacts-path=[The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.]:ARTIFACTS_DIR: ' \
                        '*--environment=[Sets the value of an environment variable.  Creates the variable if it does not exist, overrides if it does.  This will force the tests to be run in an isolated process.  This argument can be specified multiple times to provide multiple variables.  Examples\: -e VARIABLE=abc -e VARIABLE=\"value with spaces\" -e VARIABLE=\"value;seperated with;semicolons\" -e VAR1=abc -e VAR2=def -e VAR3=ghi ]:NAME="VALUE": ' \
                        '*-e=[Sets the value of an environment variable.  Creates the variable if it does not exist, overrides if it does.  This will force the tests to be run in an isolated process.  This argument can be specified multiple times to provide multiple variables.  Examples\: -e VARIABLE=abc -e VARIABLE=\"value with spaces\" -e VARIABLE=\"value;seperated with;semicolons\" -e VAR1=abc -e VAR2=def -e VAR3=ghi ]:NAME="VALUE": ' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        '*::applicationArguments -- Arguments passed to the application that is being run.: ' \
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
                    ;;
                (solution)
                    _arguments "${_arguments_options[@]}" : \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        '::SLN_FILE -- The solution file to operate on. If not specified, the command will search the current directory for one.: ' \
                        ":: :_testhost__solution_commands" \
                        "*::: :->solution" \
                        && ret=0
                        case $state in
                            (solution)
                                words=($line[2] "${words[@]}")
                                (( CURRENT += 1 ))
                                curcontext="${curcontext%:*:*}:testhost-solution-command-$line[2]:"
                                case $line[2] in
                                    (add)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--in-root=[Place project in root of the solution, rather than creating a solution folder.]: :((False\:"False" True\:"True" ))' \
                                            '--solution-folder=[The destination solution folder path to add the projects to.]: : ' \
                                            '-s=[The destination solution folder path to add the projects to.]: : ' \
                                            '--include-references=[Recursively add projects'\'' ReferencedProjects to solution]: :((False\:"False" True\:"True" ))' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::PROJECT_PATH -- The paths to the projects to add to the solution.: ' \
                                            && ret=0
                                        ;;
                                    (list)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--solution-folders[Display solution folder paths.]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            && ret=0
                                        ;;
                                    (remove)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::PROJECT_PATH -- The project paths or names to remove from the solution.: ' \
                                            && ret=0
                                        ;;
                                    (migrate)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '::SLN_FILE -- The solution file to operate on. If not specified, the command will search the current directory for one.: ' \
                                            && ret=0
                                        ;;
                                esac
                            ;;
                        esac
                    ;;
                (store)
                    _arguments "${_arguments_options[@]}" : \
                        '*--manifest=[The XML file that contains the list of packages to be stored.]:PROJECT_MANIFEST: ' \
                        '*-m=[The XML file that contains the list of packages to be stored.]:PROJECT_MANIFEST: ' \
                        '--framework-version=[The Microsoft.NETCore.App package version that will be used to run the assemblies.]:FRAMEWORK_VERSION: ' \
                        '--output=[The output directory to store the given assemblies in.]:OUTPUT_DIR: ' \
                        '-o=[The output directory to store the given assemblies in.]:OUTPUT_DIR: ' \
                        '--working-dir=[The working directory used by the command to execute.]:WORKING_DIR: ' \
                        '-w=[The working directory used by the command to execute.]:WORKING_DIR: ' \
                        '--skip-optimization[Skip the optimization phase.]' \
                        '--skip-symbols[Skip creating symbol files which can be used for profiling the optimized assemblies.]' \
                        '--framework=[The target framework to store packages for. The target framework has to be specified in the project file.]:FRAMEWORK:->dotnet_dynamic_complete' \
                        '-f=[The target framework to store packages for. The target framework has to be specified in the project file.]:FRAMEWORK:->dotnet_dynamic_complete' \
                        '--runtime=[The target runtime to store packages for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '-r=[The target runtime to store packages for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '/v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '/verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--use-current-runtime[Use current runtime as the target runtime.]' \
                        '--ucr[Use current runtime as the target runtime.]' \
                        '--disable-build-servers[Force the command to ignore any persistent build servers.]' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        '*::argument: ' \
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
                    ;;
                (test)
                    _arguments "${_arguments_options[@]}" : \
                        '--settings=[The settings file to use when running tests.]:SETTINGS_FILE: ' \
                        '-s=[The settings file to use when running tests.]:SETTINGS_FILE: ' \
                        '--list-tests[List the discovered tests instead of running the tests.]' \
                        '-t[List the discovered tests instead of running the tests.]' \
                        '*--environment=[Sets the value of an environment variable.  Creates the variable if it does not exist, overrides if it does.  This will force the tests to be run in an isolated process.  This argument can be specified multiple times to provide multiple variables.  Examples\: -e VARIABLE=abc -e VARIABLE=\"value with spaces\" -e VARIABLE=\"value;seperated with;semicolons\" -e VAR1=abc -e VAR2=def -e VAR3=ghi ]:NAME="VALUE": ' \
                        '*-e=[Sets the value of an environment variable.  Creates the variable if it does not exist, overrides if it does.  This will force the tests to be run in an isolated process.  This argument can be specified multiple times to provide multiple variables.  Examples\: -e VARIABLE=abc -e VARIABLE=\"value with spaces\" -e VARIABLE=\"value;seperated with;semicolons\" -e VAR1=abc -e VAR2=def -e VAR3=ghi ]:NAME="VALUE": ' \
                        '--filter=[Run tests that match the given expression.                                         Examples\:                                         Run tests with priority set to 1\: --filter \"Priority = 1\"                                         Run a test with the specified full name\: --filter \"FullyQualifiedName=Namespace.ClassName.MethodName\"                                         Run tests that contain the specified name\: --filter \"FullyQualifiedName~Namespace.Class\"                                         See https\://aka.ms/vstest-filtering for more information on filtering support.                                         ]:EXPRESSION: ' \
                        '*--test-adapter-path=[The path to the custom adapters to use for the test run.]:ADAPTER_PATH: ' \
                        '*--logger=[The logger to use for test results.                                         Examples\:                                         Log in trx format using a unique file name\: --logger trx                                         Log in trx format using the specified file name\: --logger \"trx;LogFileName=<TestResults.trx>\"                                         See https\://aka.ms/vstest-report for more information on logger arguments.]:LOGGER: ' \
                        '*-l=[The logger to use for test results.                                         Examples\:                                         Log in trx format using a unique file name\: --logger trx                                         Log in trx format using the specified file name\: --logger \"trx;LogFileName=<TestResults.trx>\"                                         See https\://aka.ms/vstest-report for more information on logger arguments.]:LOGGER: ' \
                        '--output=[The output directory to place built artifacts in.]:OUTPUT_DIR: ' \
                        '-o=[The output directory to place built artifacts in.]:OUTPUT_DIR: ' \
                        '--artifacts-path=[The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.]:ARTIFACTS_DIR: ' \
                        '--diag=[Enable verbose logging to the specified file.]:LOG_FILE: ' \
                        '-d=[Enable verbose logging to the specified file.]:LOG_FILE: ' \
                        '--no-build[Do not build the project before testing. Implies --no-restore.]' \
                        '--results-directory=[The directory where the test results will be placed. The specified directory will be created if it does not exist.]:RESULTS_DIR: ' \
                        '*--collect=[The friendly name of the data collector to use for the test run.                                         More info here\: https\://aka.ms/vstest-collect]:DATA_COLLECTOR_NAME: ' \
                        '--blame[Runs the tests in blame mode. This option is helpful in isolating problematic tests that cause the test host to crash or hang, but it does not create a memory dump by default.  When a crash is detected, it creates an sequence file in TestResults/guid/guid_Sequence.xml that captures the order of tests that were run before the crash.  Based on the additional settings, hang dump or crash dump can also be collected.  Example\:   Timeout the test run when test takes more than the default timeout of 1 hour, and collect crash dump when the test host exits unexpectedly.   (Crash dumps require additional setup, see below.)   dotnet test --blame-hang --blame-crash Example\:   Timeout the test run when a test takes more than 20 minutes and collect hang dump.   dotnet test --blame-hang-timeout 20min ]' \
                        '--blame-crash[Runs the tests in blame mode and collects a crash dump when the test host exits unexpectedly. This option depends on the version of .NET used, the type of error, and the operating system.  For exceptions in managed code, a dump will be automatically collected on .NET 5.0 and later versions. It will generate a dump for testhost or any child process that also ran on .NET 5.0 and crashed. Crashes in native code will not generate a dump. This option works on Windows, macOS, and Linux.  Crash dumps in native code, or when targetting .NET Framework, or .NET Core 3.1 and earlier versions, can only be collected on Windows, by using Procdump. A directory that contains procdump.exe and procdump64.exe must be in the PATH or PROCDUMP_PATH environment variable.  The tools can be downloaded here\: https\://docs.microsoft.com/sysinternals/downloads/procdump  To collect a crash dump from a native application running on .NET 5.0 or later, the usage of Procdump can be forced by setting the VSTEST_DUMP_FORCEPROCDUMP environment variable to 1.  Implies --blame.]' \
                        '--blame-crash-dump-type=[The type of crash dump to be collected. Supported values are full (default) and mini. Implies --blame-crash.]:DUMP_TYPE:((full\:"full" mini\:"mini" ))' \
                        '--blame-crash-collect-always[Enables collecting crash dump on expected as well as unexpected testhost exit.]' \
                        '--blame-hang[Run the tests in blame mode and enables collecting hang dump when test exceeds the given timeout.]' \
                        '--blame-hang-dump-type=[The type of crash dump to be collected. The supported values are full (default), mini, and none. When '\''none'\'' is used then test host is terminated on timeout, but no dump is collected. Implies --blame-hang.]:DUMP_TYPE:((full\:"full" mini\:"mini" none\:"none" ))' \
                        '--blame-hang-timeout=[Per-test timeout, after which hang dump is triggered and the testhost process is terminated. Default is 1h. The timeout value is specified in the following format\: 1.5h / 90m / 5400s / 5400000ms. When no unit is used (e.g. 5400000), the value is assumed to be in milliseconds. When used together with data driven tests, the timeout behavior depends on the test adapter used. For xUnit, NUnit and MSTest 2.2.4+ the timeout is renewed after every test case, For MSTest before 2.2.4, the timeout is used for all testcases.]:TIMESPAN: ' \
                        '--nologo[Run test(s), without displaying Microsoft Testplatform banner]' \
                        '--configuration=[The configuration to use for running tests. The default for most projects is '\''Debug'\''.]:CONFIGURATION:->dotnet_dynamic_complete' \
                        '-c=[The configuration to use for running tests. The default for most projects is '\''Debug'\''.]:CONFIGURATION:->dotnet_dynamic_complete' \
                        '--framework=[The target framework to run tests for. The target framework must also be specified in the project file.]:FRAMEWORK:->dotnet_dynamic_complete' \
                        '-f=[The target framework to run tests for. The target framework must also be specified in the project file.]:FRAMEWORK:->dotnet_dynamic_complete' \
                        '--runtime=[The target runtime to test for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '-r=[The target runtime to test for.]:RUNTIME_IDENTIFIER:->dotnet_dynamic_complete' \
                        '--no-restore[Do not restore the project before building.]' \
                        '--interactive=[Allows the command to stop and wait for user input or action (for example to complete authentication).]: :((False\:"False" True\:"True" ))' \
                        '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '-verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '/v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '/verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                        '--arch=[The target architecture.]:ARCH: ' \
                        '-a=[The target architecture.]:ARCH: ' \
                        '--os=[The target operating system.]:OS: ' \
                        '--disable-build-servers[Force the command to ignore any persistent build servers.]' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
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
                    ;;
                (tool)
                    _arguments "${_arguments_options[@]}" : \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        ":: :_testhost__tool_commands" \
                        "*::: :->tool" \
                        && ret=0
                        case $state in
                            (tool)
                                words=($line[1] "${words[@]}")
                                (( CURRENT += 1 ))
                                curcontext="${curcontext%:*:*}:testhost-tool-command-$line[1]:"
                                case $line[1] in
                                    (install)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--global[Install the tool for the current user.]' \
                                            '-g[Install the tool for the current user.]' \
                                            '--local[Install the tool and add to the local tool manifest (default).]' \
                                            '--tool-path=[The directory where the tool will be installed. The directory will be created if it does not exist.]:PATH: ' \
                                            '--version=[The version of the tool package to install.]:VERSION: ' \
                                            '--configfile=[The NuGet configuration file to use.]:FILE: ' \
                                            '--tool-manifest=[Path to the manifest file.]:PATH: ' \
                                            '*--add-source=[Add an additional NuGet package source to use during installation.]:ADDSOURCE: ' \
                                            '*--source=[Replace all NuGet package sources to use during installation with these.]:SOURCE: ' \
                                            '--framework=[The target framework to install the tool for.]:FRAMEWORK: ' \
                                            '--prerelease[Include pre-release packages.]' \
                                            '--disable-parallel[Prevent restoring multiple projects in parallel.]' \
                                            '--ignore-failed-sources[Treat package source failures as warnings.]' \
                                            '--no-http-cache[Do not cache packages and http requests.]' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--arch=[The target architecture.]: : ' \
                                            '-a=[The target architecture.]: : ' \
                                            '--create-manifest-if-needed=[Create a tool manifest if one isn'\''t found during tool installation. For information on how manifests are located, see https\://aka.ms/dotnet/tools/create-manifest-if-needed]: :((False\:"False" True\:"True" ))' \
                                            '--allow-downgrade[Allow package downgrade when installing a .NET tool package.]' \
                                            '--allow-roll-forward[Allow a .NET tool to roll forward to newer versions of the .NET runtime if the runtime it targets isn'\''t installed.]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            ':packageId -- Package reference in the form of a package identifier like '\''Newtonsoft.Json'\'' or package identifier and version separated by '\''@'\'' like '\''Newtonsoft.Json@13.0.3'\''.:->dotnet_dynamic_complete' \
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
                                        ;;
                                    (uninstall)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--global[Uninstall the tool from the current user'\''s tools directory.]' \
                                            '-g[Uninstall the tool from the current user'\''s tools directory.]' \
                                            '--local[Uninstall the tool and remove it from the local tool manifest.]' \
                                            '--tool-path=[The directory containing the tool to uninstall.]:PATH: ' \
                                            '--tool-manifest=[Path to the manifest file.]:PATH: ' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            ':packageId -- Package reference: ' \
                                            && ret=0
                                        ;;
                                    (update)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--global[Install the tool for the current user.]' \
                                            '-g[Install the tool for the current user.]' \
                                            '--local[Install the tool and add to the local tool manifest (default).]' \
                                            '--tool-path=[The directory where the tool will be installed. The directory will be created if it does not exist.]:PATH: ' \
                                            '--version=[The version of the tool package to install.]:VERSION: ' \
                                            '--configfile=[The NuGet configuration file to use.]:FILE: ' \
                                            '--tool-manifest=[Path to the manifest file.]:PATH: ' \
                                            '*--add-source=[Add an additional NuGet package source to use during installation.]:ADDSOURCE: ' \
                                            '*--source=[Replace all NuGet package sources to use during installation with these.]:SOURCE: ' \
                                            '--framework=[The target framework to install the tool for.]:FRAMEWORK: ' \
                                            '--prerelease[Include pre-release packages.]' \
                                            '--disable-parallel[Prevent restoring multiple projects in parallel.]' \
                                            '--ignore-failed-sources[Treat package source failures as warnings.]' \
                                            '--no-http-cache[Do not cache packages and http requests.]' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--allow-downgrade[Allow package downgrade when installing a .NET tool package.]' \
                                            '--all[Update all tools.]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '::packageId -- Package reference in the form of a package identifier like '\''Newtonsoft.Json'\'' or package identifier and version separated by '\''@'\'' like '\''Newtonsoft.Json@13.0.3'\''.:->dotnet_dynamic_complete' \
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
                                        ;;
                                    (list)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--global[List tools installed for the current user.]' \
                                            '-g[List tools installed for the current user.]' \
                                            '--local[List the tools installed in the local tool manifest.]' \
                                            '--tool-path=[The directory containing the tools to list.]:PATH: ' \
                                            '--format=[The output format for the list of tools.]: :((json\:"json" table\:"table" ))' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '::packageId -- The NuGet Package Id of the tool to list: ' \
                                            && ret=0
                                        ;;
                                    (run)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--allow-roll-forward[Allow a .NET tool to roll forward to newer versions of the .NET runtime if the runtime it targets isn'\''t installed.]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            ':commandName -- The command name of the tool to run.: ' \
                                            '*::toolArguments -- Arguments forwarded to the tool: ' \
                                            && ret=0
                                        ;;
                                    (search)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--detail[Show detail result of the query.]' \
                                            '--skip=[The number of results to skip, for pagination.]:Skip: ' \
                                            '--take=[The number of results to return, for pagination.]:Take: ' \
                                            '--prerelease[Include pre-release packages.]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            ':searchTerm -- Search term from package id or package description. Require at least one character.: ' \
                                            && ret=0
                                        ;;
                                    (restore)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--configfile=[The NuGet configuration file to use.]:FILE: ' \
                                            '*--add-source=[Add an additional NuGet package source to use during installation.]:ADDSOURCE: ' \
                                            '--tool-manifest=[Path to the manifest file.]:PATH: ' \
                                            '--disable-parallel[Prevent restoring multiple projects in parallel.]' \
                                            '--ignore-failed-sources[Treat package source failures as warnings.]' \
                                            '--no-http-cache[Do not cache packages and http requests.]' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            && ret=0
                                        ;;
                                    (execute)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--version=[The version of the tool package to install.]:VERSION: ' \
                                            '--yes[Accept all confirmation prompts using \"yes.\"]' \
                                            '-y[Accept all confirmation prompts using \"yes.\"]' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--allow-roll-forward[Allow a .NET tool to roll forward to newer versions of the .NET runtime if the runtime it targets isn'\''t installed.]' \
                                            '--prerelease[Include pre-release packages.]' \
                                            '--configfile=[The NuGet configuration file to use.]:FILE: ' \
                                            '*--source=[Replace all NuGet package sources to use during installation with these.]:SOURCE: ' \
                                            '*--add-source=[Add an additional NuGet package source to use during installation.]:ADDSOURCE: ' \
                                            '--disable-parallel[Prevent restoring multiple projects in parallel.]' \
                                            '--ignore-failed-sources[Treat package source failures as warnings.]' \
                                            '--no-http-cache[Do not cache packages and http requests.]' \
                                            '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            ':packageId -- Package reference in the form of a package identifier like '\''Newtonsoft.Json'\'' or package identifier and version separated by '\''@'\'' like '\''Newtonsoft.Json@13.0.3'\''.:->dotnet_dynamic_complete' \
                                            '*::commandArguments -- Arguments forwarded to the tool: ' \
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
                                        ;;
                                esac
                            ;;
                        esac
                    ;;
                (vstest)
                    _arguments "${_arguments_options[@]}" : \
                        '--Platform=[]: : ' \
                        '--Framework=[]: : ' \
                        '*--logger=[]: : ' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        && ret=0
                    ;;
                (help)
                    _arguments "${_arguments_options[@]}" : \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        '*::COMMAND_NAME -- The SDK command to launch online help for.: ' \
                        && ret=0
                    ;;
                (sdk)
                    _arguments "${_arguments_options[@]}" : \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        ":: :_testhost__sdk_commands" \
                        "*::: :->sdk" \
                        && ret=0
                        case $state in
                            (sdk)
                                words=($line[1] "${words[@]}")
                                (( CURRENT += 1 ))
                                curcontext="${curcontext%:*:*}:testhost-sdk-command-$line[1]:"
                                case $line[1] in
                                    (check)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            && ret=0
                                        ;;
                                esac
                            ;;
                        esac
                    ;;
                (workload)
                    _arguments "${_arguments_options[@]}" : \
                        '--info[Display information about installed workloads.]' \
                        '--version[Display the currently installed workload version.]' \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        ":: :_testhost__workload_commands" \
                        "*::: :->workload" \
                        && ret=0
                        case $state in
                            (workload)
                                words=($line[1] "${words[@]}")
                                (( CURRENT += 1 ))
                                curcontext="${curcontext%:*:*}:testhost-workload-command-$line[1]:"
                                case $line[1] in
                                    (install)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--configfile=[The NuGet configuration file to use.]:FILE: ' \
                                            '*--source=[The NuGet package source to use during the restore. To specify multiple sources, repeat the option.]:SOURCE: ' \
                                            '*-s=[The NuGet package source to use during the restore. To specify multiple sources, repeat the option.]:SOURCE: ' \
                                            '--include-previews=[Allow prerelease workload manifests.]: :((False\:"False" True\:"True" ))' \
                                            '--skip-manifest-update[Skip updating the workload manifests.]' \
                                            '--temp-dir=[Specify a temporary directory for this command to download and extract NuGet packages (must be secure).]: : ' \
                                            '--disable-parallel[Prevent restoring multiple projects in parallel.]' \
                                            '--ignore-failed-sources[Treat package source failures as warnings.]' \
                                            '--no-http-cache[Do not cache packages and http requests.]' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '*--version=[A workload version to display or one or more workloads and their versions joined by the '\''@'\'' character.]: : ' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::workloadId -- The NuGet package ID of the workload to install.: ' \
                                            && ret=0
                                        ;;
                                    (update)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--configfile=[The NuGet configuration file to use.]:FILE: ' \
                                            '*--source=[The NuGet package source to use during the restore. To specify multiple sources, repeat the option.]:SOURCE: ' \
                                            '*-s=[The NuGet package source to use during the restore. To specify multiple sources, repeat the option.]:SOURCE: ' \
                                            '--include-previews=[Allow prerelease workload manifests.]: :((False\:"False" True\:"True" ))' \
                                            '--temp-dir=[Specify a temporary directory for this command to download and extract NuGet packages (must be secure).]: : ' \
                                            '--from-previous-sdk=[Include workloads installed with earlier SDK versions in update.]: :((False\:"False" True\:"True" ))' \
                                            '--advertising-manifests-only[Only update advertising manifests.]' \
                                            '*--version=[A workload version to display or one or more workloads and their versions joined by the '\''@'\'' character.]: : ' \
                                            '--disable-parallel[Prevent restoring multiple projects in parallel.]' \
                                            '--ignore-failed-sources[Treat package source failures as warnings.]' \
                                            '--no-http-cache[Do not cache packages and http requests.]' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--from-history=[Update workloads to a previous version specified by the argument. Use the '\''dotnet workload history'\'' to see available workload history records.]: : ' \
                                            '--manifests-only=[Update to the workload versions specified in the history without changing which workloads are installed. Currently installed workloads will be updated to match the specified history version.]: : ' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            && ret=0
                                        ;;
                                    (list)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            && ret=0
                                        ;;
                                    (search)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '::SEARCH_STRING -- The text to search for in the IDs and descriptions of available workloads.: ' \
                                            ":: :_testhost__workload__search_commands" \
                                            "*::: :->search" \
                                            && ret=0
                                            case $state in
                                                (search)
                                                    words=($line[2] "${words[@]}")
                                                    (( CURRENT += 1 ))
                                                    curcontext="${curcontext%:*:*}:testhost-workload-search-command-$line[2]:"
                                                    case $line[2] in
                                                        (version)
                                                            _arguments "${_arguments_options[@]}" : \
                                                                '--format=[Changes the format of outputted workload versions. Can take '\''json'\'' or '\''list'\'']: : ' \
                                                                '--take=[]: : ' \
                                                                '--include-previews=[]: :((False\:"False" True\:"True" ))' \
                                                                '--help[Show command line help.]' \
                                                                '-h[Show command line help.]' \
                                                                '*::WORKLOAD_VERSION -- Output workload manifest versions associated with the provided workload version.: ' \
                                                                && ret=0
                                                            ;;
                                                    esac
                                                ;;
                                            esac
                                        ;;
                                    (uninstall)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::workloadId -- The NuGet package ID of the workload to install.: ' \
                                            && ret=0
                                        ;;
                                    (repair)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--configfile=[The NuGet configuration file to use.]:FILE: ' \
                                            '*--source=[The NuGet package source to use during the restore. To specify multiple sources, repeat the option.]:SOURCE: ' \
                                            '*-s=[The NuGet package source to use during the restore. To specify multiple sources, repeat the option.]:SOURCE: ' \
                                            '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '--disable-parallel[Prevent restoring multiple projects in parallel.]' \
                                            '--ignore-failed-sources[Treat package source failures as warnings.]' \
                                            '--no-http-cache[Do not cache packages and http requests.]' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            && ret=0
                                        ;;
                                    (restore)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--configfile=[The NuGet configuration file to use.]:FILE: ' \
                                            '*--source=[The NuGet package source to use during the restore. To specify multiple sources, repeat the option.]:SOURCE: ' \
                                            '*-s=[The NuGet package source to use during the restore. To specify multiple sources, repeat the option.]:SOURCE: ' \
                                            '--include-previews=[Allow prerelease workload manifests.]: :((False\:"False" True\:"True" ))' \
                                            '--skip-manifest-update[Skip updating the workload manifests.]' \
                                            '--temp-dir=[Specify a temporary directory for this command to download and extract NuGet packages (must be secure).]: : ' \
                                            '--disable-parallel[Prevent restoring multiple projects in parallel.]' \
                                            '--ignore-failed-sources[Treat package source failures as warnings.]' \
                                            '--no-http-cache[Do not cache packages and http requests.]' \
                                            '--interactive[Allows the command to stop and wait for user input or action (for example to complete authentication).]' \
                                            '--verbosity=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '-v=[Set the MSBuild verbosity level. Allowed values are q\[uiet\], m\[inimal\], n\[ormal\], d\[etailed\], and diag\[nostic\].]:LEVEL:((d\:"d" detailed\:"detailed" diag\:"diag" diagnostic\:"diagnostic" m\:"m" minimal\:"minimal" n\:"n" normal\:"normal" q\:"q" quiet\:"quiet" ))' \
                                            '*--version=[A workload version to display or one or more workloads and their versions joined by the '\''@'\'' character.]: : ' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '*::PROJECT | SOLUTION -- The project or solution file to operate on. If a file is not specified, the command will search the current directory for one.: ' \
                                            && ret=0
                                        ;;
                                    (clean)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--all=[Causes clean to remove and uninstall all workload components from all SDK versions.]: :((False\:"False" True\:"True" ))' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            && ret=0
                                        ;;
                                    (config)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--update-mode=[Controls whether updates should look for workload sets or the latest version of each individual manifest.]: :((manifests\:"manifests" workload-set\:"workload-set" ))' \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            && ret=0
                                        ;;
                                    (history)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            && ret=0
                                        ;;
                                esac
                            ;;
                        esac
                    ;;
                (completions)
                    _arguments "${_arguments_options[@]}" : \
                        '--help[Show command line help.]' \
                        '-h[Show command line help.]' \
                        ":: :_testhost__completions_commands" \
                        "*::: :->completions" \
                        && ret=0
                        case $state in
                            (completions)
                                words=($line[1] "${words[@]}")
                                (( CURRENT += 1 ))
                                curcontext="${curcontext%:*:*}:testhost-completions-command-$line[1]:"
                                case $line[1] in
                                    (script)
                                        _arguments "${_arguments_options[@]}" : \
                                            '--help[Show command line help.]' \
                                            '-h[Show command line help.]' \
                                            '::shell -- The shell for which to generate or register completions:((bash\:"Generates a completion script for the Bourne Again SHell (bash)." fish\:"Generates a completion script for the Fish shell." nushell\:"Generates a completion script for the NuShell shell." pwsh\:"Generates a completion script for PowerShell Core. These scripts will not work on Windows PowerShell." zsh\:"Generates a completion script for the Zsh shell." ))' \
                                            && ret=0
                                        ;;
                                esac
                            ;;
                        esac
                    ;;
            esac
        ;;
    esac
}

(( $+functions[_testhost_commands] )) ||
_testhost_commands() {
    local commands; commands=(
        'build:.NET Builder' \
        'build-server:Interact with servers started from a build.' \
        'clean:.NET Clean Command' \
        'format:' \
        'fsi:' \
        'msbuild:.NET Builder' \
        'new:Template Instantiation Commands for .NET CLI.' \
        'nuget:' \
        'pack:.NET Core NuGet Package Packer' \
        'package:' \
        'project:' \
        'publish:Publisher for the .NET Platform' \
        'reference:.NET Remove Command' \
        'restore:.NET dependency restorer' \
        'run:.NET Run Command' \
        'solution:.NET modify solution file command' \
        'store:Stores the specified assemblies for the .NET Platform. By default, these will be optimized for the target runtime and framework.' \
        'test:.NET Test Driver' \
        'tool:Install or work with tools that extend the .NET experience.' \
        'vstest:' \
        'help:.NET CLI help utility' \
        'sdk:.NET SDK Command' \
        'workload:Install or work with workloads that extend the .NET experience.' \
        'completions:Commands for generating and registering completions for supported shells' \
    )
    _describe -t commands 'testhost commands' commands "$@"
}

(( $+functions[_testhost__build_commands] )) ||
_testhost__build_commands() {
    local commands; commands=()
    _describe -t commands 'testhost build commands' commands "$@"
}

(( $+functions[_testhost__build-server_commands] )) ||
_testhost__build-server_commands() {
    local commands; commands=(
        'shutdown:Shuts down build servers that are started from dotnet. By default, all servers are shut down.' \
    )
    _describe -t commands 'testhost build-server commands' commands "$@"
}

(( $+functions[_testhost__build-server__shutdown_commands] )) ||
_testhost__build-server__shutdown_commands() {
    local commands; commands=()
    _describe -t commands 'testhost build-server shutdown commands' commands "$@"
}

(( $+functions[_testhost__clean_commands] )) ||
_testhost__clean_commands() {
    local commands; commands=()
    _describe -t commands 'testhost clean commands' commands "$@"
}

(( $+functions[_testhost__format_commands] )) ||
_testhost__format_commands() {
    local commands; commands=()
    _describe -t commands 'testhost format commands' commands "$@"
}

(( $+functions[_testhost__fsi_commands] )) ||
_testhost__fsi_commands() {
    local commands; commands=()
    _describe -t commands 'testhost fsi commands' commands "$@"
}

(( $+functions[_testhost__msbuild_commands] )) ||
_testhost__msbuild_commands() {
    local commands; commands=()
    _describe -t commands 'testhost msbuild commands' commands "$@"
}

(( $+functions[_testhost__new_commands] )) ||
_testhost__new_commands() {
    local commands; commands=(
        'create:Instantiates a template with given short name. An alias of '\''dotnet new <template name>'\''.' \
        'install:Installs a template package.' \
        'uninstall:Uninstalls a template package.' \
        'update:Checks the currently installed template packages for update, and install the updates.' \
        'search:Searches for the templates on NuGet.org.' \
        'list:Lists templates containing the specified template name. If no name is specified, lists all templates.' \
        'details:       Provides the details for specified template package.       The command checks if the package is installed locally, if it was not found, it searches the configured NuGet feeds.' \
    )
    _describe -t commands 'testhost new commands' commands "$@"
}

(( $+functions[_testhost__new__create_commands] )) ||
_testhost__new__create_commands() {
    local commands; commands=()
    _describe -t commands 'testhost new create commands' commands "$@"
}

(( $+functions[_testhost__new__install_commands] )) ||
_testhost__new__install_commands() {
    local commands; commands=()
    _describe -t commands 'testhost new install commands' commands "$@"
}

(( $+functions[_testhost__new__uninstall_commands] )) ||
_testhost__new__uninstall_commands() {
    local commands; commands=()
    _describe -t commands 'testhost new uninstall commands' commands "$@"
}

(( $+functions[_testhost__new__update_commands] )) ||
_testhost__new__update_commands() {
    local commands; commands=()
    _describe -t commands 'testhost new update commands' commands "$@"
}

(( $+functions[_testhost__new__search_commands] )) ||
_testhost__new__search_commands() {
    local commands; commands=()
    _describe -t commands 'testhost new search commands' commands "$@"
}

(( $+functions[_testhost__new__list_commands] )) ||
_testhost__new__list_commands() {
    local commands; commands=()
    _describe -t commands 'testhost new list commands' commands "$@"
}

(( $+functions[_testhost__new__details_commands] )) ||
_testhost__new__details_commands() {
    local commands; commands=()
    _describe -t commands 'testhost new details commands' commands "$@"
}

(( $+functions[_testhost__nuget_commands] )) ||
_testhost__nuget_commands() {
    local commands; commands=(
        'delete:' \
        'locals:' \
        'push:' \
        'verify:' \
        'trust:' \
        'sign:' \
        'why:Shows the dependency graph for a particular package for a given project or solution.' \
    )
    _describe -t commands 'testhost nuget commands' commands "$@"
}

(( $+functions[_testhost__nuget__delete_commands] )) ||
_testhost__nuget__delete_commands() {
    local commands; commands=()
    _describe -t commands 'testhost nuget delete commands' commands "$@"
}

(( $+functions[_testhost__nuget__locals_commands] )) ||
_testhost__nuget__locals_commands() {
    local commands; commands=()
    _describe -t commands 'testhost nuget locals commands' commands "$@"
}

(( $+functions[_testhost__nuget__push_commands] )) ||
_testhost__nuget__push_commands() {
    local commands; commands=()
    _describe -t commands 'testhost nuget push commands' commands "$@"
}

(( $+functions[_testhost__nuget__verify_commands] )) ||
_testhost__nuget__verify_commands() {
    local commands; commands=()
    _describe -t commands 'testhost nuget verify commands' commands "$@"
}

(( $+functions[_testhost__nuget__trust_commands] )) ||
_testhost__nuget__trust_commands() {
    local commands; commands=(
        'list:' \
        'author:' \
        'repository:' \
        'source:' \
        'certificate:' \
        'remove:' \
        'sync:' \
    )
    _describe -t commands 'testhost nuget trust commands' commands "$@"
}

(( $+functions[_testhost__nuget__trust__list_commands] )) ||
_testhost__nuget__trust__list_commands() {
    local commands; commands=()
    _describe -t commands 'testhost nuget trust list commands' commands "$@"
}

(( $+functions[_testhost__nuget__trust__author_commands] )) ||
_testhost__nuget__trust__author_commands() {
    local commands; commands=()
    _describe -t commands 'testhost nuget trust author commands' commands "$@"
}

(( $+functions[_testhost__nuget__trust__repository_commands] )) ||
_testhost__nuget__trust__repository_commands() {
    local commands; commands=()
    _describe -t commands 'testhost nuget trust repository commands' commands "$@"
}

(( $+functions[_testhost__nuget__trust__source_commands] )) ||
_testhost__nuget__trust__source_commands() {
    local commands; commands=()
    _describe -t commands 'testhost nuget trust source commands' commands "$@"
}

(( $+functions[_testhost__nuget__trust__certificate_commands] )) ||
_testhost__nuget__trust__certificate_commands() {
    local commands; commands=()
    _describe -t commands 'testhost nuget trust certificate commands' commands "$@"
}

(( $+functions[_testhost__nuget__trust__remove_commands] )) ||
_testhost__nuget__trust__remove_commands() {
    local commands; commands=()
    _describe -t commands 'testhost nuget trust remove commands' commands "$@"
}

(( $+functions[_testhost__nuget__trust__sync_commands] )) ||
_testhost__nuget__trust__sync_commands() {
    local commands; commands=()
    _describe -t commands 'testhost nuget trust sync commands' commands "$@"
}

(( $+functions[_testhost__nuget__sign_commands] )) ||
_testhost__nuget__sign_commands() {
    local commands; commands=()
    _describe -t commands 'testhost nuget sign commands' commands "$@"
}

(( $+functions[_testhost__nuget__why_commands] )) ||
_testhost__nuget__why_commands() {
    local commands; commands=()
    _describe -t commands 'testhost nuget why commands' commands "$@"
}

(( $+functions[_testhost__pack_commands] )) ||
_testhost__pack_commands() {
    local commands; commands=()
    _describe -t commands 'testhost pack commands' commands "$@"
}

(( $+functions[_testhost__package_commands] )) ||
_testhost__package_commands() {
    local commands; commands=(
        'search:Searches one or more package sources for packages that match a search term. If no sources are specified, all sources defined in the NuGet.Config are used.' \
        'add:Add a NuGet package reference to the project.' \
        'list:List all package references of the project or solution.' \
        'remove:Remove a NuGet package reference from the project.' \
        'update:Update referenced packages in a project or solution.' \
    )
    _describe -t commands 'testhost package commands' commands "$@"
}

(( $+functions[_testhost__package__search_commands] )) ||
_testhost__package__search_commands() {
    local commands; commands=()
    _describe -t commands 'testhost package search commands' commands "$@"
}

(( $+functions[_testhost__package__add_commands] )) ||
_testhost__package__add_commands() {
    local commands; commands=()
    _describe -t commands 'testhost package add commands' commands "$@"
}

(( $+functions[_testhost__package__list_commands] )) ||
_testhost__package__list_commands() {
    local commands; commands=()
    _describe -t commands 'testhost package list commands' commands "$@"
}

(( $+functions[_testhost__package__remove_commands] )) ||
_testhost__package__remove_commands() {
    local commands; commands=()
    _describe -t commands 'testhost package remove commands' commands "$@"
}

(( $+functions[_testhost__package__update_commands] )) ||
_testhost__package__update_commands() {
    local commands; commands=()
    _describe -t commands 'testhost package update commands' commands "$@"
}

(( $+functions[_testhost__project_commands] )) ||
_testhost__project_commands() {
    local commands; commands=(
        'convert:Convert a file-based program to a project-based program.' \
    )
    _describe -t commands 'testhost project commands' commands "$@"
}

(( $+functions[_testhost__project__convert_commands] )) ||
_testhost__project__convert_commands() {
    local commands; commands=()
    _describe -t commands 'testhost project convert commands' commands "$@"
}

(( $+functions[_testhost__publish_commands] )) ||
_testhost__publish_commands() {
    local commands; commands=()
    _describe -t commands 'testhost publish commands' commands "$@"
}

(( $+functions[_testhost__reference_commands] )) ||
_testhost__reference_commands() {
    local commands; commands=(
        'add:Add a project-to-project reference to the project.' \
        'list:List all project-to-project references of the project.' \
        'remove:Remove a project-to-project reference from the project.' \
    )
    _describe -t commands 'testhost reference commands' commands "$@"
}

(( $+functions[_testhost__reference__add_commands] )) ||
_testhost__reference__add_commands() {
    local commands; commands=()
    _describe -t commands 'testhost reference add commands' commands "$@"
}

(( $+functions[_testhost__reference__list_commands] )) ||
_testhost__reference__list_commands() {
    local commands; commands=()
    _describe -t commands 'testhost reference list commands' commands "$@"
}

(( $+functions[_testhost__reference__remove_commands] )) ||
_testhost__reference__remove_commands() {
    local commands; commands=()
    _describe -t commands 'testhost reference remove commands' commands "$@"
}

(( $+functions[_testhost__restore_commands] )) ||
_testhost__restore_commands() {
    local commands; commands=()
    _describe -t commands 'testhost restore commands' commands "$@"
}

(( $+functions[_testhost__run_commands] )) ||
_testhost__run_commands() {
    local commands; commands=()
    _describe -t commands 'testhost run commands' commands "$@"
}

(( $+functions[_testhost__solution_commands] )) ||
_testhost__solution_commands() {
    local commands; commands=(
        'add:Add one or more projects to a solution file.' \
        'list:List all projects in a solution file.' \
        'remove:Remove one or more projects from a solution file.' \
        'migrate:Generate a .slnx file from a .sln file.' \
    )
    _describe -t commands 'testhost solution commands' commands "$@"
}

(( $+functions[_testhost__solution__add_commands] )) ||
_testhost__solution__add_commands() {
    local commands; commands=()
    _describe -t commands 'testhost solution add commands' commands "$@"
}

(( $+functions[_testhost__solution__list_commands] )) ||
_testhost__solution__list_commands() {
    local commands; commands=()
    _describe -t commands 'testhost solution list commands' commands "$@"
}

(( $+functions[_testhost__solution__remove_commands] )) ||
_testhost__solution__remove_commands() {
    local commands; commands=()
    _describe -t commands 'testhost solution remove commands' commands "$@"
}

(( $+functions[_testhost__solution__migrate_commands] )) ||
_testhost__solution__migrate_commands() {
    local commands; commands=()
    _describe -t commands 'testhost solution migrate commands' commands "$@"
}

(( $+functions[_testhost__store_commands] )) ||
_testhost__store_commands() {
    local commands; commands=()
    _describe -t commands 'testhost store commands' commands "$@"
}

(( $+functions[_testhost__test_commands] )) ||
_testhost__test_commands() {
    local commands; commands=()
    _describe -t commands 'testhost test commands' commands "$@"
}

(( $+functions[_testhost__tool_commands] )) ||
_testhost__tool_commands() {
    local commands; commands=(
        'install:Install global or local tool. Local tools are added to manifest and restored.' \
        'uninstall:Uninstall a global tool or local tool.' \
        'update:Update a global or local tool.' \
        'list:List tools installed globally or locally.' \
        'run:Run a local tool. Note that this command cannot be used to run a global tool. ' \
        'search:Search dotnet tools in nuget.org' \
        'restore:Restore tools defined in the local tool manifest.' \
        'execute:Executes a tool from source without permanently installing it.' \
    )
    _describe -t commands 'testhost tool commands' commands "$@"
}

(( $+functions[_testhost__tool__install_commands] )) ||
_testhost__tool__install_commands() {
    local commands; commands=()
    _describe -t commands 'testhost tool install commands' commands "$@"
}

(( $+functions[_testhost__tool__uninstall_commands] )) ||
_testhost__tool__uninstall_commands() {
    local commands; commands=()
    _describe -t commands 'testhost tool uninstall commands' commands "$@"
}

(( $+functions[_testhost__tool__update_commands] )) ||
_testhost__tool__update_commands() {
    local commands; commands=()
    _describe -t commands 'testhost tool update commands' commands "$@"
}

(( $+functions[_testhost__tool__list_commands] )) ||
_testhost__tool__list_commands() {
    local commands; commands=()
    _describe -t commands 'testhost tool list commands' commands "$@"
}

(( $+functions[_testhost__tool__run_commands] )) ||
_testhost__tool__run_commands() {
    local commands; commands=()
    _describe -t commands 'testhost tool run commands' commands "$@"
}

(( $+functions[_testhost__tool__search_commands] )) ||
_testhost__tool__search_commands() {
    local commands; commands=()
    _describe -t commands 'testhost tool search commands' commands "$@"
}

(( $+functions[_testhost__tool__restore_commands] )) ||
_testhost__tool__restore_commands() {
    local commands; commands=()
    _describe -t commands 'testhost tool restore commands' commands "$@"
}

(( $+functions[_testhost__tool__execute_commands] )) ||
_testhost__tool__execute_commands() {
    local commands; commands=()
    _describe -t commands 'testhost tool execute commands' commands "$@"
}

(( $+functions[_testhost__vstest_commands] )) ||
_testhost__vstest_commands() {
    local commands; commands=()
    _describe -t commands 'testhost vstest commands' commands "$@"
}

(( $+functions[_testhost__help_commands] )) ||
_testhost__help_commands() {
    local commands; commands=()
    _describe -t commands 'testhost help commands' commands "$@"
}

(( $+functions[_testhost__sdk_commands] )) ||
_testhost__sdk_commands() {
    local commands; commands=(
        'check:.NET SDK Check Command' \
    )
    _describe -t commands 'testhost sdk commands' commands "$@"
}

(( $+functions[_testhost__sdk__check_commands] )) ||
_testhost__sdk__check_commands() {
    local commands; commands=()
    _describe -t commands 'testhost sdk check commands' commands "$@"
}

(( $+functions[_testhost__workload_commands] )) ||
_testhost__workload_commands() {
    local commands; commands=(
        'install:Install one or more workloads.' \
        'update:Update all installed workloads.' \
        'list:List workloads available.' \
        'search:Search for available workloads.' \
        'uninstall:Uninstall one or more workloads.' \
        'repair:Repair workload installations.' \
        'restore:Restore workloads required for a project.' \
        'clean:Removes workload components that may have been left behind from previous updates and uninstallations.' \
        'config:Modify or display workload configuration values. To display a value, specify the corresponding command-line option without providing a value.  For example\: \"dotnet workload config --update-mode\"' \
        'history:Shows a history of workload installation actions.' \
    )
    _describe -t commands 'testhost workload commands' commands "$@"
}

(( $+functions[_testhost__workload__install_commands] )) ||
_testhost__workload__install_commands() {
    local commands; commands=()
    _describe -t commands 'testhost workload install commands' commands "$@"
}

(( $+functions[_testhost__workload__update_commands] )) ||
_testhost__workload__update_commands() {
    local commands; commands=()
    _describe -t commands 'testhost workload update commands' commands "$@"
}

(( $+functions[_testhost__workload__list_commands] )) ||
_testhost__workload__list_commands() {
    local commands; commands=()
    _describe -t commands 'testhost workload list commands' commands "$@"
}

(( $+functions[_testhost__workload__search_commands] )) ||
_testhost__workload__search_commands() {
    local commands; commands=(
        'version:'\''dotnet workload search version'\'' has three functions depending on its argument\:       1. If no argument is specified, it outputs a list of the latest released workload versions from this feature band. Takes the --take option to specify how many to provide and --format to alter the format.          Example\:            dotnet workload search version --take 2 --format json            \[{\"workloadVersion\"\:\"9.0.201\"},{\"workloadVersion\"\:\"9.0.200.1\"}\]       2. If a workload version is provided as an argument, it outputs a table of various workloads and their versions for the specified workload version. Takes the --format option to alter the output format.          Example\:            dotnet workload search version 9.0.201            Workload manifest ID                               Manifest feature band      Manifest Version            ------------------------------------------------------------------------------------------------            microsoft.net.workload.emscripten.current          9.0.100-rc.1               9.0.0-rc.1.24430.3            microsoft.net.workload.emscripten.net6             9.0.100-rc.1               9.0.0-rc.1.24430.3            microsoft.net.workload.emscripten.net7             9.0.100-rc.1               9.0.0-rc.1.24430.3            microsoft.net.workload.emscripten.net8             9.0.100-rc.1               9.0.0-rc.1.24430.3            microsoft.net.sdk.android                          9.0.100-rc.1               35.0.0-rc.1.80            microsoft.net.sdk.ios                              9.0.100-rc.1               17.5.9270-net9-rc1            microsoft.net.sdk.maccatalyst                      9.0.100-rc.1               17.5.9270-net9-rc1            microsoft.net.sdk.macos                            9.0.100-rc.1               14.5.9270-net9-rc1            microsoft.net.sdk.maui                             9.0.100-rc.1               9.0.0-rc.1.24453.9            microsoft.net.sdk.tvos                             9.0.100-rc.1               17.5.9270-net9-rc1            microsoft.net.workload.mono.toolchain.current      9.0.100-rc.1               9.0.0-rc.1.24431.7            microsoft.net.workload.mono.toolchain.net6         9.0.100-rc.1               9.0.0-rc.1.24431.7            microsoft.net.workload.mono.toolchain.net7         9.0.100-rc.1               9.0.0-rc.1.24431.7            microsoft.net.workload.mono.toolchain.net8         9.0.100-rc.1               9.0.0-rc.1.24431.7       3. If one or more workloads are provided along with their versions (by joining them with the '\''@'\'' character), it outputs workload versions that match the provided versions. Takes the --take option to specify how many to provide and --format to alter the format.          Example\:            dotnet workload search version maui@9.0.0-rc.1.24453.9 ios@17.5.9270-net9-rc1            9.0.201     ' \
    )
    _describe -t commands 'testhost workload search commands' commands "$@"
}

(( $+functions[_testhost__workload__search__version_commands] )) ||
_testhost__workload__search__version_commands() {
    local commands; commands=()
    _describe -t commands 'testhost workload search version commands' commands "$@"
}

(( $+functions[_testhost__workload__uninstall_commands] )) ||
_testhost__workload__uninstall_commands() {
    local commands; commands=()
    _describe -t commands 'testhost workload uninstall commands' commands "$@"
}

(( $+functions[_testhost__workload__repair_commands] )) ||
_testhost__workload__repair_commands() {
    local commands; commands=()
    _describe -t commands 'testhost workload repair commands' commands "$@"
}

(( $+functions[_testhost__workload__restore_commands] )) ||
_testhost__workload__restore_commands() {
    local commands; commands=()
    _describe -t commands 'testhost workload restore commands' commands "$@"
}

(( $+functions[_testhost__workload__clean_commands] )) ||
_testhost__workload__clean_commands() {
    local commands; commands=()
    _describe -t commands 'testhost workload clean commands' commands "$@"
}

(( $+functions[_testhost__workload__config_commands] )) ||
_testhost__workload__config_commands() {
    local commands; commands=()
    _describe -t commands 'testhost workload config commands' commands "$@"
}

(( $+functions[_testhost__workload__history_commands] )) ||
_testhost__workload__history_commands() {
    local commands; commands=()
    _describe -t commands 'testhost workload history commands' commands "$@"
}

(( $+functions[_testhost__completions_commands] )) ||
_testhost__completions_commands() {
    local commands; commands=(
        'script:Generate the completion script for a supported shell' \
    )
    _describe -t commands 'testhost completions commands' commands "$@"
}

(( $+functions[_testhost__completions__script_commands] )) ||
_testhost__completions__script_commands() {
    local commands; commands=()
    _describe -t commands 'testhost completions script commands' commands "$@"
}

if [ "$funcstack[1]" = "_testhost" ]; then
    _testhost "$@"
else
    compdef _testhost testhost
fi
