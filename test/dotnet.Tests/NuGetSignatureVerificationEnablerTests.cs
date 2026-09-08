// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Utils;
using Moq;

namespace Microsoft.DotNet.Tests
{
    [TestClass]
    public class NuGetSignatureVerificationEnablerTests
    {
        private static readonly string FakeFilePath = Path.Combine(Path.GetTempPath(), "file.fake");

        public static IEnumerable<object[]> GetNonFalseValues()
        {
            yield return new object[] { null! };
            yield return new object[] { string.Empty };
            yield return new object[] { "0" };
            yield return new object[] { "1" };
            yield return new object[] { "no" };
            yield return new object[] { "yes" };
            yield return new object[] { "true" };
            yield return new object[] { "TRUE" };
        }

        public static IEnumerable<object[]> GetFalseValues()
        {
            yield return new object[] { "false" };
            yield return new object[] { "FALSE" };
        }

        [TestMethod]
        public void GivenANullForwardingAppThrows()
        {
            ForwardingApp forwardingApp = null!;

            ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
                () => NuGetSignatureVerificationEnabler.ConditionallyEnable(forwardingApp));

            Assert.AreEqual("forwardingApp", exception.ParamName);
        }

        [TestMethod]
        public void GivenANullMSBuildForwardingAppThrows()
        {
            MSBuildForwardingApp forwardingApp = null!;

            ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
                () => NuGetSignatureVerificationEnabler.ConditionallyEnable(forwardingApp));

            Assert.AreEqual("forwardingApp", exception.ParamName);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Linux)]
        [DynamicData(nameof(GetNonFalseValues))]
        public void GivenAForwardingAppAndAnEnvironmentVariableValueThatIsNotFalseSetsTrueOnLinux(string? value)
        {
            Mock<IEnvironmentProvider> environmentProvider = CreateEnvironmentProvider(value);
            ForwardingApp forwardingApp = new(FakeFilePath, Array.Empty<string>());

            NuGetSignatureVerificationEnabler.ConditionallyEnable(forwardingApp, environmentProvider.Object);

            environmentProvider.VerifyAll();

            VerifyEnvironmentVariable(forwardingApp.GetProcessStartInfo(), bool.TrueString);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Linux)]
        [DynamicData(nameof(GetFalseValues))]
        public void GivenAForwardingAppAndAnEnvironmentVariableValueThatIsFalseSetsFalseOnLinux(string value)
        {
            Mock<IEnvironmentProvider> environmentProvider = CreateEnvironmentProvider(value);
            ForwardingApp forwardingApp = new(FakeFilePath, Array.Empty<string>());

            NuGetSignatureVerificationEnabler.ConditionallyEnable(forwardingApp, environmentProvider.Object);

            environmentProvider.VerifyAll();

            VerifyEnvironmentVariable(forwardingApp.GetProcessStartInfo(), bool.FalseString);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Linux)]
        [DynamicData(nameof(GetNonFalseValues))]
        public void GivenAnMSBuildForwardingAppAndAnEnvironmentVariableValueThatIsNotFalseSetsTrueOnLinux(string? value)
        {
            Mock<IEnvironmentProvider> environmentProvider = CreateEnvironmentProvider(value);
            MSBuildForwardingApp forwardingApp = new(Array.Empty<string>(), FakeFilePath);

            NuGetSignatureVerificationEnabler.ConditionallyEnable(forwardingApp, environmentProvider.Object);

            environmentProvider.VerifyAll();

            VerifyEnvironmentVariable(forwardingApp.GetProcessStartInfo(), bool.TrueString);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Linux)]
        [DynamicData(nameof(GetFalseValues))]
        public void GivenAnMSBuildForwardingAppAndAnEnvironmentVariableValueThatIsFalseSetsFalseOnLinux(string value)
        {
            Mock<IEnvironmentProvider> environmentProvider = CreateEnvironmentProvider(value);
            MSBuildForwardingApp forwardingApp = new(Array.Empty<string>(), FakeFilePath);

            NuGetSignatureVerificationEnabler.ConditionallyEnable(forwardingApp, environmentProvider.Object);

            environmentProvider.VerifyAll();

            VerifyEnvironmentVariable(forwardingApp.GetProcessStartInfo(), bool.FalseString);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.OSX)]
        public void GivenAForwardingAppDoesNothingOnMacOs()
        {
            var environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            ForwardingApp forwardingApp = new(FakeFilePath, Array.Empty<string>());

            NuGetSignatureVerificationEnabler.ConditionallyEnable(forwardingApp, environmentProvider.Object);

            environmentProvider.VerifyAll();

            VerifyNoEnvironmentVariable(forwardingApp.GetProcessStartInfo());
        }

        [TestMethod]
        [OSCondition(OperatingSystems.OSX)]
        public void GivenAnMSBuildForwardingAppDoesNothingOnMacOs()
        {
            var environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            MSBuildForwardingApp forwardingApp = new(Array.Empty<string>(), FakeFilePath);

            NuGetSignatureVerificationEnabler.ConditionallyEnable(forwardingApp, environmentProvider.Object);

            environmentProvider.VerifyAll();

            VerifyNoEnvironmentVariable(forwardingApp.GetProcessStartInfo());
        }

        private static Mock<IEnvironmentProvider> CreateEnvironmentProvider(string? value)
        {
            Mock<IEnvironmentProvider> provider = new(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable(NuGetSignatureVerificationEnabler.DotNetNuGetSignatureVerification))
                .Returns(value!);

            return provider;
        }

        private static void VerifyEnvironmentVariable(ProcessStartInfo startInfo, string expectedValue)
        {
            Assert.IsTrue(startInfo.EnvironmentVariables.ContainsKey(NuGetSignatureVerificationEnabler.DotNetNuGetSignatureVerification));
            Assert.AreEqual(expectedValue, startInfo.EnvironmentVariables[NuGetSignatureVerificationEnabler.DotNetNuGetSignatureVerification]);
        }

        private static void VerifyNoEnvironmentVariable(ProcessStartInfo startInfo)
        {
            Assert.IsFalse(startInfo.EnvironmentVariables.ContainsKey(NuGetSignatureVerificationEnabler.DotNetNuGetSignatureVerification));
        }
    }
}
