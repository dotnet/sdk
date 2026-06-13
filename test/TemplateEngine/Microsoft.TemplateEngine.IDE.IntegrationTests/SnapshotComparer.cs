// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    /// <summary>
    /// Minimal snapshot comparison helper used to replace the few static <c>Verifier.Verify(...)</c>
    /// calls from <c>Verify.XunitV3</c> in this project. Compares an actual string against the
    /// corresponding <c>{ClassName}.{MethodName}.verified.txt</c> file under the Approvals
    /// directory. Newlines and a leading UTF-8 BOM are normalized to match the behaviour of
    /// Verify's text comparison.
    /// </summary>
    internal static class SnapshotComparer
    {
        public static void AssertEqualToApproval(object testInstance, string actual, [CallerMemberName] string testName = "")
        {
            string className = testInstance.GetType().Name;
            string verifiedPath = Path.Combine(TestBase.ApprovalsDirectory, $"{className}.{testName}.verified.txt");
            Assert.IsTrue(File.Exists(verifiedPath), $"Approval file not found: {verifiedPath}");

            string expected = File.ReadAllText(verifiedPath);
            if (expected.Length > 0 && expected[0] == '\uFEFF')
            {
                expected = expected.Substring(1);
            }

            Assert.AreEqual(Normalize(expected), Normalize(actual));
        }

        private static string Normalize(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n");
    }
}
