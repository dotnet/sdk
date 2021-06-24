# Inside the Template Engine

The template engine is composed of several different subsystems, which are
designed to separate gathering templates, instantiating template and processing
templates.

# Overview of the subsystems

## [IEngineEnvironmentSettings](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/IEngineEnvironmentSettings.cs)

This is responsible for holding all properties of environment.

Template engine provides default implementation:
[EngineEnvironmentSettings](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Edge/EngineEnvironmentSettings.cs).

It has single constructor that accepts ITemplateEngineHost and set up settings
based on TemplateEngine.Edge implementation of component manager, paths, and
environment. All this can also be passed in as optional parameters.

### [ITemplateEngineHost](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/ITemplateEngineHost.cs)

The interface is responsible for providing host information to template engine,
managing file system and do the logging. Host may also provide default parameter
values for template via TryGetHostParamDefault method.

The applications using template engine are often called as “hosts”.
dotnet/templating repo is managing one of such hosts: dotnet new CLI, which is
part of dotnet SDK.

Main host properties:

-   Identifier – unique name of the host

-   Version – version of the host

Those properties are used to identify host to various built-in components
explained below.

If you are considering using the template engine core, you need to make
implementation of this interface representing your host. Template engine
provides default implementation:
[DefaultTemplateEngineHost](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Edge/DefaultTemplateEngineHost.cs).

#### IPhysicalFileSystem

Abstraction over file system, accessible from ITemplateEngineHost.

Each template engine host should support physical and in-memory file system and
virtualize file system under given path. Switch between physical and in-memory
is done via ITemplateEngineHost.VirtualizePath method.

Template engine provides default implementation of file systems:
[PhysicalFileSystem](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Utils/PhysicalFileSystem.cs)
and
[InMemoryFileSystem](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Utils/InMemoryFileSystem.cs).

### [IComponentManager](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/IComponentManager.cs)

ComponentManager is responsible for loading components provided by Host from
ITemplateEngineHost.BuiltIns, but additional components can be added via
IComponentManager.AddComponent or they are dynamically loaded from
TemplatePackage during scanning similar to templates. ComponentManager
implementation is not publicly accessible, however it is used when default
implementation of IEngineEnvironmentSettings is used.

### [IEnvironment](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/IEnvironment.cs)

Abstraction over environment variables. Default implementation uses system
environment variables.

### [IPathInfo](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/IPathInfo.cs)

Provides the main paths used by template engine: global settings path, host
settings path host version settings path.

The default locations are:

-   \<home directory\>/.templateengine – global settings

-   \<home directory\>/.templateengine/\<host ID\> – host settings

-   \<home directory\>/.templateengine/\<host ID\>/\<host version\> – host
    version settings

## [TemplatePackageManager](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Edge/Settings/TemplatePackageManager.cs) class

The class responsible for managing templates and templates packages.

-   Gets available providers to install/update/uninstall template packages
    avaliable via IComponentManager

-   Gets available template packages

-   TemplatePackagesChanged which is triggered when list of template packages
    changes

-   Gets available templates

-   Manages template packages cache and template cache.

The host need to instantiate the class when needed. Note that the first-time run
can be time consuming, so consider creating one instance for duration of hosting
application.

## [TemplateCreator](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Edge/Template/TemplateCreator.cs) class

The class responsible for template creation, including dry run. The host need to
instantiate the class when needed.

# Template Engine packages

Template engine publishes the following packages:

-   Microsoft.TemplateEngine.Abstractions – contains interfaces to work with
    template engine

-   Microsoft.TemplateEngine.Egde – enables hosting the template engine in the
    application

-   Microsoft.TemplateEngine.IDE – lightweight API over Edge (optional to use)

-   Microsoft.TemplateEngine.Orchestrator.RunnableProjects – template engine
    default generator (a.k.a. the generator of template.json format)

-   Microsoft.TemplateEngine.Utils – different utilities useful for template
    engine usage

# Components

Main purpose of components is to allow template authors and host implementers to
supply their own implementations of different interfaces and to invoke them by
specifying GUID or name that defines component in template.json. Components can
be split into two main categories:

-   the components required to run templates:

    -   macros

    -   post actions (TBD)

    -   generators

-   the components required by the host:

    -   template package providers: managed and non-managed

    -   installers

    -   mount points

## Template package providers 

Template package providers responsible for providing template packages the
template engine needs to manage. There are two kinds of providers:

-   Non-managed
    ([ITemplatePackageProvider](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/TemplatePackage/ITemplatePackageProvider.cs))
    – the template engine uses the packages provided by provider however cannot
    modify them

-   Managed
    ([IManagedTemplatePackageProvider](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/TemplatePackage/IManagedTemplatePackageProvider.cs))
    – the template engine can install/update/uninstall packages to provider.

Template engine provides the following providers:

-   Global settings managed provider – the packages available to all template
    engine hosts using built in implementation.

-   (not available yet) host managed provider - the packages available to
    current host.

-   (not available yet) host version managed provider - the packages available
    to current host version.

Template engine also provides [default non-managed provider
implementation](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Utils/DefaultTemplatePackageProvider.cs)
that can be used by other hosts to build simple providers or base implementation
on. Here are two examples:
[SdkTemplates](https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/commands/dotnet-new/BuiltInTemplatePackageProvider.cs)
and
[OptionalWorkloads](https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/commands/dotnet-new/OptionalWorkloadProvider.cs).

### Prioritizing the providers

