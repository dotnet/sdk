// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

/// <summary>
/// Recognizes an <c>env script</c> (or legacy <c>print-env-script</c>) invocation from the raw
/// command-line arguments, before the parser or telemetry are set up. The managed shell profile
/// block runs this command on every shell start, so startup can skip work it doesn't need.
/// This only looks at the leading tokens and must not touch the parser or localized strings.
/// </summary>
internal static class EnvScriptInvocation
{
    public static bool IsMatch(IReadOnlyList<string> args) =>
        args.Count > 0 &&
        (args[0] == "print-env-script" || (args.Count > 1 && args[0] == "env" && args[1] == "script"));
}
