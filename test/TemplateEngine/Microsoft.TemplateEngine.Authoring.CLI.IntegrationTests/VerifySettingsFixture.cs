// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using VerifyTests.DiffPlex;

namespace Microsoft.TemplateEngine.Authoring.CLI.IntegrationTests
{
    public class VerifySettingsFixture : IDisposable
    {
        private static bool s_called;

        public VerifySettingsFixture()
        {
            if (s_called)
            {
                return;
            }
            s_called = true;
            DerivePathInfo(
                (_, _, type, method) => new(
                    directory: "Snapshots",
                    typeName: type.Name,
                    methodName: method.Name));

            // Customize diff output of verifier
            VerifyDiffPlex.Initialize(OutputType.Compact);
        }

        public void Dispose() { }
    }
}