Template providers can implement the
[IPrioritizedComponent](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/IPrioritizedComponent.cs)
interface to setup the provider priority. The providers will be read following
the priority with the priority with largest value of priority to be preferred in
case the providers have same templates defined.

The default priority of Global Settings provider is 1000. If priority is not
defined, the provider will be treated as provider with priority 0.

Note: it is possible to define negative priority, then the provider will be
lower priority than default.

When implementing provider, note that the packages returned by provider will be
processed exactly in the same order they provided, in case of template is
available in several packages, the last package will win.

## [Installer](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/Installer/IInstaller.cs)

Installer installs, updates and uninstall the template packages that can be feed
to template package providers.

Template engine provides two installer implementations:

-   NuGet – installs the template package from NuGet feed

-   Folder – installs the template package from given folder path

You can use them when implementing your own providers, they are available from
IComponentManager.

## [Mount point](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/Mount/IMountPoint.cs)

Template package location is represented by mount point URI. Mount point that
can load certain type of URI are also components.

Template engine provides two mountpoint factory implementations:

-   Zip/NuGet – manages zip package/NuGet package

-   File System – manages folder on local file system

They are available from IComponentManager.

It is possible to get mount point using bool TryGetMountPoint(string
mountPointUri, out IMountPoint mountPoint) method of IEngineEnvironmentSettings.

## Registering the components

When creating template engine host, all built in components will be registered.

If you’d like to use the runnable projects (template.json) generator available
from Microsoft.TemplateEngine.Orchestrator.RunnableProjects, you need to add it
using AddComponent method of IComponentManager.

You can implement the following own components and register them:

-   [ITemplateEngineProvider](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/TemplatePackage/ITemplatePackageProvider.cs)
    – non-managed template package provider, via
    [ITemplatePackageProviderFactory](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/TemplatePackage/ITemplatePackageProviderFactory.cs)

-   [IManagedTemplateEngineProvider](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/TemplatePackage/IManagedTemplatePackageProvider.cs)
    – managed template package provider, via
    [ITemplatePackageProviderFactory](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/TemplatePackage/ITemplatePackageProviderFactory.cs)

-   [IMountPoint](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/Mount/IMountPoint.cs)
    – mount point implementation, via
    [IMountPointFactory](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/Mount/IMountPointFactory.cs)

-   [IInstaller](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/Installer/IInstaller.cs)
    – template package installer, via
    [IInstallerFactory](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/Installer/IInstallerFactory.cs)

It is possible to register additional components to in the following way:

-   Components of
    [ITemplateEngineHost.BuiltInComponents](https://github.com/dotnet/templating/blob/6ab649522414baa8fe31d08b449a5063e5572291/src/Microsoft.TemplateEngine.Abstractions/ITemplateEngineHost.cs#L21)
    will be added when loading environment settings. For
    DefaultTemplateEngineHost they can be passed to constructor.

-   You can use
    [IComponentManager.AddComponent](https://github.com/dotnet/templating/blob/6ab649522414baa8fe31d08b449a5063e5572291/src/Microsoft.TemplateEngine.Abstractions/IComponentManager.cs#L58)
    method to add the component in runtime.

Note that components are not persisted, so they should be added each time
EngineEnvironmentSettings instance is created.

# Microsoft.TemplateEngine.IDE 

The package provides
[Bootstrapper](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.IDE/Bootstrapper.cs)
class allowing to setup and access main functionality of template engine via
single entry point.

Note that it is not mandatory to use
[Bootstrapper](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.IDE/Bootstrapper.cs)
class to use template engine, however if you don’t need advanced features it
might be easier to start with using this API.

Basic workflow:

1.  The application hosting template engine should implement and instantiate
    [ITemplateEngineHost](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/ITemplateEngineHost.cs),
    or instantiate default implementation
    ([DefaultTemplateEngineHost](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Edge/DefaultTemplateEngineHost.cs)).

2.  The application creates the instance of Bootstrapper:

    1.  If session state doesn’t need to be persisted use
        virtualizeConfiguration set to true – all the settings will be stored in
        memory only.

    2.  If the host assumes running default infrastructure, use
        loadDefaultComponents set to true – this will load the default generator
        and default infrastructure (providers, installers, mount points)

3.  If the application has the templates specific to it, the application should
    implement
    [ITemplatePackageProvider](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/TemplatePackage/ITemplatePackageProvider.cs)
    returning available template packages and register it via
    Bootstrapper.AddComponent method or provide it via built-in components of
    the host.

4.  To get template packages, use GetTemplatePackages method.

    Note: these templates are available to all template engine hosting
    applications for the current user.

5.  To manage template packages installed globally, use
    InstallTemplatePackagesAsync, GetLatestVersionAsync,
    UpdateTemplatePackagesAsync, UninstallTemplatePackagesAsync methods.

    Installation supports installing local sources (folder, packages) as well as
    installing NuGet packages from remote feeds.

    Note: these actions impact set of template packages available to all
    template engine hosting applications for the current user.

6.  To available list all templates, use GetTemplatesAsync method.

    The method also supports filters and criterias to filter templates. The
    default filters are available in
    [Utils.WellKnownSearchFilters](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Utils/WellKnownSearchFilters.cs)
    class.

7.  To dry run the template, use GetCreationEffectsAsync method.

    This method needs template definition (ITemplateInfo) that can be obtained
    through GetTemplatesAsync.

8.  To run the template, use CreateAsync method.

    This method needs template definition (ITemplateInfo) that can be obtained
    through GetTemplatesAsync.

Note: Bootstrapper class needs to be disposed.
