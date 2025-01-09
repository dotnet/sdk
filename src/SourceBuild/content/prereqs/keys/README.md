This directory contains the public key portion of keys used by different
projects so we can public sign (also known as OSS signing) assemblies that need
to be signed to have the correct strong name.

These are used for projects which full sign their assemblies and don't have keys
checked in.

To extract a key, take an existing built binary for a project (e.g. download a
nupkg from NuGet.org and then unpack one of the assemblies from it) and use `sn`:

```
sn -e <path-to-binary> <path-to-snk-to-write>
```
