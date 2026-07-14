// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
#endif
using Microsoft.TemplateEngine.Tests;
using VerifyTests.DiffPlex;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests
{
    public class VerifySettingsFixture
    {
        private static bool s_called;

        public VerifySettingsFixture()
        {
            if (s_called)
            {
                return;
            }
            s_called = true;
            VerifyMSTest.Verifier.DerivePathInfo(
                (_, _, type, method) => new(
                    directory: TestBase.ApprovalsDirectory,
                    typeName: type.Name,
                    methodName: method.Name));

            // Customize diff output of verifier
            VerifyDiffPlex.Initialize(OutputType.Compact);

#if NET
            // The shared TemplateVerifier engine is compiled against the xUnit Verify adapter; route its directory
            // verification to the MSTest adapter so the ambient MSTest test context is used under MTP.
            VerificationEngine.DirectoryVerifier = VerifyMSTest.Verifier.VerifyDirectory;
#endif
        }
    }
}
