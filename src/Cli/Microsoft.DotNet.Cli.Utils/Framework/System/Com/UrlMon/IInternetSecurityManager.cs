// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.System.Com.Urlmon;

internal partial struct IInternetSecurityManager : IComIID
{
    readonly Guid IComIID.Guid => IID_Guid;
}
