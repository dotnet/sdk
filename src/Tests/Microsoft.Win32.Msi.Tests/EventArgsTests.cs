// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.Win32.Msi.Tests
{
    public class EventArgsTests
    {
        [WindowsOnlyFact]
        public void ItParsesProgressMessageFields()
        {
            ProgressEventArgs e = new("1: 2 2: 4 3: 6 4: 9", InstallMessage.PROGRESS, 0);

            Assert.Equal(4, e.Fields.Length);
            Assert.Equal(2, e.Fields[0]);
            Assert.Equal(ProgressType.ProgressReport, e.ProgressType);
        }

        [WindowsOnlyFact]
        public void ItParsesActionStartMessageFields()
        {
            ActionStartEventArgs e = new("Action 20:08:24: ProcessComponents. Updating component registration",
                InstallMessage.ACTIONSTART, 0);

            Assert.Equal("20:08:24", e.ActionTime);
            Assert.Equal("ProcessComponents", e.ActionName);
            Assert.Equal("Updating component registration", e.ActionDescription);
        }
    }
}
