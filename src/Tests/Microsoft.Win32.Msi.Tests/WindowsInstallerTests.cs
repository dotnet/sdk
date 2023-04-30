// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.Win32.Msi.Tests
{
    public class WindowsInstallerTests
    {
        [WindowsOnlyTheory]
        [InlineData("", "", Error.INVALID_PARAMETER)]
        [InlineData("{807215B4-F42F-4E5F-BFEE-9817D7F2CEA5}", "ProductVersion", Error.UNKNOWN_PRODUCT)]
        public void InstallProductReturnsAnError(string productCode, string property, uint expectedError)
        {
            uint error = WindowsInstaller.GetProductInfo(productCode, property, out string propertyValue);

            Assert.Equal(error, expectedError);
        }

        [WindowsOnlyTheory]
        [InlineData("", InstallState.INVALIDARG)]
        [InlineData("{807215B4-F42F-4E5F-BFEE-9817D7F2CEA5}", InstallState.UNKNOWN)]
        public void QueryProductStateReturnsAnError(string productCode, InstallState expectedState)
        {
            InstallState state = WindowsInstaller.QueryProduct(productCode);

            Assert.Equal(state, expectedState);
        }
    }
}
