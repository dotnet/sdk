﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using VerifyTests.DiffPlex;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public class VerifySettingsFixture : IDisposable
    {
        private static bool _called;

        public VerifySettingsFixture()
        {
            if (_called)
            {
                return;
            }
            _called = true;
            Verifier.DerivePathInfo(
                (_, _, type, method) => new(
                    directory: "Approvals",
                    typeName: type.Name,
                    methodName: method.Name));

            // Customize diff output of verifier
            VerifyDiffPlex.Initialize(OutputType.Compact);
        }

        public void Dispose() { }
    }
}
