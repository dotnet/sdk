# Validating dotnet-format

dotnet-format includes project loading tests which validate clean project loading for all the common C# & VB project templates. To validate new SDKs, we can simply must remove the SDK pinned in the global.json and run `Test.cmd`.

## Steps:
1. Install the SDK being validated against.
1. Checkout the dotnet-format repo. `git clone https://github.com/dotnet/format.git`
2. Update the gobal.json by removing the "sdk" configuration.
3. Run `Restore.cmd`.
4. Run `Build.cmd`.
5. Run `Test.cmd`.

You can report test failures here: https://github.com/dotnet/format/issues
