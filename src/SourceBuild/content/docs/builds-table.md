### Supported Platforms


--------------------------------------------------------------------------------------
| Platform | main<br>(10.0.x&nbsp;Runtime) |
| :--------- | :----------: |
| **Windows x64** | [![][win-x64-badge-main]][win-x64-version-main]<br>[Installer][win-x64-installer-main] - [Checksum][win-x64-installer-checksum-main]<br>[zip][win-x64-zip-main] - [Checksum][win-x64-zip-checksum-main] |
| **Windows x86** | [![][win-x86-badge-main]][win-x86-version-main]<br>[Installer][win-x86-installer-main] - [Checksum][win-x86-installer-checksum-main]<br>[zip][win-x86-zip-main] - [Checksum][win-x86-zip-checksum-main] |
| **Windows arm64** | [![][win-arm64-badge-main]][win-arm64-version-main]<br>[Installer][win-arm64-installer-main] - [Checksum][win-arm64-installer-checksum-main]<br>[zip][win-arm64-zip-main] - [Checksum][win-arm64-zip-checksum-main] |
| **macOS x64** | [![][osx-x64-badge-main]][osx-x64-version-main]<br>[Installer][osx-x64-installer-main] - [Checksum][osx-x64-installer-checksum-main]<br>[tar.gz][osx-x64-targz-main] - [Checksum][osx-x64-targz-checksum-main] |
| **macOS arm64** | [![][osx-arm64-badge-main]][osx-arm64-version-main]<br>[Installer][osx-arm64-installer-main] - [Checksum][osx-arm64-installer-checksum-main]<br>[tar.gz][osx-arm64-targz-main] - [Checksum][osx-arm64-targz-checksum-main] |
| **Linux x64** | [![][linux-badge-main]][linux-version-main]<br>[DEB Installer][linux-DEB-installer-main] - [Checksum][linux-DEB-installer-checksum-main]<br>[RPM Installer][linux-RPM-installer-main] - [Checksum][linux-RPM-installer-checksum-main]<br>_see installer note below_<sup>1</sup><br>[tar.gz][linux-targz-main] - [Checksum][linux-targz-checksum-main] |
| **Linux arm** | [![][linux-arm-badge-main]][linux-arm-version-main]<br>[tar.gz][linux-arm-targz-main] - [Checksum][linux-arm-targz-checksum-main] |
| **Linux arm64** | [![][linux-arm64-badge-main]][linux-arm64-version-main]<br>[tar.gz][linux-arm64-targz-main] - [Checksum][linux-arm64-targz-checksum-main] |
| **Linux-musl-x64** | [![][linux-musl-x64-badge-main]][linux-musl-x64-version-main]<br>[tar.gz][linux-musl-x64-targz-main] - [Checksum][linux-musl-x64-targz-checksum-main] |
| **Linux-musl-arm** | [![][linux-musl-arm-badge-main]][linux-musl-arm-version-main]<br>[tar.gz][linux-musl-arm-targz-main] - [Checksum][linux-musl-arm-targz-checksum-main] |
| **Linux-musl-arm64** | [![][linux-musl-arm64-badge-main]][linux-musl-arm64-version-main]<br>[tar.gz][linux-musl-arm64-targz-main] - [Checksum][linux-musl-arm64-targz-checksum-main] |

