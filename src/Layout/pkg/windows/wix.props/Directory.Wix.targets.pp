<!-- Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the MIT license. See License.txt in the project root for full license information. -->
<Project>
  <PropertyGroup>
    <!-- DefineConstants values generate preprocessor variables passed to the WiX compiler. -->
    <DefineConstants>$(DefineConstants);ProductVersion={msiVersion}</DefineConstants>
    <DefineConstants>$(DefineConstants);BundleVersion={bundleVersion}</DefineConstants>
  </PropertyGroup>
</Project>
