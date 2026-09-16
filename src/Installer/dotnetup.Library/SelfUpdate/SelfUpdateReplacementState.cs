// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Records the validated identities of one replacement for conservative rollback.</summary>
internal sealed record SelfUpdateReplacementState(string BackupPath, string OriginalIdentity, string ReplacementIdentity);