Reference notes:
> **1**: Our Debian packages are put together slightly differently than the other OS specific installers. Instead of combining everything, we have separate component packages that depend on each other. If you're installing the SDK from the .deb file (via dpkg or similar), then you'll need to install the corresponding dependencies first:
> * [Host, Host FX Resolver, and Shared Framework](https://github.com/dotnet/runtime/blob/main/docs/project/dogfooding.md#nightly-builds-table)
> * [ASP.NET Core Shared Framework](https://github.com/aspnet/AspNetCore/blob/main/docs/DailyBuilds.md)

[win-x64-badge-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/win_x64_Release_version_badge.svg?no-cache
[win-x64-version-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/productCommit-win-x64.txt
[win-x64-installer-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-win-x64.exe
[win-x64-installer-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-win-x64.exe.sha512
[win-x64-zip-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-win-x64.zip
[win-x64-zip-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-win-x64.zip.sha512

[win-x86-badge-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/win_x86_Release_version_badge.svg?no-cache
[win-x86-version-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/productCommit-win-x86.txt
[win-x86-installer-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-win-x86.exe
[win-x86-installer-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-win-x86.exe.sha512
[win-x86-zip-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-win-x86.zip
[win-x86-zip-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-win-x86.zip.sha512

[osx-x64-badge-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/osx_x64_Release_version_badge.svg?no-cache
[osx-x64-version-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/productCommit-osx-x64.txt
[osx-x64-installer-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-osx-x64.pkg
[osx-x64-installer-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-osx-x64.pkg.sha512
[osx-x64-targz-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-osx-x64.tar.gz
[osx-x64-targz-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-osx-x64.pkg.tar.gz.sha512

[osx-arm64-badge-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/osx_arm64_Release_version_badge.svg?no-cache
[osx-arm64-version-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/productCommit-osx-arm64.txt
[osx-arm64-installer-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-osx-arm64.pkg
[osx-arm64-installer-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-osx-arm64.pkg.sha512
[osx-arm64-targz-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-osx-arm64.tar.gz
[osx-arm64-targz-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-osx-arm64.pkg.tar.gz.sha512

[linux-badge-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/linux_x64_Release_version_badge.svg?no-cache
[linux-version-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/productCommit-linux-x64.txt
[linux-DEB-installer-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-x64.deb
[linux-DEB-installer-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-x64.deb.sha512
[linux-RPM-installer-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-x64.rpm
[linux-RPM-installer-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-x64.rpm.sha512
[linux-targz-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-linux-x64.tar.gz
[linux-targz-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-linux-x64.tar.gz.sha512

[linux-arm-badge-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/linux_arm_Release_version_badge.svg?no-cache
[linux-arm-version-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/productCommit-linux-arm.txt
[linux-arm-targz-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-linux-arm.tar.gz
[linux-arm-targz-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-linux-arm.tar.gz.sha512

[linux-arm64-badge-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/linux_arm64_Release_version_badge.svg?no-cache
[linux-arm64-version-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/productCommit-linux-arm64.txt
[linux-arm64-targz-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-linux-arm64.tar.gz
[linux-arm64-targz-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-linux-arm64.tar.gz.sha512

[rhel-6-badge-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/rhel.6_x64_Release_version_badge.svg?no-cache
[rhel-6-version-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/productCommit-rhel.6-x64.txt
[rhel-6-targz-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-rhel.6-x64.tar.gz
[rhel-6-targz-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-rhel.6-x64.tar.gz.sha512

[linux-musl-x64-badge-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/linux_musl_x64_Release_version_badge.svg?no-cache
[linux-musl-x64-version-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/productCommit-linux-musl-x64.txt
[linux-musl-x64-targz-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-linux-musl-x64.tar.gz
[linux-musl-x64-targz-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-linux-musl-x64.tar.gz.sha512

[linux-musl-arm-badge-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/linux_musl_arm_Release_version_badge.svg?no-cache
[linux-musl-arm-version-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/productCommit-linux-musl-arm.txt
[linux-musl-arm-targz-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-linux-musl-arm.tar.gz
[linux-musl-arm-targz-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-linux-musl-arm.tar.gz.sha512

[linux-musl-arm64-badge-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/linux_musl_arm64_Release_version_badge.svg?no-cache
[linux-musl-arm64-version-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/productCommit-linux-musl-arm64.txt
[linux-musl-arm64-targz-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-linux-musl-arm64.tar.gz
[linux-musl-arm64-targz-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-linux-musl-arm64.tar.gz.sha512

[win-arm64-badge-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/win_arm64_Release_version_badge.svg?no-cache
[win-arm64-version-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/productCommit-win-arm64.txt
[win-arm64-installer-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-win-arm64.exe
[win-arm64-installer-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-win-arm64.exe.sha512
[win-arm64-zip-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-win-arm64.zip
[win-arm64-zip-checksum-main]: https://aka.ms/dotnet/10.0.1xx-ub/daily/dotnet-sdk-win-arm64.zip.sha512