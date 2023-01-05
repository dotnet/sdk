// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using VerifyTests.DiffPlex;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    public class VerifySettingsFixture : IDisposable
    {
        private static readonly Lazy<bool> Called = new Lazy<bool>(() =>
        {
            DerivePathInfo(
               (_, _, type, method) => new(
                   directory: "Approvals",
                   typeName: type.Name,
                   methodName: method.Name));

            // Customize diff output of verifier
            VerifyDiffPlex.Initialize(OutputType.Compact);

            return true;
        });

        public VerifySettingsFixture() => _ = Called.Value;

        public void Dispose() { }
    }
}
