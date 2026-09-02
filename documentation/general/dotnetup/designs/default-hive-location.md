# Default dotnetup hive location

## Decision

The default dotnetup-managed .NET hive MUST be the `dotnet` child of the dotnetup data directory. On Windows, this is `%LocalAppData%\dotnetup\dotnet`; the equivalent platform data directory is used on macOS and Linux.

Explicit install paths remain separate hives. This decision changes only the default path.

## Rationale

The product-owned `dotnetup` directory gives the managed hive an unambiguous ownership boundary and avoids conflicts with pre-existing user or installer content in a generic `dotnet` directory. Keeping the manifest, configuration, cache, and managed .NET installation under one root also makes the default layout coherent and discoverable. The `dotnetup` manifest tracks install root content for garbage collection, so cleanup and uninstall operations cannot delete the data directory wholesale.

## Prior art

- [dnvm](https://github.com/dn-vm/dnvm/blob/main/src/dnvm/DnvmEnv.cs) stores its manifest, executable, and installed SDK directories under `DNVM_HOME`; its default SDK directory is a child named `dn`.
- [rustup](https://github.com/rust-lang/rustup/blob/main/src/config.rs) derives settings, downloads, and installed toolchains from `RUSTUP_HOME`.
- [nvm](https://github.com/nvm-sh/nvm/blob/master/nvm.sh) stores versions, aliases, and its cache under `NVM_DIR`.
- [nvm-windows](https://github.com/coreybutler/nvm-windows/blob/master/nvm.iss) defaults its manager root to `%LocalAppData%\nvm`; [installed Node versions](https://github.com/coreybutler/nvm-windows/blob/master/src/nvm.go) are stored under that root.
