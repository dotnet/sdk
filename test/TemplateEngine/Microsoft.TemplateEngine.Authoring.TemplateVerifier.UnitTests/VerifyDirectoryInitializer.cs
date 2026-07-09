// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier.UnitTests;

/// <summary>
/// Routes <see cref="VerificationEngine"/>'s snapshot directory verification through the MSTest
/// Verify integration. <see cref="VerificationEngine"/> defaults to the xUnit (v3) verifier, which
/// resolves the test method from xUnit's ambient context. Under MSTest that context is unavailable
/// (the verification would otherwise fail with <c>TestContext.TestMethod is null</c>), so we point
/// it at <c>VerifyMSTest.Verifier.VerifyDirectory</c> which reads the ambient MSTest test context.
/// </summary>
internal static class VerifyDirectoryInitializer
{
#pragma warning disable CA2255 // The ModuleInitializer attribute should not be used in libraries
    [ModuleInitializer]
    internal static void Initialize()
        => VerificationEngine.DirectoryVerifier =
            (path, include, pattern, options, settings, info, fileScrubber, sourceFile)
                => VerifyMSTest.Verifier.VerifyDirectory(path, include, pattern, options, settings, info, fileScrubber, sourceFile);
#pragma warning restore CA2255
}
