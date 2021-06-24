# Template Engine API: Migration from .NET 3.1/.NET 5 to .NET 6

In .NET 6 the available API set has significantly changed with breaking changes
involved. The required changes when migrating from the earlier versions to .NET
6 are given below.

## Bootstrapper implementation

Most of the methods of
[Bootstrapper](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.IDE/Bootstrapper.cs)
are obsolete now, preserving backward compatibility when possible. Please ensure
you use new methods when migrating to .NET 6, the obsolete messages in the code
will point to new version of method.

## Template package management

With .NET 6 a new way of template package management was introduced. Now the
template packages are managed using providers with ability to support several
providers and adding custom implementations of providers when implementing the
host. This was done to fix a bug that CLI templates disappeared on SDK version
update, support Optional Workloads and provide more future flexibility.

Template engine delivers one provider out of box in .NET 6 – global settings
template provider. The templates installed to this provider are available to all
the hosts using IDE/Edge implementations to work with template engine
generators.

(Now obsolete) Install and Uninstall methods of Bootstrapper now install
templates to the global settings template provider, this means the templates
installed via these methods will be available to all the hosts (including dotnet
new host), and other hosts may update or uninstall them. If you wish to
contribute to the common share of templates, so that templates you install are
also available to users of the .NET CLI, Visual Studio and all other hosts, you
do not need to anything immediately, but please replace use of the obsolete
methods at your next opportunity.

If your host implementation requires the template to be installed specifically
for your host without other hosts having access to them, you need to manage it
via template package provider, starting in .NET 6. The basic workflow is:

-   Implement ITemplatePackageProviderFactory and ITemplatePackageProvider
    interface.

    ITemplatePackageProviderFactory has the following members:

    -   property ID – unique Guid representing the component ID.

    -   property DisplayName – string – the display name for the provider

    -   method CreateProvider – creates implemented ITemplatePackageProvider
        with passing IEngineEnvironmentSettings.

ITemplatePackageProvider interface has the following members:

-   method: GetAllTemplatePackagesAsync which returns the list of
    ITemplatePackage. ITemplatePackage has the following properties:

    1.  string MountPointUri – the location of template package (can be local
        folder or local NuGet package).

        1.  DateTime LastChangeTime – date/time the package was changed.

        2.  ITemplatePackageProvider – provider returning the package (itself).

[The default implementation of
ITemplatePackage](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/TemplatePackage/TemplatePackage.cs)
is available in Abstractions assembly.

-   event TemplatePackagesChanged – the event that should be raised if template
    packages returned by provider has changed. Triggering this event will
    trigger update template engine cache in TemplatePackageManager. Implementing
    this event is not mandatory but recommended. If the event is not
    implemented, the cache should be reloaded manually passing force parameter
    when getting packages or templates, or TemplatePackageManager should be
    recreated.

1.  Register the factory using AddComponent method of Bootstrapper or
    IEngineEnvironmentSettings.ComponentManager. The component should be added
    each time Bootstrapper or EngineEnvironmentSettings is instantiated - it is
    not cached by template engine.

The host may implement as many providers as needed.

In future releases we are considering a built-in host template provider and host
version template provider allowing you to install the templates available to
specific host or host version.

## ITemplateEngineHost updates 

[ITemplateEngineHost](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Abstractions/ITemplateEngineHost.cs)
interface and its default implementation
[DefaultTemplateEngineHost](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Edge/DefaultTemplateEngineHost.cs)
was changed:

-   ILogger Logger and ILoggerFactory LoggerFactory properties were added.
    Template Engine now uses Microsoft.Extensions.Logging to log messages of
    different levels. At the moment, logging is not done in many components,
    however we plan to revise it in next versions and increase the logging
    coverage.

-   Previous logging methods and events are now obsolete.
    OnCriticalError/OnNonCriticalError are now replaced with error/warning log
    messages, respectively. In case you need more details on how to replace
    them, please reach us out in GitHub.

## Loading the components

Boostrapper/IComponentManager methos Register and RegisterMany are now
deprecated. This way of loading components was not performant due to reflection
usage.

If you were using these methods to load default components
(Microsoft.TemplateEngine.Edge, Microsoft.TemplateEngine.RunnableProjects), use
LoadDefaultComponents method instead or pass loadDefaultComponents set to true
in Bootstrapper constructor.

If you were using these methods to load other components, use AddComponent
method instead (pay attention that this should be done each time
Bootstrapper/EngineEnvironmentSettings are instantiated) or pass them in
BuiltInComponents property of you ITemplateEngineHost.

Advanced: if you want to load only some components of
Microsoft.TemplateEngine.Edge or Microsoft.TemplateEngine.RunnableProjects
projects, you may use Components.AllComponents property in these assemblies to
get the list of available components, and then load required components via
AddComponent methods.

The list of components defined in
[Microsoft.TemplateEngine.Edge:](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Edge/Components.cs)

-   Providers (ITemplatePackageProviderFactory)

    -   global settings provider

-   Mount points (IMountPointFactory)

    -   Zip (also supports NuGet packages)

    -   Folder (FileSystemMountPointFactory)

-   Installers (IInstallerFactory)

    -   Folder

    -   NuGet

If you don’t need global settings provider to be loaded, do not include it when
loading the components. In this case installers are not used as well, include
them only if needed for the host. It is recommended to always include default
mount point implementations.

The list of components defined in
[Microsoft.TemplateEngine.Orchestrator.RunnableProjects](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/Components.cs):

-   Generators

    -   Runnable project generator (template.json generator)

-   Macros supported by runnable project generator

-   Operation configs supported by runnable project generator

If you are using runnable project generator, consider adding macros and
operation configs implementation to enable these features.

## ISettingsLoader

(This section is only applicable for those who did not use Bootstrapper class
before)

ISettingsLoader and its implementation is no longer available. It was
multi-purpose and overloaded, that’s why we decided to split it in logical
parts.

For components management, please use IComponentManager and
IEngineEnvironmentSettings.ComponentManager.

For template package and template cache, use new class
[TemplatePackageManager](https://github.com/dotnet/templating/blob/main/src/Microsoft.TemplateEngine.Edge/Settings/TemplatePackageManager.cs).
It is required to instantiate this class to work with it. It reads all template
packages available to engine and maintains the template cache.
TemplatePackageManager maintains the cache for information received from
providers and read from template cache, so it’s quite performant after the first
call unless providers triggered events on template package change, then
TemplatePackageManager will do rescan available templates and save the updates
to template cache.

## Closing notes

Previously, the Template Engine packages had many public members, including
implementations which were unneeded. This meant future changes had breaking
change concerns, even though we believe these methods do not have a useful
public purpose. We made a lot of implementations and members internal. *If the
class/member you need is not available now, please reach us out via GitHub
issues to find a solution.*