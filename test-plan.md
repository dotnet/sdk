# Validating dotnet-format

dotnet-format includes project loading tests which validate clean project loading for all the common C# & VB project templates. To validate new SDKs, we can simply must remove the SDK pinned in the global.json and run `Test.cmd`.

## Steps:
1. Install the SDK being validated against.
2. Checkout the dotnet-format repo. `git clone https://github.com/dotnet/format.git`
3. Update the gobal.json by removing the "sdk" configuration.
Before:
```json
{
  "tools": {
    "dotnet": "6.0.103"
  },
  "sdk": {
    "version": "6.0.103"
  },
  "msbuild-sdks": {
    "Microsoft.DotNet.Arcade.Sdk": "6.0.0-beta.22166.2"
  }
}
```
After:
```json
{
  "tools": {
    "dotnet": "6.0.103"
  },
  "msbuild-sdks": {
    "Microsoft.DotNet.Arcade.Sdk": "6.0.0-beta.22166.2"
  }
}
```
4. Run `Restore.cmd`.
5. Run `Build.cmd`.
6. Run `Test.cmd`.

You can report test failures here: https://github.com/dotnet/format/issues
