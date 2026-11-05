// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

/// <summary>
/// Outcome of a child process run by <see cref="ChildProcessRunner"/>. Output is empty when it was not
/// redirected or when the run timed out.
/// </summary>
internal sealed record ChildProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);
