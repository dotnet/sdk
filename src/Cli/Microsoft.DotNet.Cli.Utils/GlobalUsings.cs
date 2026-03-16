// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable IDE0005 // Using directive is unnecessary.
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;

global using Microsoft.DotNet.Cli.Utils;

#if !DOTNET_BUILDSOURCEONLY
global using Windows.Win32;
global using Windows.Win32.Foundation;
global using WDK = Windows.Wdk;
#endif

// global using OverloadResolutionPriorityAttribute = System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute;
#pragma warning restore IDE0005
