// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi.Tests
{
    [TestClass]
    public class EventArgsTests
    {
        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void ItParsesProgressMessageFields()
        {
            ProgressEventArgs e = new("1: 2 2: 4 3: 6 4: 9", InstallMessage.PROGRESS, 0);

            Assert.HasCount(4, e.Fields);
            Assert.AreEqual(2, e.Fields[0]);
            Assert.AreEqual(ProgressType.ProgressReport, e.ProgressType);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void ItParsesActionStartMessageFields()
        {
            ActionStartEventArgs e = new("Action 20:08:24: ProcessComponents. Updating component registration",
                InstallMessage.ACTIONSTART, 0);

            Assert.AreEqual("20:08:24", e.ActionTime);
            Assert.AreEqual("ProcessComponents", e.ActionName);
            Assert.AreEqual("Updating component registration", e.ActionDescription);
        }
    }
}
