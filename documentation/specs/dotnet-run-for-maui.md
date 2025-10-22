# `dotnet run` for .NET MAUI Scenarios

The current state of `dotnet run` for .NET MAUI projects is summarized
by this issue from 2021:

* [Figure out 'dotnet run' CLI experience for iOS and Android](https://github.com/dotnet/xamarin/issues/26)

The pain points being:

* iOS and Android use different properties to select a device,
  emulator, or simulator. It is difficult for developers to make this
  selection as you need to run platform-specific commands along with
  complex MSBuild properties.

* Each platform has different behavior, regards to displaying console
  output, etc.

* Using an MSIX with WindowsAppSDK doesn't support `dotnet run` at all,
  relying completely on IDEs like Visual Studio.

* `dotnet run` does not have a concept of a "deploy" step.

This has become more relevant in the AI era, as someone is going to
expect AIs in "agent mode" to build and run their app. If the
command-line options are difficult, AIs will fail just as badly as
humans do today.

## The `dotnet run` Pipeline

These are the high-level steps during `dotnet run`, that we would like
to make extensible for .NET MAUI (and future) scenarios.

* `restore`: unchanged

* "Pre-run evaluation"

  * If the project is multi-targeted, containing the
    `$(TargetFrameworks)` property _and_ that property has more than
    one item in it, and `-f` was not supplied...

    * Prompt the user to select from a list of the
      `$(TargetFrameworks)`

    * Non-interactive mode will give a friendly error message,
      suggesting to supply the `-f` property, listing available target
      frameworks in the project.

  * Once a `$(TargetFramework)` is selected, either from previous
    steps or `-f`...

    * If a `ComputeAvailableDevices` MSBuild target is available, provided by
      the iOS or Android workload, etc. ...

    * Call the MSBuild target, which returns a list of `@(Devices)` items...

```xml
<ItemGroup>
  <!-- Android examples -->
  <Devices Include="emulator-5554"  Description="Pixel 7 - API 35" Type="Emulator" Status="Offline" />
  <Devices Include="emulator-5555"  Description="Pixel 7 - API 36" Type="Emulator" Status="Online" />
  <Devices Include="0A041FDD400327" Description="Pixel 7 Pro"      Type="Device"   Status="Online" />
  <!-- iOS examples -->
  <Devices Include="94E71AE5-8040-4DB2-8A9C-6CD24EF4E7DE" Description="iPhone 11 - iOS 18.6" Type="Simulator" Status="Shutdown" />
  <Devices Include="FBF5DCE8-EE2B-4215-8118-3A2190DE1AD7" Description="iPhone 14 - iOS 26.0" Type="Simulator" Status="Booted" />
  <Devices Include="23261B78-1E31-469C-A46E-1776D386EFD8" Description="My iPhone 13"         Type="Device"    Status="Unavailable" />
  <Devices Include="AF40CC64-2CDB-5F16-9651-86BCDF380881" Description="My iPhone 15"         Type="Device"    Status="Paired" />
</ItemGroup>
```

_NOTE: each workload can decide which metadata values for `%(Type)`
and `%(Status)` are useful, filtering offline devices, etc. The output
above would be analogous to running `adb devices`, `xcrun simctl list
devices`, or `xcrun devicectl list devices`._

* Continuing on...

  * Prompt the user to select from this list of devices, emulators,
    or simulators.

  * Non-interactive mode will error, suggesting to supply the
    `--device` switch. Listing the options returned by the
    `ComputeAvailableDevices` MSBuild target.

* `build`: unchanged, but is passed `-p:Device`.

* `deploy`

  * If a `DeployToDevice` MSBuild target is available, provided by the
    iOS or Android workload, etc.

  * Call the MSBuild target, passing in the identifier for the selected
    `-p:Device` global MSBuild property.

  * This step needs to run, even with `--no-build`, as you may have
    selected a different device.

* `ComputeRunArguments`: unchanged, but is passed `-p:Device`.

* `run`: unchanged. `ComputeRunArguments` should have set a valid
  `$(RunCommand)` and `$(RunArguments)` using the value supplied by
  `-p:Device`.

## New `dotnet run` Command-line Switches

So far, it feels like no new subcommand is needed. In interactive
mode, `dotnet run` will now prompt to select a `$(TargetFramework)`
for all multi-targeted projects. Platform-specific projects like
Android, iOS, etc. will prompt for device selection.

`dotnet run --list-devices` will:

* Prompt for `$(TargetFramework)` for multi-targeted projects just
  like when `--list-devices` is omitted.

  * If there is a single `$(TargetFramework)`, skip to the next step.

* Call `ComputeAvailableDevices` if the MSBuild target exists, just
  like when `--list-devices` is omitted.

  * List the available targets by name, a unique identifier, and an
    optional status of the device.

  * Print a friendly message that says how to run `dotnet run` with
    the new `--device` switch.

  * If `ComputeAvailableDevices` does not exist in the project
    (workload), it can print a friendly message and exit.

* `dotnet run --list-devices` will then basically exit early, never
  running any build, deploy, `ComputeRunArguments`, or run steps.

A new `--device` switch will:

* bypass the device-selection portion of the `run` workflow described above

* Pass in the `-p:Device` global MSBuild property to all build,
  deploy, `ComputeRunArguments`, or run steps.

* The iOS and Android workloads will know how to interpret `$(Device)`
  to select an appropriate device, emulator, or simulator.

## What about Launch Profiles?

The iOS and Android workloads ignore all
`Properties/launchSettings.json` files and do nothing with them.

WindowsAppSDK requires a launch profile to select between "packaged"
MSIX and an "unpackaged" .exe, and so we currently provide this in the
`dotnet new maui` project template:

```json
{
  "profiles": {
    "Windows Machine": {
      "commandName": "Project",
      "nativeDebugging": false
    }
  }
}
```

Or if you want to be "packaged":

```json
{
  "profiles": {
    "Windows Machine": {
      "commandName": "MsixPackage",
      "nativeDebugging": false
    }
  }
}
```

Launch profiles don't immediately look useful for iOS or Android, as
the identifier you'd put in here would be different per developer --
you wouldn't want the value saved in source control. You could put a
generic selection like device vs emulator/simulator, but that isn't
addressing the problem we are interested in. Full launch profile
support for .NET MAUI projects may be interesting to address in the
future.

## iOS and Android workload behavior

We will work to align as much behavior as possible between the
platforms. Most of this work will happen in the dotnet/android and
dotnet/macios repo, and would be orthogonal to the changes in the .NET
SDK.

## macOS and MacCatalyst Behavior

`dotnet run` "just works" on macOS Desktop, because `$(RunCommand)`
can launch the app directly -- similar to console apps.

## WindowsAppSDK Behavior

Running a `dotnet new maui` project works today because of the default
launch profile in "unpackaged" mode:

```dotnetcli
> dotnet run -f net10.0-windows10.0.19041.0
Using launch settings from D:\src\hellomaui\Properties\launchSettings.json...
```

To improve the behavior for "packaged" / MSIX, we could:

* Implement the `DeployToDevice` MSBuild target.

* `ComputeAvailableDevices` is not needed.

* Implement the `ComputeRunArguments` MSBuild target.

In the future, we can either add this logic into the .NET MAUI
workload or WindowsAppSDK itself.

## Other frameworks: Avalonia, Uno, MonoGame, etc.

When these frameworks run on iOS or Android, they are basically using
the `ios` and `android` workloads _without_ .NET MAUI. Any iOS or
Android-specific behavior would apply to these project types in the
same way a `dotnet new android` or `dotnet new ios` project template
would.
