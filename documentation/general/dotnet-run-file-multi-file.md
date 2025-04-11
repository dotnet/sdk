# Multi-file support in `dotnet run app.cs`

## Scenarios

Some multi-file scenarios to consider:

### Direcotry-based apps

- Directory of files that are all part of the same app
- Only a single entry-point file
- Might be using differnt SDKs, e.g. `Microsoft.NET.Sdk` vs. `Microsoft.NET.Sdk.Web`, that have different default items

Example console app:

```text
- myapp/
  - myapp.cs
  - helpers.cs
```

Example Web API app:

```text
- webapi/
  - webapi.cs
  - helpers.cs
  - customerapi.cs
  - productapi.cs
  - appsettings.json
```

Example Razor app:

```text
- razorapp/
  - razorapp.cs
  - helpers.cs
  - Pages/
    - _ViewImports.razor
    - Index.razor
    - App.razor
    - Layout.razor
  - appsettings.json
```

Example Aspire app host app:

```text
- apphost/
  - apphost.cs
  - helpers.cs
  - appsettings.json
```

### Directories with multiple separate entry-point files

- Each entry-point file is a separate app
- Some entry-point files require types from some non-entry-point files
- Entry-point files don't require types from other entry-point files

Example:

```text
- apps/
  - app1.cs
  - app2.cs
  - webapi1.cs
  - helpers.cs
```

### Directories with multiple related entry-point files

- Each entry-point file is a separate app
- Non-entry-point files are shared by multiple entry-point files
- Some entry-point files require types from other entry-point files, e.g. `cat.cs` and `cat.benchmarks.cs`, `webapi.cs` and `webapi.tests.cs`

Example:

```text
- apps/
  - app1.cs
  - app1.benchmarks.cs
  - app2.cs
  - app2.tests.cs
  - helpers.cs
```

### Other considerations

- Different default items for different SDKs, e.g. `Microsoft.NET.Sdk` vs. `Microsoft.NET.Sdk.Web`
- Performance from CLI and IDE point of view
- Workspace context in IDE for a given file (does each entry-point file map to a project? or do they all map to the same project? or do they all map to different projects?)

## Approaches

### Adopt default items according to the SDK being used

- Defaults all files to work like a regular project
- Can be disabled for a given entry-point file by adding a directive like `#:property EnableDefaultItems=false`
- Can be disbled for a whole directory by adding a file `Directory.Build.props` with the following content:

    ```xml
    <Project>
      <PropertyGroup>
        <EnableDefaultItems>false</EnableDefaultItems>
      </PropertyGroup>
    </Project>
    ```

- Works for all SDKs including those that add other default items beyond **.cs* files
- Maps naturally to converted project
- How to know when to stop including files in the directory tree? Is this already implicitly handled given existing project-based default items behavior?
- How to deal with multiple entry-point files?
  - Include all entry-point files, no special treatment at the file level
  - Update compiler to handle multiple top-level statements entry-point files by new option that changes generated type name to one derived from the file name, e.g. `app` or `Program_app.cs` for `app.cs` and `app2` or `Program_app2` for `app2.cs`, so that type name conflicts are avoided
  - `dotnet run app.cs` will set the [build/compiler options](https://learn.microsoft.com/dotnet/csharp/language-reference/compiler-options/advanced#mainentrypoint-or-startupobject) to use the entry-point type that corresponds to the entry-point file that was passed
- With compiler changes, enables all scenarios mentioned above

### Require explicit opt-in to enable default items

- Defaults all entry-point files to be single-file apps
- Require default items to be explicitly enabled via the entry-point file
- Can be enabled for a given entry-point file by adding a directive like `#:property EnableDefaultItems=true`
- Works for all SDKs including those that add other default items beyond **.cs* files once opted-in
- Doesn't map to the natural default for coverted projects, but maybe that's OK? When converted a new directory is created and only the relevant files are brought in based on whether default items were enabled.
- Likely hard to discover for users. Mitigation could be to have a special directive, e.g. `#:multifile`

### Support directives to specify included files

- Defaults all entry-point files to be single-file apps
- Support directives to specify included files patterns, e.g. `#:include *.cs` or `#:include **/*.cs`
  - This seems pretty good at first but is limited to `Compile` items only. Maybe has utility as a limited option to go along with `#:property EnableDefaultItems=false` for folks who want to control included **.cs* files only.
- Supporting other file types including Razor, JSON, etc. (i.e. those that don't map to `<Compile Include="..." />`) would require a more complex pattern
  - Needing to specify the item type at all is unusual as most use cases with regular projects do not require it, e.g. `#:compile` to include files as `<Compile Include="...">`, `#:razorcompile` to include files as `<RazorCompile Include="...">`, etc.
  - Other possible option is to mirror `#:property` and support `#:item` with a structure that enables specifying the item type and pattern for include, e.g. `#:item Compile=*.cs` or `#:item RazorCompile=**/*.razor`
  - Supporting items in a way that isn't super limiting likely requires ensuring support for include, exclude, and update and that will make the syntax more complex
