# How to track what's installed in dotnetup

dotnetup should support installing various versions of the .NET SDK or runtime, as well as updating or uninstalling them.  To do this, it will need to store information about what is installed in what we call the dotnetup shared manifest.

## Desired behavior

When a user installs a .NET SDK or runtime with dotnetup, we will call the information about what they requested to be installed the "Install Spec".  This includes the component that should be installed as well as the version or channel that should be installed.  An install spec may also be derived from a global.json file, in which case the spec should also include the path to the corresponding global.json.

The effect of an update operation should be to install the latest version of the SDK or runtimes that matches each active install spec.  Any installations that are no longer needed by any active install specs would then be removed.

An uninstall operation would be implemented as deleting an install spec and then running a garbage collection.  This may not actually delete or uninstall anything if there are other install specs that resolve to the same version.  In that case dotnetup should display a message explaining what happened (and what specs still refer to that version) so that the user doesn't get confused about why the version is still installed.  We might also want to offer some sort of force uninstall command.

## Dotnetup shared manifest contents

We will store the manifest in json format.  At the top level it should have a schemaVersion property to help make it possible to update the format if we need to.

### Dotnet roots

- Path to dotnet root
- Architecture of installs in the root

### Install specs

- Component (SDK or one of the runtimes)
- Version, channel, or version range
- Source: explicit install command, global.json, or previous (non-dotnetup) install
- Global.json path

### Installation
- Component
- Version (this is the exact version that is installed)
- Subcomponents

### Subcomponent

Subcomponents are sub-pieces of an installation.  We need to represent these because different installed components or versions may have overlapping subcomponents.  So for each installation, we will keep a list of subcomponents that are part of that installation.

A subcomponent can be identified by a relative path to a folder from the dotnet root.  The depth of the folder depends on the top-level subfolder under the dotnet root.  For example:

- `sdk/10.0.102` - 2 levels
- `packs/Microsoft.AspNetCore.App.Ref/10.0.2` - 3 levels
- `sdk-manifests/10.0.100/microsoft.net.sdk.android/36.1.2` - 4 levels

### Manifest structure

Each dotnet root may have multiple install specs and installations.  Each Installation may have multiple subcomponents.

We could represent the dotnet roots in the manifest as a node which has properties for the dotnet root path and the architecture, as well as the list of install specs and installations.  Alternatively, we could have Install specs and installations be top-level properties in the manifest and each of them could have a dotnet root path and an architecture property.

### Sample manifest

```json
{
  schemaVersion: "1",
  dotnetRoots:
  [
    {
      path: "C:\\Users\\Daniel\\AppData\\Local\\dotnet",
      architecture: "x64",
      installSpecs:
      [
        {
          "component": "sdk",
          "versionOrChannel": "10",
          "installSource": "explicit"
        },
        {
          "component": "runtime",
          "versionOrChannel": "9",
          "installSource": "explicit"
        }
      ],
      installations:
      [
        {
          "component": "sdk",
          "version": "10.0.103",
          "subcomponents":
          [
            "sdk/10.0.103",
            "shared/Microsoft.AspNetCore.App/10.0.3",
            "shared/Microsoft.NETCore.App/10.0.3",
            "shared/Microsoft.WindowsDesktop.App/10.0.3",
            "etc."
          ]
        },
        {
          "component": "runtime",
          "version": "9.0.12",
          "subcomponents":
          [
            "shared/Microsoft.NETCore.App/9.0.12",
          ]
        }
      ]
    }
  ]
}
```

## Implementation

### Installing a component

- Is there already a matching install spec in the shared manifest?
  - If yes, then we may want to do an update on that install spec instead of an install
- Resolve the version of the component to install
- If that version is not already installed:
  - Install that version.  Subcomponents that are already installed don't need to be overwritten.
  - Add installation record to shared manifest
  - Installation record should include subcomponents based on the archive that was used
- Add install spec to shared manifest

### Updating a component

- Get the latest available version that matches the install spec
- If there's no installation matching that version:
  - Install that version
- Run garbage collection to remove any installations that are no longer needed

### Deleting a component

- Remove corresponding install spec
- Run garbage collection
- If there's still an installation matching removed install spec, print message explaining why

### Garbage collection

- Go through all install specs in the manifest.
  - For install specs that came from a global.json file, update the versions in them if the global.json file has changed.  Delete those specs if the global.json file has been deleted (or no longer specifies a version).
  - For each install spec, find the latest installation record that matches.  Mark that installation record to keep for this garbage collection.
- Delete all installation records from the manifest which weren't marked.
- Iterate through all components installed in the dotnet root.  Remove any which are no longer listed under an installation record in the manifest.

### Using dotnet roots that were not previously managed by dotnetup

Users may use dotnetup to install SDKs or runtimes into a folder that already has other versions installed, which were not installed by dotnetup.  This means that the dotnetup manifest won't contain any entries for that dotnet root.  If we don't do any special handling of the existing items in the root, then garbage collection would immediately delete them all.

There are multiple ways we can handle this:

- We can error out and tell the user it's not supported to install into existing non-dotnetup-managed dotnet roots.
- We can examine the dotnet root for existing components and record an install spec, installation, and subcomponents of the installation to cover all the existing files in the folder.  That way they won't be garbage collected for future operations.  Later the user could choose to uninstall all of the previous versions as a single entity if they have fully migrated to dotnetup installations.
- We could try to detect what components are installed and re-create installations for them.  This would likely require downloading the original archives for those components so we can map what subcomponents belong to each one.

We will start with the first option (or possibly the second if it's trivial enough to implement), and adjust based on user feedback.

NOTE: If the manifest is corrupted, it may be necessary to delete it and start from scratch, which may mean reinstalling all components.

### Concurrency / locking

We'll use the ScopedMutex over `MutexNames.ModifyInstallationStates` to prevent multiple processes from accessing the manifest or executing install operations at the same time.