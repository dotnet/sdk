using namespace System.Management.Automation
using namespace System.Management.Automation.Language

Register-ArgumentCompleter -Native -CommandName 'testhost' -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)

    $commandElements = $commandAst.CommandElements
    $command = @(
        'testhost'
        for ($i = 1; $i -lt $commandElements.Count; $i++) {
            $element = $commandElements[$i]
            if ($element -isnot [StringConstantExpressionAst] -or
                $element.StringConstantType -ne [StringConstantType]::BareWord -or
                $element.Value.StartsWith('-') -or
                $element.Value -eq $wordToComplete) {
                break
            }
            $element.Value
        }) -join ';'

    $completions = @()
    switch ($command) {
        'testhost' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--diagnostics', '--diagnostics', [CompletionResultType]::ParameterName, "Enable diagnostic output.")
                [CompletionResult]::new('--diagnostics', '-d', [CompletionResultType]::ParameterName, "Enable diagnostic output.")
                [CompletionResult]::new('--version', '--version', [CompletionResultType]::ParameterName, "--version")
                [CompletionResult]::new('--info', '--info', [CompletionResultType]::ParameterName, "--info")
                [CompletionResult]::new('--list-sdks', '--list-sdks', [CompletionResultType]::ParameterName, "--list-sdks")
                [CompletionResult]::new('--list-runtimes', '--list-runtimes', [CompletionResultType]::ParameterName, "--list-runtimes")
                [CompletionResult]::new('build', 'build', [CompletionResultType]::ParameterValue, ".NET Builder")
                [CompletionResult]::new('build-server', 'build-server', [CompletionResultType]::ParameterValue, "Interact with servers started from a build.")
                [CompletionResult]::new('clean', 'clean', [CompletionResultType]::ParameterValue, ".NET Clean Command")
                [CompletionResult]::new('format', 'format', [CompletionResultType]::ParameterValue, "format")
                [CompletionResult]::new('fsi', 'fsi', [CompletionResultType]::ParameterValue, "fsi")
                [CompletionResult]::new('msbuild', 'msbuild', [CompletionResultType]::ParameterValue, ".NET Builder")
                [CompletionResult]::new('new', 'new', [CompletionResultType]::ParameterValue, "Template Instantiation Commands for .NET CLI.")
                [CompletionResult]::new('nuget', 'nuget', [CompletionResultType]::ParameterValue, "nuget")
                [CompletionResult]::new('pack', 'pack', [CompletionResultType]::ParameterValue, ".NET Core NuGet Package Packer")
                [CompletionResult]::new('package', 'package', [CompletionResultType]::ParameterValue, "package")
                [CompletionResult]::new('project', 'project', [CompletionResultType]::ParameterValue, "project")
                [CompletionResult]::new('publish', 'publish', [CompletionResultType]::ParameterValue, "Publisher for the .NET Platform")
                [CompletionResult]::new('reference', 'reference', [CompletionResultType]::ParameterValue, ".NET Remove Command")
                [CompletionResult]::new('restore', 'restore', [CompletionResultType]::ParameterValue, ".NET dependency restorer")
                [CompletionResult]::new('run', 'run', [CompletionResultType]::ParameterValue, ".NET Run Command")
                [CompletionResult]::new('solution', 'solution', [CompletionResultType]::ParameterValue, ".NET modify solution file command")
                [CompletionResult]::new('solution', 'sln', [CompletionResultType]::ParameterValue, ".NET modify solution file command")
                [CompletionResult]::new('store', 'store', [CompletionResultType]::ParameterValue, "Stores the specified assemblies for the .NET Platform. By default, these will be optimized for the target runtime and framework.")
                [CompletionResult]::new('test', 'test', [CompletionResultType]::ParameterValue, ".NET Test Driver")
                [CompletionResult]::new('tool', 'tool', [CompletionResultType]::ParameterValue, "Install or work with tools that extend the .NET experience.")
                [CompletionResult]::new('vstest', 'vstest', [CompletionResultType]::ParameterValue, "vstest")
                [CompletionResult]::new('help', 'help', [CompletionResultType]::ParameterValue, ".NET CLI help utility")
                [CompletionResult]::new('sdk', 'sdk', [CompletionResultType]::ParameterValue, ".NET SDK Command")
                [CompletionResult]::new('workload', 'workload', [CompletionResultType]::ParameterValue, "Install or work with workloads that extend the .NET experience.")
                [CompletionResult]::new('completions', 'completions', [CompletionResultType]::ParameterValue, "Commands for generating and registering completions for supported shells")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;build' {
            $staticCompletions = @(
                [CompletionResult]::new('--use-current-runtime', '--use-current-runtime', [CompletionResultType]::ParameterName, "Use current runtime as the target runtime.")
                [CompletionResult]::new('--use-current-runtime', '--ucr', [CompletionResultType]::ParameterName, "Use current runtime as the target runtime.")
                [CompletionResult]::new('--framework', '--framework', [CompletionResultType]::ParameterName, "The target framework to build for. The target framework must also be specified in the project file.")
                [CompletionResult]::new('--framework', '-f', [CompletionResultType]::ParameterName, "The target framework to build for. The target framework must also be specified in the project file.")
                [CompletionResult]::new('--configuration', '--configuration', [CompletionResultType]::ParameterName, "The configuration to use for building the project. The default for most projects is `'Debug`'.")
                [CompletionResult]::new('--configuration', '-c', [CompletionResultType]::ParameterName, "The configuration to use for building the project. The default for most projects is `'Debug`'.")
                [CompletionResult]::new('--runtime', '--runtime', [CompletionResultType]::ParameterName, "--runtime")
                [CompletionResult]::new('--runtime', '-r', [CompletionResultType]::ParameterName, "--runtime")
                [CompletionResult]::new('--version-suffix', '--version-suffix', [CompletionResultType]::ParameterName, "Set the value of the `$(VersionSuffix) property to use when building the project.")
                [CompletionResult]::new('--no-restore', '--no-restore', [CompletionResultType]::ParameterName, "Do not restore the project before building.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--debug', '--debug', [CompletionResultType]::ParameterName, "--debug")
                [CompletionResult]::new('--output', '--output', [CompletionResultType]::ParameterName, "The output directory to place built artifacts in.")
                [CompletionResult]::new('--output', '-o', [CompletionResultType]::ParameterName, "The output directory to place built artifacts in.")
                [CompletionResult]::new('--artifacts-path', '--artifacts-path', [CompletionResultType]::ParameterName, "The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.")
                [CompletionResult]::new('--no-incremental', '--no-incremental', [CompletionResultType]::ParameterName, "Do not use incremental building.")
                [CompletionResult]::new('--no-dependencies', '--no-dependencies', [CompletionResultType]::ParameterName, "Do not build project-to-project references and only build the specified project.")
                [CompletionResult]::new('--nologo', '--nologo', [CompletionResultType]::ParameterName, "Do not display the startup banner or the copyright message.")
                [CompletionResult]::new('--self-contained', '--self-contained', [CompletionResultType]::ParameterName, "Publish the .NET runtime with your application so the runtime doesn`'t need to be installed on the target machine. The default is `'false.`' However, when targeting .NET 7 or lower, the default is `'true`' if a runtime identifier is specified.")
                [CompletionResult]::new('--self-contained', '--sc', [CompletionResultType]::ParameterName, "Publish the .NET runtime with your application so the runtime doesn`'t need to be installed on the target machine. The default is `'false.`' However, when targeting .NET 7 or lower, the default is `'true`' if a runtime identifier is specified.")
                [CompletionResult]::new('--no-self-contained', '--no-self-contained', [CompletionResultType]::ParameterName, "Publish your application as a framework dependent application. A compatible .NET runtime must be installed on the target machine to run your application.")
                [CompletionResult]::new('--arch', '--arch', [CompletionResultType]::ParameterName, "The target architecture.")
                [CompletionResult]::new('--arch', '-a', [CompletionResultType]::ParameterName, "The target architecture.")
                [CompletionResult]::new('--os', '--os', [CompletionResultType]::ParameterName, "The target operating system.")
                [CompletionResult]::new('--disable-build-servers', '--disable-build-servers', [CompletionResultType]::ParameterName, "Force the command to ignore any persistent build servers.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;build-server' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('shutdown', 'shutdown', [CompletionResultType]::ParameterValue, "Shuts down build servers that are started from dotnet. By default, all servers are shut down.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;build-server;shutdown' {
            $staticCompletions = @(
                [CompletionResult]::new('--msbuild', '--msbuild', [CompletionResultType]::ParameterName, "Shut down the MSBuild build server.")
                [CompletionResult]::new('--vbcscompiler', '--vbcscompiler', [CompletionResultType]::ParameterName, "Shut down the VB/C# compiler build server.")
                [CompletionResult]::new('--razor', '--razor', [CompletionResultType]::ParameterName, "Shut down the Razor build server.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;clean' {
            $staticCompletions = @(
                [CompletionResult]::new('--framework', '--framework', [CompletionResultType]::ParameterName, "The target framework to clean for. The target framework must also be specified in the project file.")
                [CompletionResult]::new('--framework', '-f', [CompletionResultType]::ParameterName, "The target framework to clean for. The target framework must also be specified in the project file.")
                [CompletionResult]::new('--runtime', '--runtime', [CompletionResultType]::ParameterName, "--runtime")
                [CompletionResult]::new('--runtime', '-r', [CompletionResultType]::ParameterName, "--runtime")
                [CompletionResult]::new('--configuration', '--configuration', [CompletionResultType]::ParameterName, "The configuration to clean for. The default for most projects is `'Debug`'.")
                [CompletionResult]::new('--configuration', '-c', [CompletionResultType]::ParameterName, "The configuration to clean for. The default for most projects is `'Debug`'.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--output', '--output', [CompletionResultType]::ParameterName, "The directory containing the build artifacts to clean.")
                [CompletionResult]::new('--output', '-o', [CompletionResultType]::ParameterName, "The directory containing the build artifacts to clean.")
                [CompletionResult]::new('--artifacts-path', '--artifacts-path', [CompletionResultType]::ParameterName, "The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.")
                [CompletionResult]::new('--nologo', '--nologo', [CompletionResultType]::ParameterName, "Do not display the startup banner or the copyright message.")
                [CompletionResult]::new('--disable-build-servers', '--disable-build-servers', [CompletionResultType]::ParameterName, "Force the command to ignore any persistent build servers.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;format' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;fsi' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;msbuild' {
            $staticCompletions = @(
                [CompletionResult]::new('--disable-build-servers', '--disable-build-servers', [CompletionResultType]::ParameterName, "Force the command to ignore any persistent build servers.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;new' {
            $staticCompletions = @(
                [CompletionResult]::new('--output', '--output', [CompletionResultType]::ParameterName, "Location to place the generated output.")
                [CompletionResult]::new('--output', '-o', [CompletionResultType]::ParameterName, "Location to place the generated output.")
                [CompletionResult]::new('--name', '--name', [CompletionResultType]::ParameterName, "The name for the output being created. If no name is specified, the name of the output directory is used.")
                [CompletionResult]::new('--name', '-n', [CompletionResultType]::ParameterName, "The name for the output being created. If no name is specified, the name of the output directory is used.")
                [CompletionResult]::new('--dry-run', '--dry-run', [CompletionResultType]::ParameterName, "Displays a summary of what would happen if the given command line were run if it would result in a template creation.")
                [CompletionResult]::new('--force', '--force', [CompletionResultType]::ParameterName, "Forces content to be generated even if it would change existing files.")
                [CompletionResult]::new('--no-update-check', '--no-update-check', [CompletionResultType]::ParameterName, "Disables checking for the template package updates when instantiating a template.")
                [CompletionResult]::new('--project', '--project', [CompletionResultType]::ParameterName, "The project that should be used for context evaluation.")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--diagnostics', '--diagnostics', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--diagnostics', '-d', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('create', 'create', [CompletionResultType]::ParameterValue, "Instantiates a template with given short name. An alias of `'dotnet new <template name>`'.")
                [CompletionResult]::new('install', 'install', [CompletionResultType]::ParameterValue, "Installs a template package.")
                [CompletionResult]::new('uninstall', 'uninstall', [CompletionResultType]::ParameterValue, "Uninstalls a template package.")
                [CompletionResult]::new('update', 'update', [CompletionResultType]::ParameterValue, "Checks the currently installed template packages for update, and install the updates.")
                [CompletionResult]::new('search', 'search', [CompletionResultType]::ParameterValue, "Searches for the templates on NuGet.org.")
                [CompletionResult]::new('list', 'list', [CompletionResultType]::ParameterValue, "Lists templates containing the specified template name. If no name is specified, lists all templates.")
                [CompletionResult]::new('details', 'details', [CompletionResultType]::ParameterValue, "       Provides the details for specified template package.       The command checks if the package is installed locally, if it was not found, it searches the configured NuGet feeds.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;new;create' {
            $staticCompletions = @(
                [CompletionResult]::new('--output', '--output', [CompletionResultType]::ParameterName, "Location to place the generated output.")
                [CompletionResult]::new('--output', '-o', [CompletionResultType]::ParameterName, "Location to place the generated output.")
                [CompletionResult]::new('--name', '--name', [CompletionResultType]::ParameterName, "The name for the output being created. If no name is specified, the name of the output directory is used.")
                [CompletionResult]::new('--name', '-n', [CompletionResultType]::ParameterName, "The name for the output being created. If no name is specified, the name of the output directory is used.")
                [CompletionResult]::new('--dry-run', '--dry-run', [CompletionResultType]::ParameterName, "Displays a summary of what would happen if the given command line were run if it would result in a template creation.")
                [CompletionResult]::new('--force', '--force', [CompletionResultType]::ParameterName, "Forces content to be generated even if it would change existing files.")
                [CompletionResult]::new('--no-update-check', '--no-update-check', [CompletionResultType]::ParameterName, "Disables checking for the template package updates when instantiating a template.")
                [CompletionResult]::new('--project', '--project', [CompletionResultType]::ParameterName, "The project that should be used for context evaluation.")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--diagnostics', '--diagnostics', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--diagnostics', '-d', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;new;install' {
            $staticCompletions = @(
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--add-source', '--add-source', [CompletionResultType]::ParameterName, "Specifies a NuGet source to use.")
                [CompletionResult]::new('--add-source', '--nuget-source', [CompletionResultType]::ParameterName, "Specifies a NuGet source to use.")
                [CompletionResult]::new('--force', '--force', [CompletionResultType]::ParameterName, "Allows installing template packages from the specified sources even if they would override a template package from another source.")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--diagnostics', '--diagnostics', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--diagnostics', '-d', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;new;uninstall' {
            $staticCompletions = @(
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--diagnostics', '--diagnostics', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--diagnostics', '-d', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;new;update' {
            $staticCompletions = @(
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--add-source', '--add-source', [CompletionResultType]::ParameterName, "Specifies a NuGet source to use.")
                [CompletionResult]::new('--add-source', '--nuget-source', [CompletionResultType]::ParameterName, "Specifies a NuGet source to use.")
                [CompletionResult]::new('--check-only', '--check-only', [CompletionResultType]::ParameterName, "Only checks for updates and display the template packages to be updated without applying update.")
                [CompletionResult]::new('--check-only', '--dry-run', [CompletionResultType]::ParameterName, "Only checks for updates and display the template packages to be updated without applying update.")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--diagnostics', '--diagnostics', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--diagnostics', '-d', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;new;search' {
            $staticCompletions = @(
                [CompletionResult]::new('--author', '--author', [CompletionResultType]::ParameterName, "Filters the templates based on the template author.")
                [CompletionResult]::new('--language', '--language', [CompletionResultType]::ParameterName, "Filters templates based on language.")
                [CompletionResult]::new('--language', '-lang', [CompletionResultType]::ParameterName, "Filters templates based on language.")
                [CompletionResult]::new('--type', '--type', [CompletionResultType]::ParameterName, "Filters templates based on available types. Predefined values are `"project`" and `"item`".")
                [CompletionResult]::new('--tag', '--tag', [CompletionResultType]::ParameterName, "Filters the templates based on the tag.")
                [CompletionResult]::new('--package', '--package', [CompletionResultType]::ParameterName, "Filters the templates based on NuGet package ID.")
                [CompletionResult]::new('--columns-all', '--columns-all', [CompletionResultType]::ParameterName, "Displays all columns in the output.")
                [CompletionResult]::new('--columns', '--columns', [CompletionResultType]::ParameterName, "Specifies the columns to display in the output. ")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--diagnostics', '--diagnostics', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--diagnostics', '-d', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;new;list' {
            $staticCompletions = @(
                [CompletionResult]::new('--author', '--author', [CompletionResultType]::ParameterName, "Filters the templates based on the template author.")
                [CompletionResult]::new('--language', '--language', [CompletionResultType]::ParameterName, "Filters templates based on language.")
                [CompletionResult]::new('--language', '-lang', [CompletionResultType]::ParameterName, "Filters templates based on language.")
                [CompletionResult]::new('--type', '--type', [CompletionResultType]::ParameterName, "Filters templates based on available types. Predefined values are `"project`" and `"item`".")
                [CompletionResult]::new('--tag', '--tag', [CompletionResultType]::ParameterName, "Filters the templates based on the tag.")
                [CompletionResult]::new('--ignore-constraints', '--ignore-constraints', [CompletionResultType]::ParameterName, "Disables checking if the template meets the constraints to be run.")
                [CompletionResult]::new('--output', '--output', [CompletionResultType]::ParameterName, "Location to place the generated output.")
                [CompletionResult]::new('--output', '-o', [CompletionResultType]::ParameterName, "Location to place the generated output.")
                [CompletionResult]::new('--project', '--project', [CompletionResultType]::ParameterName, "The project that should be used for context evaluation.")
                [CompletionResult]::new('--columns-all', '--columns-all', [CompletionResultType]::ParameterName, "Displays all columns in the output.")
                [CompletionResult]::new('--columns', '--columns', [CompletionResultType]::ParameterName, "Specifies the columns to display in the output. ")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--diagnostics', '--diagnostics', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--diagnostics', '-d', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;new;details' {
            $staticCompletions = @(
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--add-source', '--add-source', [CompletionResultType]::ParameterName, "Specifies a NuGet source to use.")
                [CompletionResult]::new('--add-source', '--nuget-source', [CompletionResultType]::ParameterName, "Specifies a NuGet source to use.")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
                [CompletionResult]::new('--diagnostics', '--diagnostics', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--diagnostics', '-d', [CompletionResultType]::ParameterName, "Enables diagnostic output.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget' {
            $staticCompletions = @(
                [CompletionResult]::new('--version', '--version', [CompletionResultType]::ParameterName, "--version")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "--verbosity")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "--verbosity")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('delete', 'delete', [CompletionResultType]::ParameterValue, "delete")
                [CompletionResult]::new('locals', 'locals', [CompletionResultType]::ParameterValue, "locals")
                [CompletionResult]::new('push', 'push', [CompletionResultType]::ParameterValue, "push")
                [CompletionResult]::new('verify', 'verify', [CompletionResultType]::ParameterValue, "verify")
                [CompletionResult]::new('trust', 'trust', [CompletionResultType]::ParameterValue, "trust")
                [CompletionResult]::new('sign', 'sign', [CompletionResultType]::ParameterValue, "sign")
                [CompletionResult]::new('why', 'why', [CompletionResultType]::ParameterValue, "Shows the dependency graph for a particular package for a given project or solution.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;delete' {
            $staticCompletions = @(
                [CompletionResult]::new('--force-english-output', '--force-english-output', [CompletionResultType]::ParameterName, "--force-english-output")
                [CompletionResult]::new('--source', '--source', [CompletionResultType]::ParameterName, "--source")
                [CompletionResult]::new('--source', '-s', [CompletionResultType]::ParameterName, "--source")
                [CompletionResult]::new('--non-interactive', '--non-interactive', [CompletionResultType]::ParameterName, "--non-interactive")
                [CompletionResult]::new('--api-key', '--api-key', [CompletionResultType]::ParameterName, "--api-key")
                [CompletionResult]::new('--api-key', '-k', [CompletionResultType]::ParameterName, "--api-key")
                [CompletionResult]::new('--no-service-endpoint', '--no-service-endpoint', [CompletionResultType]::ParameterName, "--no-service-endpoint")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;locals' {
            $staticCompletions = @(
                [CompletionResult]::new('--force-english-output', '--force-english-output', [CompletionResultType]::ParameterName, "--force-english-output")
                [CompletionResult]::new('--clear', '--clear', [CompletionResultType]::ParameterName, "--clear")
                [CompletionResult]::new('--clear', '-c', [CompletionResultType]::ParameterName, "--clear")
                [CompletionResult]::new('--list', '--list', [CompletionResultType]::ParameterName, "--list")
                [CompletionResult]::new('--list', '-l', [CompletionResultType]::ParameterName, "--list")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('all', 'all', [CompletionResultType]::ParameterValue, "all")
                [CompletionResult]::new('global-packages', 'global-packages', [CompletionResultType]::ParameterValue, "global-packages")
                [CompletionResult]::new('http-cache', 'http-cache', [CompletionResultType]::ParameterValue, "http-cache")
                [CompletionResult]::new('plugins-cache', 'plugins-cache', [CompletionResultType]::ParameterValue, "plugins-cache")
                [CompletionResult]::new('temp', 'temp', [CompletionResultType]::ParameterValue, "temp")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;push' {
            $staticCompletions = @(
                [CompletionResult]::new('--force-english-output', '--force-english-output', [CompletionResultType]::ParameterName, "--force-english-output")
                [CompletionResult]::new('--source', '--source', [CompletionResultType]::ParameterName, "--source")
                [CompletionResult]::new('--source', '-s', [CompletionResultType]::ParameterName, "--source")
                [CompletionResult]::new('--symbol-source', '--symbol-source', [CompletionResultType]::ParameterName, "--symbol-source")
                [CompletionResult]::new('--symbol-source', '-ss', [CompletionResultType]::ParameterName, "--symbol-source")
                [CompletionResult]::new('--timeout', '--timeout', [CompletionResultType]::ParameterName, "--timeout")
                [CompletionResult]::new('--timeout', '-t', [CompletionResultType]::ParameterName, "--timeout")
                [CompletionResult]::new('--api-key', '--api-key', [CompletionResultType]::ParameterName, "--api-key")
                [CompletionResult]::new('--api-key', '-k', [CompletionResultType]::ParameterName, "--api-key")
                [CompletionResult]::new('--symbol-api-key', '--symbol-api-key', [CompletionResultType]::ParameterName, "--symbol-api-key")
                [CompletionResult]::new('--symbol-api-key', '-sk', [CompletionResultType]::ParameterName, "--symbol-api-key")
                [CompletionResult]::new('--disable-buffering', '--disable-buffering', [CompletionResultType]::ParameterName, "--disable-buffering")
                [CompletionResult]::new('--disable-buffering', '-d', [CompletionResultType]::ParameterName, "--disable-buffering")
                [CompletionResult]::new('--no-symbols', '--no-symbols', [CompletionResultType]::ParameterName, "--no-symbols")
                [CompletionResult]::new('--no-symbols', '-n', [CompletionResultType]::ParameterName, "--no-symbols")
                [CompletionResult]::new('--no-service-endpoint', '--no-service-endpoint', [CompletionResultType]::ParameterName, "--no-service-endpoint")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--skip-duplicate', '--skip-duplicate', [CompletionResultType]::ParameterName, "--skip-duplicate")
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "--configfile")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;verify' {
            $staticCompletions = @(
                [CompletionResult]::new('--all', '--all', [CompletionResultType]::ParameterName, "--all")
                [CompletionResult]::new('--certificate-fingerprint', '--certificate-fingerprint', [CompletionResultType]::ParameterName, "--certificate-fingerprint")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;trust' {
            $staticCompletions = @(
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "--configfile")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('list', 'list', [CompletionResultType]::ParameterValue, "list")
                [CompletionResult]::new('author', 'author', [CompletionResultType]::ParameterValue, "author")
                [CompletionResult]::new('repository', 'repository', [CompletionResultType]::ParameterValue, "repository")
                [CompletionResult]::new('source', 'source', [CompletionResultType]::ParameterValue, "source")
                [CompletionResult]::new('certificate', 'certificate', [CompletionResultType]::ParameterValue, "certificate")
                [CompletionResult]::new('remove', 'remove', [CompletionResultType]::ParameterValue, "remove")
                [CompletionResult]::new('sync', 'sync', [CompletionResultType]::ParameterValue, "sync")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;trust;list' {
            $staticCompletions = @(
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "--configfile")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;trust;author' {
            $staticCompletions = @(
                [CompletionResult]::new('--allow-untrusted-root', '--allow-untrusted-root', [CompletionResultType]::ParameterName, "--allow-untrusted-root")
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "--configfile")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;trust;repository' {
            $staticCompletions = @(
                [CompletionResult]::new('--allow-untrusted-root', '--allow-untrusted-root', [CompletionResultType]::ParameterName, "--allow-untrusted-root")
                [CompletionResult]::new('--owners', '--owners', [CompletionResultType]::ParameterName, "--owners")
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "--configfile")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;trust;source' {
            $staticCompletions = @(
                [CompletionResult]::new('--owners', '--owners', [CompletionResultType]::ParameterName, "--owners")
                [CompletionResult]::new('--source-url', '--source-url', [CompletionResultType]::ParameterName, "--source-url")
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "--configfile")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;trust;certificate' {
            $staticCompletions = @(
                [CompletionResult]::new('--allow-untrusted-root', '--allow-untrusted-root', [CompletionResultType]::ParameterName, "--allow-untrusted-root")
                [CompletionResult]::new('--algorithm', '--algorithm', [CompletionResultType]::ParameterName, "--algorithm")
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "--configfile")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;trust;remove' {
            $staticCompletions = @(
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "--configfile")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;trust;sync' {
            $staticCompletions = @(
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "--configfile")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;sign' {
            $staticCompletions = @(
                [CompletionResult]::new('--output', '--output', [CompletionResultType]::ParameterName, "--output")
                [CompletionResult]::new('--output', '-o', [CompletionResultType]::ParameterName, "--output")
                [CompletionResult]::new('--certificate-path', '--certificate-path', [CompletionResultType]::ParameterName, "--certificate-path")
                [CompletionResult]::new('--certificate-store-name', '--certificate-store-name', [CompletionResultType]::ParameterName, "--certificate-store-name")
                [CompletionResult]::new('--certificate-store-location', '--certificate-store-location', [CompletionResultType]::ParameterName, "--certificate-store-location")
                [CompletionResult]::new('--certificate-subject-name', '--certificate-subject-name', [CompletionResultType]::ParameterName, "--certificate-subject-name")
                [CompletionResult]::new('--certificate-fingerprint', '--certificate-fingerprint', [CompletionResultType]::ParameterName, "--certificate-fingerprint")
                [CompletionResult]::new('--certificate-password', '--certificate-password', [CompletionResultType]::ParameterName, "--certificate-password")
                [CompletionResult]::new('--hash-algorithm', '--hash-algorithm', [CompletionResultType]::ParameterName, "--hash-algorithm")
                [CompletionResult]::new('--timestamper', '--timestamper', [CompletionResultType]::ParameterName, "--timestamper")
                [CompletionResult]::new('--timestamp-hash-algorithm', '--timestamp-hash-algorithm', [CompletionResultType]::ParameterName, "--timestamp-hash-algorithm")
                [CompletionResult]::new('--overwrite', '--overwrite', [CompletionResultType]::ParameterName, "--overwrite")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;nuget;why' {
            $staticCompletions = @(
                [CompletionResult]::new('--framework', '--framework', [CompletionResultType]::ParameterName, "The target framework(s) for which dependency graphs are shown.")
                [CompletionResult]::new('--framework', '-f', [CompletionResultType]::ParameterName, "The target framework(s) for which dependency graphs are shown.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show help and usage information")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show help and usage information")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;pack' {
            $staticCompletions = @(
                [CompletionResult]::new('--output', '--output', [CompletionResultType]::ParameterName, "The output directory to place built packages in.")
                [CompletionResult]::new('--output', '-o', [CompletionResultType]::ParameterName, "The output directory to place built packages in.")
                [CompletionResult]::new('--artifacts-path', '--artifacts-path', [CompletionResultType]::ParameterName, "The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.")
                [CompletionResult]::new('--no-build', '--no-build', [CompletionResultType]::ParameterName, "Do not build the project before packing. Implies --no-restore.")
                [CompletionResult]::new('--include-symbols', '--include-symbols', [CompletionResultType]::ParameterName, "Include packages with symbols in addition to regular packages in output directory.")
                [CompletionResult]::new('--include-source', '--include-source', [CompletionResultType]::ParameterName, "Include PDBs and source files. Source files go into the `'src`' folder in the resulting nuget package.")
                [CompletionResult]::new('--serviceable', '--serviceable', [CompletionResultType]::ParameterName, "Set the serviceable flag in the package. See https://aka.ms/nupkgservicing for more information.")
                [CompletionResult]::new('--serviceable', '-s', [CompletionResultType]::ParameterName, "Set the serviceable flag in the package. See https://aka.ms/nupkgservicing for more information.")
                [CompletionResult]::new('--nologo', '--nologo', [CompletionResultType]::ParameterName, "Do not display the startup banner or the copyright message.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--no-restore', '--no-restore', [CompletionResultType]::ParameterName, "Do not restore the project before building.")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--version-suffix', '--version-suffix', [CompletionResultType]::ParameterName, "Set the value of the `$(VersionSuffix) property to use when building the project.")
                [CompletionResult]::new('--configuration', '--configuration', [CompletionResultType]::ParameterName, "The configuration to use for building the package. The default is `'Release`'.")
                [CompletionResult]::new('--configuration', '-c', [CompletionResultType]::ParameterName, "The configuration to use for building the package. The default is `'Release`'.")
                [CompletionResult]::new('--disable-build-servers', '--disable-build-servers', [CompletionResultType]::ParameterName, "Force the command to ignore any persistent build servers.")
                [CompletionResult]::new('--use-current-runtime', '--use-current-runtime', [CompletionResultType]::ParameterName, "Use current runtime as the target runtime.")
                [CompletionResult]::new('--use-current-runtime', '--ucr', [CompletionResultType]::ParameterName, "Use current runtime as the target runtime.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;package' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('search', 'search', [CompletionResultType]::ParameterValue, "Searches one or more package sources for packages that match a search term. If no sources are specified, all sources defined in the NuGet.Config are used.")
                [CompletionResult]::new('add', 'add', [CompletionResultType]::ParameterValue, "Add a NuGet package reference to the project.")
                [CompletionResult]::new('list', 'list', [CompletionResultType]::ParameterValue, "List all package references of the project or solution.")
                [CompletionResult]::new('remove', 'remove', [CompletionResultType]::ParameterValue, "Remove a NuGet package reference from the project.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;package;search' {
            $staticCompletions = @(
                [CompletionResult]::new('--source', '--source', [CompletionResultType]::ParameterName, "The package source to search. You can pass multiple ``--source`` options to search multiple package sources. Example: ``--source https://api.nuget.org/v3/index.json``.")
                [CompletionResult]::new('--take', '--take', [CompletionResultType]::ParameterName, "Number of results to return. Default 20.")
                [CompletionResult]::new('--skip', '--skip', [CompletionResultType]::ParameterName, "Number of results to skip, to allow pagination. Default 0.")
                [CompletionResult]::new('--exact-match', '--exact-match', [CompletionResultType]::ParameterName, "Require that the search term exactly match the name of the package. Causes ``--take`` and ``--skip`` options to be ignored.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--prerelease', '--prerelease', [CompletionResultType]::ParameterName, "Include prerelease packages.")
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "The NuGet configuration file. If specified, only the settings from this file will be used. If not specified, the hierarchy of configuration files from the current directory will be used. For more information, see https://docs.microsoft.com/nuget/consume-packages/configuring-nuget-behavior")
                [CompletionResult]::new('--format', '--format', [CompletionResultType]::ParameterName, "Format the output accordingly. Either ``table``, or ``json``. The default value is ``table``.")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Display this amount of details in the output: ``normal``, ``minimal``, ``detailed``. The default is ``normal``")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;package;add' {
            $staticCompletions = @(
                [CompletionResult]::new('--version', '--version', [CompletionResultType]::ParameterName, "The version of the package to add.")
                [CompletionResult]::new('--version', '-v', [CompletionResultType]::ParameterName, "The version of the package to add.")
                [CompletionResult]::new('--framework', '--framework', [CompletionResultType]::ParameterName, "Add the reference only when targeting a specific framework.")
                [CompletionResult]::new('--framework', '-f', [CompletionResultType]::ParameterName, "Add the reference only when targeting a specific framework.")
                [CompletionResult]::new('--no-restore', '--no-restore', [CompletionResultType]::ParameterName, "Add the reference without performing restore preview and compatibility check.")
                [CompletionResult]::new('--no-restore', '-n', [CompletionResultType]::ParameterName, "Add the reference without performing restore preview and compatibility check.")
                [CompletionResult]::new('--source', '--source', [CompletionResultType]::ParameterName, "The NuGet package source to use during the restore.")
                [CompletionResult]::new('--source', '-s', [CompletionResultType]::ParameterName, "The NuGet package source to use during the restore.")
                [CompletionResult]::new('--package-directory', '--package-directory', [CompletionResultType]::ParameterName, "The directory to restore packages to.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--prerelease', '--prerelease', [CompletionResultType]::ParameterName, "Allows prerelease packages to be installed.")
                [CompletionResult]::new('--project', '--project', [CompletionResultType]::ParameterName, "--project")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            $text = $commandAst.ToString()
            $dotnetCompleteResults = @(dotnet complete --position $cursorPosition "$text") | Where-Object { $_ -NotMatch "^-|^/" }
            $dynamicCompletions = $dotnetCompleteResults | Foreach-Object { [CompletionResult]::new($_, $_, [CompletionResultType]::ParameterValue, $_) }
            $completions += $dynamicCompletions
            break
        }
        'testhost;package;list' {
            $staticCompletions = @(
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--outdated', '--outdated', [CompletionResultType]::ParameterName, "Lists packages that have newer versions. Cannot be combined with `'--deprecated`' or `'--vulnerable`' options.")
                [CompletionResult]::new('--deprecated', '--deprecated', [CompletionResultType]::ParameterName, "Lists packages that have been deprecated. Cannot be combined with `'--vulnerable`' or `'--outdated`' options.")
                [CompletionResult]::new('--vulnerable', '--vulnerable', [CompletionResultType]::ParameterName, "Lists packages that have known vulnerabilities. Cannot be combined with `'--deprecated`' or `'--outdated`' options.")
                [CompletionResult]::new('--framework', '--framework', [CompletionResultType]::ParameterName, "Chooses a framework to show its packages. Use the option multiple times for multiple frameworks.")
                [CompletionResult]::new('--framework', '-f', [CompletionResultType]::ParameterName, "Chooses a framework to show its packages. Use the option multiple times for multiple frameworks.")
                [CompletionResult]::new('--include-transitive', '--include-transitive', [CompletionResultType]::ParameterName, "Lists transitive and top-level packages.")
                [CompletionResult]::new('--include-prerelease', '--include-prerelease', [CompletionResultType]::ParameterName, "Consider packages with prerelease versions when searching for newer packages. Requires the `'--outdated`' option.")
                [CompletionResult]::new('--highest-patch', '--highest-patch', [CompletionResultType]::ParameterName, "Consider only the packages with a matching major and minor version numbers when searching for newer packages. Requires the `'--outdated`' option.")
                [CompletionResult]::new('--highest-minor', '--highest-minor', [CompletionResultType]::ParameterName, "Consider only the packages with a matching major version number when searching for newer packages. Requires the `'--outdated`' option.")
                [CompletionResult]::new('--config', '--config', [CompletionResultType]::ParameterName, "The path to the NuGet config file to use. Requires the `'--outdated`', `'--deprecated`' or `'--vulnerable`' option.")
                [CompletionResult]::new('--config', '--configfile', [CompletionResultType]::ParameterName, "The path to the NuGet config file to use. Requires the `'--outdated`', `'--deprecated`' or `'--vulnerable`' option.")
                [CompletionResult]::new('--source', '--source', [CompletionResultType]::ParameterName, "The NuGet sources to use when searching for newer packages. Requires the `'--outdated`', `'--deprecated`' or `'--vulnerable`' option.")
                [CompletionResult]::new('--source', '-s', [CompletionResultType]::ParameterName, "The NuGet sources to use when searching for newer packages. Requires the `'--outdated`', `'--deprecated`' or `'--vulnerable`' option.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--format', '--format', [CompletionResultType]::ParameterName, "Specifies the output format type for the list packages command.")
                [CompletionResult]::new('--output-version', '--output-version', [CompletionResultType]::ParameterName, "Specifies the version of machine-readable output. Requires the `'--format json`' option.")
                [CompletionResult]::new('--project', '--project', [CompletionResultType]::ParameterName, "--project")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;package;remove' {
            $staticCompletions = @(
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--project', '--project', [CompletionResultType]::ParameterName, "--project")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;project' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('convert', 'convert', [CompletionResultType]::ParameterValue, "Convert a file-based program to a project-based program.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;project;convert' {
            $staticCompletions = @(
                [CompletionResult]::new('--output', '--output', [CompletionResultType]::ParameterName, "Location to place the generated output.")
                [CompletionResult]::new('--output', '-o', [CompletionResultType]::ParameterName, "Location to place the generated output.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;publish' {
            $staticCompletions = @(
                [CompletionResult]::new('--use-current-runtime', '--use-current-runtime', [CompletionResultType]::ParameterName, "Use current runtime as the target runtime.")
                [CompletionResult]::new('--use-current-runtime', '--ucr', [CompletionResultType]::ParameterName, "Use current runtime as the target runtime.")
                [CompletionResult]::new('--output', '--output', [CompletionResultType]::ParameterName, "The output directory to place the published artifacts in.")
                [CompletionResult]::new('--output', '-o', [CompletionResultType]::ParameterName, "The output directory to place the published artifacts in.")
                [CompletionResult]::new('--artifacts-path', '--artifacts-path', [CompletionResultType]::ParameterName, "The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.")
                [CompletionResult]::new('--manifest', '--manifest', [CompletionResultType]::ParameterName, "The path to a target manifest file that contains the list of packages to be excluded from the publish step.")
                [CompletionResult]::new('--no-build', '--no-build', [CompletionResultType]::ParameterName, "Do not build the project before publishing. Implies --no-restore.")
                [CompletionResult]::new('--self-contained', '--self-contained', [CompletionResultType]::ParameterName, "Publish the .NET runtime with your application so the runtime doesn`'t need to be installed on the target machine. The default is `'false.`' However, when targeting .NET 7 or lower, the default is `'true`' if a runtime identifier is specified.")
                [CompletionResult]::new('--self-contained', '--sc', [CompletionResultType]::ParameterName, "Publish the .NET runtime with your application so the runtime doesn`'t need to be installed on the target machine. The default is `'false.`' However, when targeting .NET 7 or lower, the default is `'true`' if a runtime identifier is specified.")
                [CompletionResult]::new('--no-self-contained', '--no-self-contained', [CompletionResultType]::ParameterName, "Publish your application as a framework dependent application. A compatible .NET runtime must be installed on the target machine to run your application.")
                [CompletionResult]::new('--nologo', '--nologo', [CompletionResultType]::ParameterName, "Do not display the startup banner or the copyright message.")
                [CompletionResult]::new('--framework', '--framework', [CompletionResultType]::ParameterName, "The target framework to publish for. The target framework has to be specified in the project file.")
                [CompletionResult]::new('--framework', '-f', [CompletionResultType]::ParameterName, "The target framework to publish for. The target framework has to be specified in the project file.")
                [CompletionResult]::new('--runtime', '--runtime', [CompletionResultType]::ParameterName, "--runtime")
                [CompletionResult]::new('--runtime', '-r', [CompletionResultType]::ParameterName, "--runtime")
                [CompletionResult]::new('--configuration', '--configuration', [CompletionResultType]::ParameterName, "The configuration to publish for. The default is `'Release`' for NET 8.0 projects and above, but `'Debug`' for older projects.")
                [CompletionResult]::new('--configuration', '-c', [CompletionResultType]::ParameterName, "The configuration to publish for. The default is `'Release`' for NET 8.0 projects and above, but `'Debug`' for older projects.")
                [CompletionResult]::new('--version-suffix', '--version-suffix', [CompletionResultType]::ParameterName, "Set the value of the `$(VersionSuffix) property to use when building the project.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--no-restore', '--no-restore', [CompletionResultType]::ParameterName, "Do not restore the project before building.")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--arch', '--arch', [CompletionResultType]::ParameterName, "The target architecture.")
                [CompletionResult]::new('--arch', '-a', [CompletionResultType]::ParameterName, "The target architecture.")
                [CompletionResult]::new('--os', '--os', [CompletionResultType]::ParameterName, "The target operating system.")
                [CompletionResult]::new('--disable-build-servers', '--disable-build-servers', [CompletionResultType]::ParameterName, "Force the command to ignore any persistent build servers.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;reference' {
            $staticCompletions = @(
                [CompletionResult]::new('--project', '--project', [CompletionResultType]::ParameterName, "The project file to operate on. If a file is not specified, the command will search the current directory for one.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('add', 'add', [CompletionResultType]::ParameterValue, "Add a project-to-project reference to the project.")
                [CompletionResult]::new('list', 'list', [CompletionResultType]::ParameterValue, "List all project-to-project references of the project.")
                [CompletionResult]::new('remove', 'remove', [CompletionResultType]::ParameterValue, "Remove a project-to-project reference from the project.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;reference;add' {
            $staticCompletions = @(
                [CompletionResult]::new('--framework', '--framework', [CompletionResultType]::ParameterName, "Add the reference only when targeting a specific framework.")
                [CompletionResult]::new('--framework', '-f', [CompletionResultType]::ParameterName, "Add the reference only when targeting a specific framework.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--project', '--project', [CompletionResultType]::ParameterName, "The project file to operate on. If a file is not specified, the command will search the current directory for one.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;reference;list' {
            $staticCompletions = @(
                [CompletionResult]::new('--project', '--project', [CompletionResultType]::ParameterName, "The project file to operate on. If a file is not specified, the command will search the current directory for one.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;reference;remove' {
            $staticCompletions = @(
                [CompletionResult]::new('--framework', '--framework', [CompletionResultType]::ParameterName, "Remove the reference only when targeting a specific framework.")
                [CompletionResult]::new('--framework', '-f', [CompletionResultType]::ParameterName, "Remove the reference only when targeting a specific framework.")
                [CompletionResult]::new('--project', '--project', [CompletionResultType]::ParameterName, "The project file to operate on. If a file is not specified, the command will search the current directory for one.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            $text = $commandAst.ToString()
            $dotnetCompleteResults = @(dotnet complete --position $cursorPosition "$text") | Where-Object { $_ -NotMatch "^-|^/" }
            $dynamicCompletions = $dotnetCompleteResults | Foreach-Object { [CompletionResult]::new($_, $_, [CompletionResultType]::ParameterValue, $_) }
            $completions += $dynamicCompletions
            break
        }
        'testhost;restore' {
            $staticCompletions = @(
                [CompletionResult]::new('--disable-build-servers', '--disable-build-servers', [CompletionResultType]::ParameterName, "Force the command to ignore any persistent build servers.")
                [CompletionResult]::new('--source', '--source', [CompletionResultType]::ParameterName, "The NuGet package source to use for the restore.")
                [CompletionResult]::new('--source', '-s', [CompletionResultType]::ParameterName, "The NuGet package source to use for the restore.")
                [CompletionResult]::new('--packages', '--packages', [CompletionResultType]::ParameterName, "The directory to restore packages to.")
                [CompletionResult]::new('--use-current-runtime', '--use-current-runtime', [CompletionResultType]::ParameterName, "Use current runtime as the target runtime.")
                [CompletionResult]::new('--use-current-runtime', '--ucr', [CompletionResultType]::ParameterName, "Use current runtime as the target runtime.")
                [CompletionResult]::new('--disable-parallel', '--disable-parallel', [CompletionResultType]::ParameterName, "Prevent restoring multiple projects in parallel.")
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "The NuGet configuration file to use.")
                [CompletionResult]::new('--no-http-cache', '--no-http-cache', [CompletionResultType]::ParameterName, "Disable Http Caching for packages.")
                [CompletionResult]::new('--ignore-failed-sources', '--ignore-failed-sources', [CompletionResultType]::ParameterName, "Treat package source failures as warnings.")
                [CompletionResult]::new('--force', '--force', [CompletionResultType]::ParameterName, "Force all dependencies to be resolved even if the last restore was successful. This is equivalent to deleting project.assets.json.")
                [CompletionResult]::new('--force', '-f', [CompletionResultType]::ParameterName, "Force all dependencies to be resolved even if the last restore was successful. This is equivalent to deleting project.assets.json.")
                [CompletionResult]::new('--runtime', '--runtime', [CompletionResultType]::ParameterName, "The target runtime to restore packages for.")
                [CompletionResult]::new('--runtime', '-r', [CompletionResultType]::ParameterName, "The target runtime to restore packages for.")
                [CompletionResult]::new('--no-dependencies', '--no-dependencies', [CompletionResultType]::ParameterName, "Do not restore project-to-project references and only restore the specified project.")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--artifacts-path', '--artifacts-path', [CompletionResultType]::ParameterName, "The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.")
                [CompletionResult]::new('--use-lock-file', '--use-lock-file', [CompletionResultType]::ParameterName, "Enables project lock file to be generated and used with restore.")
                [CompletionResult]::new('--locked-mode', '--locked-mode', [CompletionResultType]::ParameterName, "Don`'t allow updating project lock file.")
                [CompletionResult]::new('--lock-file-path', '--lock-file-path', [CompletionResultType]::ParameterName, "Output location where project lock file is written. By default, this is `'PROJECT_ROOT\packages.lock.json`'.")
                [CompletionResult]::new('--force-evaluate', '--force-evaluate', [CompletionResultType]::ParameterName, "Forces restore to reevaluate all dependencies even if a lock file already exists.")
                [CompletionResult]::new('--arch', '--arch', [CompletionResultType]::ParameterName, "The target architecture.")
                [CompletionResult]::new('--arch', '-a', [CompletionResultType]::ParameterName, "The target architecture.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;run' {
            $staticCompletions = @(
                [CompletionResult]::new('--configuration', '--configuration', [CompletionResultType]::ParameterName, "The configuration to run for. The default for most projects is `'Debug`'.")
                [CompletionResult]::new('--configuration', '-c', [CompletionResultType]::ParameterName, "The configuration to run for. The default for most projects is `'Debug`'.")
                [CompletionResult]::new('--framework', '--framework', [CompletionResultType]::ParameterName, "The target framework to run for. The target framework must also be specified in the project file.")
                [CompletionResult]::new('--framework', '-f', [CompletionResultType]::ParameterName, "The target framework to run for. The target framework must also be specified in the project file.")
                [CompletionResult]::new('--runtime', '--runtime', [CompletionResultType]::ParameterName, "--runtime")
                [CompletionResult]::new('--runtime', '-r', [CompletionResultType]::ParameterName, "--runtime")
                [CompletionResult]::new('--project', '--project', [CompletionResultType]::ParameterName, "The path to the project file to run (defaults to the current directory if there is only one project).")
                [CompletionResult]::new('--launch-profile', '--launch-profile', [CompletionResultType]::ParameterName, "The name of the launch profile (if any) to use when launching the application.")
                [CompletionResult]::new('--launch-profile', '-lp', [CompletionResultType]::ParameterName, "The name of the launch profile (if any) to use when launching the application.")
                [CompletionResult]::new('--no-launch-profile', '--no-launch-profile', [CompletionResultType]::ParameterName, "Do not attempt to use launchSettings.json to configure the application.")
                [CompletionResult]::new('--no-build', '--no-build', [CompletionResultType]::ParameterName, "Do not build the project before running. Implies --no-restore.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--no-restore', '--no-restore', [CompletionResultType]::ParameterName, "Do not restore the project before building.")
                [CompletionResult]::new('--self-contained', '--self-contained', [CompletionResultType]::ParameterName, "Publish the .NET runtime with your application so the runtime doesn`'t need to be installed on the target machine. The default is `'false.`' However, when targeting .NET 7 or lower, the default is `'true`' if a runtime identifier is specified.")
                [CompletionResult]::new('--self-contained', '--sc', [CompletionResultType]::ParameterName, "Publish the .NET runtime with your application so the runtime doesn`'t need to be installed on the target machine. The default is `'false.`' However, when targeting .NET 7 or lower, the default is `'true`' if a runtime identifier is specified.")
                [CompletionResult]::new('--no-self-contained', '--no-self-contained', [CompletionResultType]::ParameterName, "Publish your application as a framework dependent application. A compatible .NET runtime must be installed on the target machine to run your application.")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--arch', '--arch', [CompletionResultType]::ParameterName, "The target architecture.")
                [CompletionResult]::new('--arch', '-a', [CompletionResultType]::ParameterName, "The target architecture.")
                [CompletionResult]::new('--os', '--os', [CompletionResultType]::ParameterName, "The target operating system.")
                [CompletionResult]::new('--disable-build-servers', '--disable-build-servers', [CompletionResultType]::ParameterName, "Force the command to ignore any persistent build servers.")
                [CompletionResult]::new('--artifacts-path', '--artifacts-path', [CompletionResultType]::ParameterName, "The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.")
                [CompletionResult]::new('--environment', '--environment', [CompletionResultType]::ParameterName, "Sets the value of an environment variable.  Creates the variable if it does not exist, overrides if it does.  This will force the tests to be run in an isolated process.  This argument can be specified multiple times to provide multiple variables.  Examples: -e VARIABLE=abc -e VARIABLE=`"value with spaces`" -e VARIABLE=`"value;seperated with;semicolons`" -e VAR1=abc -e VAR2=def -e VAR3=ghi ")
                [CompletionResult]::new('--environment', '-e', [CompletionResultType]::ParameterName, "Sets the value of an environment variable.  Creates the variable if it does not exist, overrides if it does.  This will force the tests to be run in an isolated process.  This argument can be specified multiple times to provide multiple variables.  Examples: -e VARIABLE=abc -e VARIABLE=`"value with spaces`" -e VARIABLE=`"value;seperated with;semicolons`" -e VAR1=abc -e VAR2=def -e VAR3=ghi ")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;solution' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('add', 'add', [CompletionResultType]::ParameterValue, "Add one or more projects to a solution file.")
                [CompletionResult]::new('list', 'list', [CompletionResultType]::ParameterValue, "List all projects in a solution file.")
                [CompletionResult]::new('remove', 'remove', [CompletionResultType]::ParameterValue, "Remove one or more projects from a solution file.")
                [CompletionResult]::new('migrate', 'migrate', [CompletionResultType]::ParameterValue, "Generate a .slnx file from a .sln file.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;solution;add' {
            $staticCompletions = @(
                [CompletionResult]::new('--in-root', '--in-root', [CompletionResultType]::ParameterName, "Place project in root of the solution, rather than creating a solution folder.")
                [CompletionResult]::new('--solution-folder', '--solution-folder', [CompletionResultType]::ParameterName, "The destination solution folder path to add the projects to.")
                [CompletionResult]::new('--solution-folder', '-s', [CompletionResultType]::ParameterName, "The destination solution folder path to add the projects to.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;solution;list' {
            $staticCompletions = @(
                [CompletionResult]::new('--solution-folders', '--solution-folders', [CompletionResultType]::ParameterName, "Display solution folder paths.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;solution;remove' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;solution;migrate' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;store' {
            $staticCompletions = @(
                [CompletionResult]::new('--manifest', '--manifest', [CompletionResultType]::ParameterName, "The XML file that contains the list of packages to be stored.")
                [CompletionResult]::new('--manifest', '-m', [CompletionResultType]::ParameterName, "The XML file that contains the list of packages to be stored.")
                [CompletionResult]::new('--framework-version', '--framework-version', [CompletionResultType]::ParameterName, "The Microsoft.NETCore.App package version that will be used to run the assemblies.")
                [CompletionResult]::new('--output', '--output', [CompletionResultType]::ParameterName, "The output directory to store the given assemblies in.")
                [CompletionResult]::new('--output', '-o', [CompletionResultType]::ParameterName, "The output directory to store the given assemblies in.")
                [CompletionResult]::new('--working-dir', '--working-dir', [CompletionResultType]::ParameterName, "The working directory used by the command to execute.")
                [CompletionResult]::new('--working-dir', '-w', [CompletionResultType]::ParameterName, "The working directory used by the command to execute.")
                [CompletionResult]::new('--skip-optimization', '--skip-optimization', [CompletionResultType]::ParameterName, "Skip the optimization phase.")
                [CompletionResult]::new('--skip-symbols', '--skip-symbols', [CompletionResultType]::ParameterName, "Skip creating symbol files which can be used for profiling the optimized assemblies.")
                [CompletionResult]::new('--framework', '--framework', [CompletionResultType]::ParameterName, "The target framework to store packages for. The target framework has to be specified in the project file.")
                [CompletionResult]::new('--framework', '-f', [CompletionResultType]::ParameterName, "The target framework to store packages for. The target framework has to be specified in the project file.")
                [CompletionResult]::new('--runtime', '--runtime', [CompletionResultType]::ParameterName, "--runtime")
                [CompletionResult]::new('--runtime', '-r', [CompletionResultType]::ParameterName, "--runtime")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--use-current-runtime', '--use-current-runtime', [CompletionResultType]::ParameterName, "Use current runtime as the target runtime.")
                [CompletionResult]::new('--use-current-runtime', '--ucr', [CompletionResultType]::ParameterName, "Use current runtime as the target runtime.")
                [CompletionResult]::new('--disable-build-servers', '--disable-build-servers', [CompletionResultType]::ParameterName, "Force the command to ignore any persistent build servers.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;test' {
            $staticCompletions = @(
                [CompletionResult]::new('--settings', '--settings', [CompletionResultType]::ParameterName, "The settings file to use when running tests.")
                [CompletionResult]::new('--settings', '-s', [CompletionResultType]::ParameterName, "The settings file to use when running tests.")
                [CompletionResult]::new('--list-tests', '--list-tests', [CompletionResultType]::ParameterName, "List the discovered tests instead of running the tests.")
                [CompletionResult]::new('--list-tests', '-t', [CompletionResultType]::ParameterName, "List the discovered tests instead of running the tests.")
                [CompletionResult]::new('--environment', '--environment', [CompletionResultType]::ParameterName, "Sets the value of an environment variable.  Creates the variable if it does not exist, overrides if it does.  This will force the tests to be run in an isolated process.  This argument can be specified multiple times to provide multiple variables.  Examples: -e VARIABLE=abc -e VARIABLE=`"value with spaces`" -e VARIABLE=`"value;seperated with;semicolons`" -e VAR1=abc -e VAR2=def -e VAR3=ghi ")
                [CompletionResult]::new('--environment', '-e', [CompletionResultType]::ParameterName, "Sets the value of an environment variable.  Creates the variable if it does not exist, overrides if it does.  This will force the tests to be run in an isolated process.  This argument can be specified multiple times to provide multiple variables.  Examples: -e VARIABLE=abc -e VARIABLE=`"value with spaces`" -e VARIABLE=`"value;seperated with;semicolons`" -e VAR1=abc -e VAR2=def -e VAR3=ghi ")
                [CompletionResult]::new('--filter', '--filter', [CompletionResultType]::ParameterName, "Run tests that match the given expression.                                         Examples:                                         Run tests with priority set to 1: --filter `"Priority = 1`"                                         Run a test with the specified full name: --filter `"FullyQualifiedName=Namespace.ClassName.MethodName`"                                         Run tests that contain the specified name: --filter `"FullyQualifiedName~Namespace.Class`"                                         See https://aka.ms/vstest-filtering for more information on filtering support.                                         ")
                [CompletionResult]::new('--test-adapter-path', '--test-adapter-path', [CompletionResultType]::ParameterName, "The path to the custom adapters to use for the test run.")
                [CompletionResult]::new('--logger', '--logger', [CompletionResultType]::ParameterName, "The logger to use for test results.                                         Examples:                                         Log in trx format using a unique file name: --logger trx                                         Log in trx format using the specified file name: --logger `"trx;LogFileName=<TestResults.trx>`"                                         See https://aka.ms/vstest-report for more information on logger arguments.")
                [CompletionResult]::new('--logger', '-l', [CompletionResultType]::ParameterName, "The logger to use for test results.                                         Examples:                                         Log in trx format using a unique file name: --logger trx                                         Log in trx format using the specified file name: --logger `"trx;LogFileName=<TestResults.trx>`"                                         See https://aka.ms/vstest-report for more information on logger arguments.")
                [CompletionResult]::new('--output', '--output', [CompletionResultType]::ParameterName, "The output directory to place built artifacts in.")
                [CompletionResult]::new('--output', '-o', [CompletionResultType]::ParameterName, "The output directory to place built artifacts in.")
                [CompletionResult]::new('--artifacts-path', '--artifacts-path', [CompletionResultType]::ParameterName, "The artifacts path. All output from the project, including build, publish, and pack output, will go in subfolders under the specified path.")
                [CompletionResult]::new('--diag', '--diag', [CompletionResultType]::ParameterName, "Enable verbose logging to the specified file.")
                [CompletionResult]::new('--diag', '-d', [CompletionResultType]::ParameterName, "Enable verbose logging to the specified file.")
                [CompletionResult]::new('--no-build', '--no-build', [CompletionResultType]::ParameterName, "Do not build the project before testing. Implies --no-restore.")
                [CompletionResult]::new('--results-directory', '--results-directory', [CompletionResultType]::ParameterName, "The directory where the test results will be placed. The specified directory will be created if it does not exist.")
                [CompletionResult]::new('--collect', '--collect', [CompletionResultType]::ParameterName, "The friendly name of the data collector to use for the test run.                                         More info here: https://aka.ms/vstest-collect")
                [CompletionResult]::new('--blame', '--blame', [CompletionResultType]::ParameterName, "Runs the tests in blame mode. This option is helpful in isolating problematic tests that cause the test host to crash or hang, but it does not create a memory dump by default.   When a crash is detected, it creates an sequence file in TestResults/guid/guid_Sequence.xml that captures the order of tests that were run before the crash.  Based on the additional settings, hang dump or crash dump can also be collected.  Example:    Timeout the test run when test takes more than the default timeout of 1 hour, and collect crash dump when the test host exits unexpectedly.    (Crash dumps require additional setup, see below.)   dotnet test --blame-hang --blame-crash Example:    Timeout the test run when a test takes more than 20 minutes and collect hang dump.    dotnet test --blame-hang-timeout 20min ")
                [CompletionResult]::new('--blame-crash', '--blame-crash', [CompletionResultType]::ParameterName, "Runs the tests in blame mode and collects a crash dump when the test host exits unexpectedly. This option depends on the version of .NET used, the type of error, and the operating system.    For exceptions in managed code, a dump will be automatically collected on .NET 5.0 and later versions. It will generate a dump for testhost or any child process that also ran on .NET 5.0 and crashed. Crashes in native code will not generate a dump. This option works on Windows, macOS, and Linux.  Crash dumps in native code, or when targetting .NET Framework, or .NET Core 3.1 and earlier versions, can only be collected on Windows, by using Procdump. A directory that contains procdump.exe and procdump64.exe must be in the PATH or PROCDUMP_PATH environment variable.  The tools can be downloaded here: https://docs.microsoft.com/sysinternals/downloads/procdump    To collect a crash dump from a native application running on .NET 5.0 or later, the usage of Procdump can be forced by setting the VSTEST_DUMP_FORCEPROCDUMP environment variable to 1.  Implies --blame.")
                [CompletionResult]::new('--blame-crash-dump-type', '--blame-crash-dump-type', [CompletionResultType]::ParameterName, "The type of crash dump to be collected. Supported values are full (default) and mini. Implies --blame-crash.")
                [CompletionResult]::new('--blame-crash-collect-always', '--blame-crash-collect-always', [CompletionResultType]::ParameterName, "Enables collecting crash dump on expected as well as unexpected testhost exit.")
                [CompletionResult]::new('--blame-hang', '--blame-hang', [CompletionResultType]::ParameterName, "Run the tests in blame mode and enables collecting hang dump when test exceeds the given timeout.")
                [CompletionResult]::new('--blame-hang-dump-type', '--blame-hang-dump-type', [CompletionResultType]::ParameterName, "The type of crash dump to be collected. The supported values are full (default), mini, and none. When `'none`' is used then test host is terminated on timeout, but no dump is collected. Implies --blame-hang.")
                [CompletionResult]::new('--blame-hang-timeout', '--blame-hang-timeout', [CompletionResultType]::ParameterName, "Per-test timeout, after which hang dump is triggered and the testhost process is terminated. Default is 1h. The timeout value is specified in the following format: 1.5h / 90m / 5400s / 5400000ms. When no unit is used (e.g. 5400000), the value is assumed to be in milliseconds. When used together with data driven tests, the timeout behavior depends on the test adapter used. For xUnit, NUnit and MSTest 2.2.4+ the timeout is renewed after every test case, For MSTest before 2.2.4, the timeout is used for all testcases.")
                [CompletionResult]::new('--nologo', '--nologo', [CompletionResultType]::ParameterName, "Run test(s), without displaying Microsoft Testplatform banner")
                [CompletionResult]::new('--configuration', '--configuration', [CompletionResultType]::ParameterName, "The configuration to use for running tests. The default for most projects is `'Debug`'.")
                [CompletionResult]::new('--configuration', '-c', [CompletionResultType]::ParameterName, "The configuration to use for running tests. The default for most projects is `'Debug`'.")
                [CompletionResult]::new('--framework', '--framework', [CompletionResultType]::ParameterName, "The target framework to run tests for. The target framework must also be specified in the project file.")
                [CompletionResult]::new('--framework', '-f', [CompletionResultType]::ParameterName, "The target framework to run tests for. The target framework must also be specified in the project file.")
                [CompletionResult]::new('--runtime', '--runtime', [CompletionResultType]::ParameterName, "--runtime")
                [CompletionResult]::new('--runtime', '-r', [CompletionResultType]::ParameterName, "--runtime")
                [CompletionResult]::new('--no-restore', '--no-restore', [CompletionResultType]::ParameterName, "Do not restore the project before building.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--arch', '--arch', [CompletionResultType]::ParameterName, "The target architecture.")
                [CompletionResult]::new('--arch', '-a', [CompletionResultType]::ParameterName, "The target architecture.")
                [CompletionResult]::new('--os', '--os', [CompletionResultType]::ParameterName, "The target operating system.")
                [CompletionResult]::new('--disable-build-servers', '--disable-build-servers', [CompletionResultType]::ParameterName, "Force the command to ignore any persistent build servers.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;tool' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('install', 'install', [CompletionResultType]::ParameterValue, "Install global or local tool. Local tools are added to manifest and restored.")
                [CompletionResult]::new('uninstall', 'uninstall', [CompletionResultType]::ParameterValue, "Uninstall a global tool or local tool.")
                [CompletionResult]::new('update', 'update', [CompletionResultType]::ParameterValue, "Update a global or local tool.")
                [CompletionResult]::new('list', 'list', [CompletionResultType]::ParameterValue, "List tools installed globally or locally.")
                [CompletionResult]::new('run', 'run', [CompletionResultType]::ParameterValue, "Run a local tool. Note that this command cannot be used to run a global tool. ")
                [CompletionResult]::new('search', 'search', [CompletionResultType]::ParameterValue, "Search dotnet tools in nuget.org")
                [CompletionResult]::new('restore', 'restore', [CompletionResultType]::ParameterValue, "Restore tools defined in the local tool manifest.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;tool;install' {
            $staticCompletions = @(
                [CompletionResult]::new('--global', '--global', [CompletionResultType]::ParameterName, "--global")
                [CompletionResult]::new('--global', '-g', [CompletionResultType]::ParameterName, "--global")
                [CompletionResult]::new('--local', '--local', [CompletionResultType]::ParameterName, "--local")
                [CompletionResult]::new('--tool-path', '--tool-path', [CompletionResultType]::ParameterName, "--tool-path")
                [CompletionResult]::new('--version', '--version', [CompletionResultType]::ParameterName, "The version of the tool package to install.")
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "The NuGet configuration file to use.")
                [CompletionResult]::new('--tool-manifest', '--tool-manifest', [CompletionResultType]::ParameterName, "--tool-manifest")
                [CompletionResult]::new('--add-source', '--add-source', [CompletionResultType]::ParameterName, "Add an additional NuGet package source to use during installation.")
                [CompletionResult]::new('--source', '--source', [CompletionResultType]::ParameterName, "Replace all NuGet package sources to use during installation with these.")
                [CompletionResult]::new('--framework', '--framework', [CompletionResultType]::ParameterName, "The target framework to install the tool for.")
                [CompletionResult]::new('--prerelease', '--prerelease', [CompletionResultType]::ParameterName, "Include pre-release packages.")
                [CompletionResult]::new('--disable-parallel', '--disable-parallel', [CompletionResultType]::ParameterName, "Prevent restoring multiple projects in parallel.")
                [CompletionResult]::new('--ignore-failed-sources', '--ignore-failed-sources', [CompletionResultType]::ParameterName, "Treat package source failures as warnings.")
                [CompletionResult]::new('--no-http-cache', '--no-http-cache', [CompletionResultType]::ParameterName, "Do not cache packages and http requests.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--arch', '--arch', [CompletionResultType]::ParameterName, "The target architecture.")
                [CompletionResult]::new('--arch', '-a', [CompletionResultType]::ParameterName, "The target architecture.")
                [CompletionResult]::new('--create-manifest-if-needed', '--create-manifest-if-needed', [CompletionResultType]::ParameterName, "Create a tool manifest if one isn`'t found during tool installation. For information on how manifests are located, see https://aka.ms/dotnet/tools/create-manifest-if-needed")
                [CompletionResult]::new('--allow-downgrade', '--allow-downgrade', [CompletionResultType]::ParameterName, "Allow package downgrade when installing a .NET tool package.")
                [CompletionResult]::new('--allow-roll-forward', '--allow-roll-forward', [CompletionResultType]::ParameterName, "Allow a .NET tool to roll forward to newer versions of the .NET runtime if the runtime it targets isn`'t installed.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;tool;uninstall' {
            $staticCompletions = @(
                [CompletionResult]::new('--global', '--global', [CompletionResultType]::ParameterName, "--global")
                [CompletionResult]::new('--global', '-g', [CompletionResultType]::ParameterName, "--global")
                [CompletionResult]::new('--local', '--local', [CompletionResultType]::ParameterName, "--local")
                [CompletionResult]::new('--tool-path', '--tool-path', [CompletionResultType]::ParameterName, "--tool-path")
                [CompletionResult]::new('--tool-manifest', '--tool-manifest', [CompletionResultType]::ParameterName, "--tool-manifest")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;tool;update' {
            $staticCompletions = @(
                [CompletionResult]::new('--global', '--global', [CompletionResultType]::ParameterName, "--global")
                [CompletionResult]::new('--global', '-g', [CompletionResultType]::ParameterName, "--global")
                [CompletionResult]::new('--local', '--local', [CompletionResultType]::ParameterName, "--local")
                [CompletionResult]::new('--tool-path', '--tool-path', [CompletionResultType]::ParameterName, "--tool-path")
                [CompletionResult]::new('--version', '--version', [CompletionResultType]::ParameterName, "The version of the tool package to install.")
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "The NuGet configuration file to use.")
                [CompletionResult]::new('--tool-manifest', '--tool-manifest', [CompletionResultType]::ParameterName, "--tool-manifest")
                [CompletionResult]::new('--add-source', '--add-source', [CompletionResultType]::ParameterName, "Add an additional NuGet package source to use during installation.")
                [CompletionResult]::new('--source', '--source', [CompletionResultType]::ParameterName, "Replace all NuGet package sources to use during installation with these.")
                [CompletionResult]::new('--framework', '--framework', [CompletionResultType]::ParameterName, "The target framework to install the tool for.")
                [CompletionResult]::new('--prerelease', '--prerelease', [CompletionResultType]::ParameterName, "Include pre-release packages.")
                [CompletionResult]::new('--disable-parallel', '--disable-parallel', [CompletionResultType]::ParameterName, "Prevent restoring multiple projects in parallel.")
                [CompletionResult]::new('--ignore-failed-sources', '--ignore-failed-sources', [CompletionResultType]::ParameterName, "Treat package source failures as warnings.")
                [CompletionResult]::new('--no-http-cache', '--no-http-cache', [CompletionResultType]::ParameterName, "Do not cache packages and http requests.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--allow-downgrade', '--allow-downgrade', [CompletionResultType]::ParameterName, "Allow package downgrade when installing a .NET tool package.")
                [CompletionResult]::new('--all', '--all', [CompletionResultType]::ParameterName, "Update all tools.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;tool;list' {
            $staticCompletions = @(
                [CompletionResult]::new('--global', '--global', [CompletionResultType]::ParameterName, "--global")
                [CompletionResult]::new('--global', '-g', [CompletionResultType]::ParameterName, "--global")
                [CompletionResult]::new('--local', '--local', [CompletionResultType]::ParameterName, "--local")
                [CompletionResult]::new('--tool-path', '--tool-path', [CompletionResultType]::ParameterName, "--tool-path")
                [CompletionResult]::new('--format', '--format', [CompletionResultType]::ParameterName, "--format")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;tool;run' {
            $staticCompletions = @(
                [CompletionResult]::new('--allow-roll-forward', '--allow-roll-forward', [CompletionResultType]::ParameterName, "Allow a .NET tool to roll forward to newer versions of the .NET runtime if the runtime it targets isn`'t installed.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;tool;search' {
            $staticCompletions = @(
                [CompletionResult]::new('--detail', '--detail', [CompletionResultType]::ParameterName, "Show detail result of the query.")
                [CompletionResult]::new('--skip', '--skip', [CompletionResultType]::ParameterName, "The number of results to skip, for pagination.")
                [CompletionResult]::new('--take', '--take', [CompletionResultType]::ParameterName, "The number of results to return, for pagination.")
                [CompletionResult]::new('--prerelease', '--prerelease', [CompletionResultType]::ParameterName, "Include pre-release packages.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;tool;restore' {
            $staticCompletions = @(
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "The NuGet configuration file to use.")
                [CompletionResult]::new('--add-source', '--add-source', [CompletionResultType]::ParameterName, "Add an additional NuGet package source to use during installation.")
                [CompletionResult]::new('--tool-manifest', '--tool-manifest', [CompletionResultType]::ParameterName, "--tool-manifest")
                [CompletionResult]::new('--disable-parallel', '--disable-parallel', [CompletionResultType]::ParameterName, "Prevent restoring multiple projects in parallel.")
                [CompletionResult]::new('--ignore-failed-sources', '--ignore-failed-sources', [CompletionResultType]::ParameterName, "Treat package source failures as warnings.")
                [CompletionResult]::new('--no-http-cache', '--no-http-cache', [CompletionResultType]::ParameterName, "Do not cache packages and http requests.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;vstest' {
            $staticCompletions = @(
                [CompletionResult]::new('--Platform', '--Platform', [CompletionResultType]::ParameterName, "--Platform")
                [CompletionResult]::new('--Framework', '--Framework', [CompletionResultType]::ParameterName, "--Framework")
                [CompletionResult]::new('--logger', '--logger', [CompletionResultType]::ParameterName, "--logger")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;help' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;sdk' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('check', 'check', [CompletionResultType]::ParameterValue, ".NET SDK Check Command")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;sdk;check' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;workload' {
            $staticCompletions = @(
                [CompletionResult]::new('--info', '--info', [CompletionResultType]::ParameterName, "Display information about installed workloads.")
                [CompletionResult]::new('--version', '--version', [CompletionResultType]::ParameterName, "Display the currently installed workload version.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('install', 'install', [CompletionResultType]::ParameterValue, "Install one or more workloads.")
                [CompletionResult]::new('update', 'update', [CompletionResultType]::ParameterValue, "Update all installed workloads.")
                [CompletionResult]::new('list', 'list', [CompletionResultType]::ParameterValue, "List workloads available.")
                [CompletionResult]::new('search', 'search', [CompletionResultType]::ParameterValue, "Search for available workloads.")
                [CompletionResult]::new('uninstall', 'uninstall', [CompletionResultType]::ParameterValue, "Uninstall one or more workloads.")
                [CompletionResult]::new('repair', 'repair', [CompletionResultType]::ParameterValue, "Repair workload installations.")
                [CompletionResult]::new('restore', 'restore', [CompletionResultType]::ParameterValue, "Restore workloads required for a project.")
                [CompletionResult]::new('clean', 'clean', [CompletionResultType]::ParameterValue, "Removes workload components that may have been left behind from previous updates and uninstallations.")
                [CompletionResult]::new('config', 'config', [CompletionResultType]::ParameterValue, "Modify or display workload configuration values. To display a value, specify the corresponding command-line option without providing a value.  For example: `"dotnet workload config --update-mode`"")
                [CompletionResult]::new('history', 'history', [CompletionResultType]::ParameterValue, "Shows a history of workload installation actions.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;workload;install' {
            $staticCompletions = @(
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "The NuGet configuration file to use.")
                [CompletionResult]::new('--source', '--source', [CompletionResultType]::ParameterName, "The NuGet package source to use during the restore. To specify multiple sources, repeat the option.")
                [CompletionResult]::new('--source', '-s', [CompletionResultType]::ParameterName, "The NuGet package source to use during the restore. To specify multiple sources, repeat the option.")
                [CompletionResult]::new('--include-previews', '--include-previews', [CompletionResultType]::ParameterName, "Allow prerelease workload manifests.")
                [CompletionResult]::new('--skip-manifest-update', '--skip-manifest-update', [CompletionResultType]::ParameterName, "Skip updating the workload manifests.")
                [CompletionResult]::new('--temp-dir', '--temp-dir', [CompletionResultType]::ParameterName, "Specify a temporary directory for this command to download and extract NuGet packages (must be secure).")
                [CompletionResult]::new('--disable-parallel', '--disable-parallel', [CompletionResultType]::ParameterName, "Prevent restoring multiple projects in parallel.")
                [CompletionResult]::new('--ignore-failed-sources', '--ignore-failed-sources', [CompletionResultType]::ParameterName, "Treat package source failures as warnings.")
                [CompletionResult]::new('--no-http-cache', '--no-http-cache', [CompletionResultType]::ParameterName, "Do not cache packages and http requests.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--version', '--version', [CompletionResultType]::ParameterName, "A workload version to display or one or more workloads and their versions joined by the `'@`' character.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;workload;update' {
            $staticCompletions = @(
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "The NuGet configuration file to use.")
                [CompletionResult]::new('--source', '--source', [CompletionResultType]::ParameterName, "The NuGet package source to use during the restore. To specify multiple sources, repeat the option.")
                [CompletionResult]::new('--source', '-s', [CompletionResultType]::ParameterName, "The NuGet package source to use during the restore. To specify multiple sources, repeat the option.")
                [CompletionResult]::new('--include-previews', '--include-previews', [CompletionResultType]::ParameterName, "Allow prerelease workload manifests.")
                [CompletionResult]::new('--temp-dir', '--temp-dir', [CompletionResultType]::ParameterName, "Specify a temporary directory for this command to download and extract NuGet packages (must be secure).")
                [CompletionResult]::new('--from-previous-sdk', '--from-previous-sdk', [CompletionResultType]::ParameterName, "Include workloads installed with earlier SDK versions in update.")
                [CompletionResult]::new('--advertising-manifests-only', '--advertising-manifests-only', [CompletionResultType]::ParameterName, "Only update advertising manifests.")
                [CompletionResult]::new('--version', '--version', [CompletionResultType]::ParameterName, "A workload version to display or one or more workloads and their versions joined by the `'@`' character.")
                [CompletionResult]::new('--disable-parallel', '--disable-parallel', [CompletionResultType]::ParameterName, "Prevent restoring multiple projects in parallel.")
                [CompletionResult]::new('--ignore-failed-sources', '--ignore-failed-sources', [CompletionResultType]::ParameterName, "Treat package source failures as warnings.")
                [CompletionResult]::new('--no-http-cache', '--no-http-cache', [CompletionResultType]::ParameterName, "Do not cache packages and http requests.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--from-history', '--from-history', [CompletionResultType]::ParameterName, "Update workloads to a previous version specified by the argument. Use the `'dotnet workload history`' to see available workload history records.")
                [CompletionResult]::new('--manifests-only', '--manifests-only', [CompletionResultType]::ParameterName, "Update to the workload versions specified in the history without changing which workloads are installed. Currently installed workloads will be updated to match the specified history version.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;workload;list' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;workload;search' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('version', 'version', [CompletionResultType]::ParameterValue, "`'dotnet workload search version`' has three functions depending on its argument:       1. If no argument is specified, it outputs a list of the latest released workload versions from this feature band. Takes the --take option to specify how many to provide and --format to alter the format.          Example:            dotnet workload search version --take 2 --format json            [{`"workloadVersion`":`"9.0.201`"},{`"workloadVersion`":`"9.0.200.1`"}]       2. If a workload version is provided as an argument, it outputs a table of various workloads and their versions for the specified workload version. Takes the --format option to alter the output format.          Example:            dotnet workload search version 9.0.201            Workload manifest ID                               Manifest feature band      Manifest Version            ------------------------------------------------------------------------------------------------            microsoft.net.workload.emscripten.current          9.0.100-rc.1               9.0.0-rc.1.24430.3            microsoft.net.workload.emscripten.net6             9.0.100-rc.1               9.0.0-rc.1.24430.3            microsoft.net.workload.emscripten.net7             9.0.100-rc.1               9.0.0-rc.1.24430.3            microsoft.net.workload.emscripten.net8             9.0.100-rc.1               9.0.0-rc.1.24430.3            microsoft.net.sdk.android                          9.0.100-rc.1               35.0.0-rc.1.80            microsoft.net.sdk.ios                              9.0.100-rc.1               17.5.9270-net9-rc1            microsoft.net.sdk.maccatalyst                      9.0.100-rc.1               17.5.9270-net9-rc1            microsoft.net.sdk.macos                            9.0.100-rc.1               14.5.9270-net9-rc1            microsoft.net.sdk.maui                             9.0.100-rc.1               9.0.0-rc.1.24453.9            microsoft.net.sdk.tvos                             9.0.100-rc.1               17.5.9270-net9-rc1            microsoft.net.workload.mono.toolchain.current      9.0.100-rc.1               9.0.0-rc.1.24431.7            microsoft.net.workload.mono.toolchain.net6         9.0.100-rc.1               9.0.0-rc.1.24431.7            microsoft.net.workload.mono.toolchain.net7         9.0.100-rc.1               9.0.0-rc.1.24431.7            microsoft.net.workload.mono.toolchain.net8         9.0.100-rc.1               9.0.0-rc.1.24431.7            microsoft.net.sdk.aspire                           8.0.100                    8.2.0       3. If one or more workloads are provided along with their versions (by joining them with the `'@`' character), it outputs workload versions that match the provided versions. Takes the --take option to specify how many to provide and --format to alter the format.          Example:            dotnet workload search version maui@9.0.0-rc.1.24453.9 ios@17.5.9270-net9-rc1            9.0.201     ")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;workload;search;version' {
            $staticCompletions = @(
                [CompletionResult]::new('--format', '--format', [CompletionResultType]::ParameterName, "Changes the format of outputted workload versions. Can take `'json`' or `'list`'")
                [CompletionResult]::new('--take', '--take', [CompletionResultType]::ParameterName, "--take")
                [CompletionResult]::new('--include-previews', '--include-previews', [CompletionResultType]::ParameterName, "--include-previews")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;workload;uninstall' {
            $staticCompletions = @(
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;workload;repair' {
            $staticCompletions = @(
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "The NuGet configuration file to use.")
                [CompletionResult]::new('--source', '--source', [CompletionResultType]::ParameterName, "The NuGet package source to use during the restore. To specify multiple sources, repeat the option.")
                [CompletionResult]::new('--source', '-s', [CompletionResultType]::ParameterName, "The NuGet package source to use during the restore. To specify multiple sources, repeat the option.")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--disable-parallel', '--disable-parallel', [CompletionResultType]::ParameterName, "Prevent restoring multiple projects in parallel.")
                [CompletionResult]::new('--ignore-failed-sources', '--ignore-failed-sources', [CompletionResultType]::ParameterName, "Treat package source failures as warnings.")
                [CompletionResult]::new('--no-http-cache', '--no-http-cache', [CompletionResultType]::ParameterName, "Do not cache packages and http requests.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;workload;restore' {
            $staticCompletions = @(
                [CompletionResult]::new('--configfile', '--configfile', [CompletionResultType]::ParameterName, "The NuGet configuration file to use.")
                [CompletionResult]::new('--source', '--source', [CompletionResultType]::ParameterName, "The NuGet package source to use during the restore. To specify multiple sources, repeat the option.")
                [CompletionResult]::new('--source', '-s', [CompletionResultType]::ParameterName, "The NuGet package source to use during the restore. To specify multiple sources, repeat the option.")
                [CompletionResult]::new('--include-previews', '--include-previews', [CompletionResultType]::ParameterName, "Allow prerelease workload manifests.")
                [CompletionResult]::new('--skip-manifest-update', '--skip-manifest-update', [CompletionResultType]::ParameterName, "Skip updating the workload manifests.")
                [CompletionResult]::new('--temp-dir', '--temp-dir', [CompletionResultType]::ParameterName, "Specify a temporary directory for this command to download and extract NuGet packages (must be secure).")
                [CompletionResult]::new('--disable-parallel', '--disable-parallel', [CompletionResultType]::ParameterName, "Prevent restoring multiple projects in parallel.")
                [CompletionResult]::new('--ignore-failed-sources', '--ignore-failed-sources', [CompletionResultType]::ParameterName, "Treat package source failures as warnings.")
                [CompletionResult]::new('--no-http-cache', '--no-http-cache', [CompletionResultType]::ParameterName, "Do not cache packages and http requests.")
                [CompletionResult]::new('--interactive', '--interactive', [CompletionResultType]::ParameterName, "Allows the command to stop and wait for user input or action (for example to complete authentication).")
                [CompletionResult]::new('--verbosity', '--verbosity', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--verbosity', '-v', [CompletionResultType]::ParameterName, "Set the MSBuild verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].")
                [CompletionResult]::new('--version', '--version', [CompletionResultType]::ParameterName, "A workload version to display or one or more workloads and their versions joined by the `'@`' character.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;workload;clean' {
            $staticCompletions = @(
                [CompletionResult]::new('--all', '--all', [CompletionResultType]::ParameterName, "Causes clean to remove and uninstall all workload components from all SDK versions.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;workload;config' {
            $staticCompletions = @(
                [CompletionResult]::new('--update-mode', '--update-mode', [CompletionResultType]::ParameterName, "Controls whether updates should look for workload sets or the latest version of each individual manifest.")
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;workload;history' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;completions' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('script', 'script', [CompletionResultType]::ParameterValue, "Generate the completion script for a supported shell")
            )
            $completions += $staticCompletions
            break
        }
        'testhost;completions;script' {
            $staticCompletions = @(
                [CompletionResult]::new('--help', '--help', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('--help', '-h', [CompletionResultType]::ParameterName, "Show command line help.")
                [CompletionResult]::new('bash', 'bash', [CompletionResultType]::ParameterValue, "Generates a completion script for the Bourne Again SHell (bash).")
                [CompletionResult]::new('fish', 'fish', [CompletionResultType]::ParameterValue, "Generates a completion script for the Fish shell.")
                [CompletionResult]::new('nushell', 'nushell', [CompletionResultType]::ParameterValue, "Generates a completion script for the NuShell shell.")
                [CompletionResult]::new('pwsh', 'pwsh', [CompletionResultType]::ParameterValue, "Generates a completion script for PowerShell Core. These scripts will not work on Windows PowerShell.")
                [CompletionResult]::new('zsh', 'zsh', [CompletionResultType]::ParameterValue, "Generates a completion script for the Zsh shell.")
            )
            $completions += $staticCompletions
            break
        }
    }
    $completions | Where-Object -FilterScript { $_.CompletionText -like "$wordToComplete*" } | Sort-Object -Property ListItemText
}
