// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
{
    /// <summary>
    /// The browser only trusts the <c>dotnet watch</c> provider whose public key the application
    /// pinned at build time, so the build owns the key pair. These tests pin the two properties the
    /// rest of the design depends on: the pair is reused whenever it is usable, so that a rebuild
    /// during a watch session does not rotate the key out from under a running browser, and it is
    /// regenerated whenever it is not, so that a watch session can never be started against a key
    /// the provider cannot load.
    /// </summary>
    [TestClass]
    public class EnsureDotNetWatchBrowserToolsKeyTest
    {
        private string _directory;
        private string _publicKeyPath;
        private string _privateKeyPath;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "swa-browser-tools-key", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _publicKeyPath = Path.Combine(_directory, "browser-tools-key.public.json");
            _privateKeyPath = Path.Combine(_directory, "browser-tools-key.private.json");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private EnsureDotNetWatchBrowserToolsKey CreateTask() => new()
        {
            BuildEngine = Mock.Of<IBuildEngine>(),
            PublicKeyPath = _publicKeyPath,
            PrivateKeyPath = _privateKeyPath,
        };

        [TestMethod]
        public void CreatesAKeyPairWhenNoneExists()
        {
            var task = CreateTask();

            Assert.IsTrue(task.Execute());
            Assert.IsTrue(task.Regenerated);
            task.PublicKey.Should().NotBeNullOrEmpty();

            new FileInfo(_publicKeyPath).Should().Exist();
            new FileInfo(_privateKeyPath).Should().Exist();
        }

        /// <summary>
        /// The public half is pinned into an application asset, so it has to be exactly the base64
        /// X.509 SubjectPublicKeyInfo that the browser's crypto.subtle.importKey('spki', ...) call
        /// expects, and it has to be the key the private half describes.
        /// </summary>
        [TestMethod]
        public void PublicKeyIsTheSubjectPublicKeyInfoOfThePrivateKey()
        {
            var task = CreateTask();
            Assert.IsTrue(task.Execute());

            using var publicDocument = JsonDocument.Parse(File.ReadAllBytes(_publicKeyPath));
            Assert.AreEqual(1, publicDocument.RootElement.GetProperty("version").GetInt32());
            Assert.AreEqual("RSA-OAEP-SHA256", publicDocument.RootElement.GetProperty("algorithm").GetString());
            Assert.AreEqual("SubjectPublicKeyInfo", publicDocument.RootElement.GetProperty("format").GetString());
            Assert.AreEqual(task.PublicKey, publicDocument.RootElement.GetProperty("publicKey").GetString());

            using var rsa = RSA.Create();
            rsa.ImportParameters(ReadPrivateKeyParameters());

            Assert.AreEqual(Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()), task.PublicKey);
            Assert.AreEqual(2048, rsa.KeySize);
        }

        /// <summary>
        /// The private half is the provider's identity. It must never be reported as a task output
        /// or leak into the document that gets pinned into the application.
        /// </summary>
        [TestMethod]
        public void PublicDocumentContainsNoPrivateMaterial()
        {
            var task = CreateTask();
            Assert.IsTrue(task.Execute());

            var publicContent = File.ReadAllText(_publicKeyPath);
            using var privateDocument = JsonDocument.Parse(File.ReadAllBytes(_privateKeyPath));
            var parameters = privateDocument.RootElement.GetProperty("parameters");

            foreach (var name in new[] { "d", "p", "q", "dp", "dq", "inverseQ" })
            {
                var component = parameters.GetProperty(name).GetString();
                component.Should().NotBeNullOrEmpty();
                publicContent.Should().NotContain(component);
                task.PublicKey.Should().NotContain(component);
            }
        }

        /// <summary>
        /// A rebuild during a watch session must not rotate the key: the running browser already
        /// pinned the previous one, and rewriting the file would also invalidate every static web
        /// asset computed from the generated configuration module.
        /// </summary>
        [TestMethod]
        public void ReusesAValidMatchingPair()
        {
            var first = CreateTask();
            Assert.IsTrue(first.Execute());

            var publicContent = File.ReadAllBytes(_publicKeyPath);
            var privateContent = File.ReadAllBytes(_privateKeyPath);

            var second = CreateTask();
            Assert.IsTrue(second.Execute());

            Assert.IsFalse(second.Regenerated);
            Assert.AreEqual(first.PublicKey, second.PublicKey);
            Assert.AreSequenceEqual(publicContent, File.ReadAllBytes(_publicKeyPath));
            Assert.AreSequenceEqual(privateContent, File.ReadAllBytes(_privateKeyPath));
        }

        [TestMethod]
        [DataRow(true, false, DisplayName = "Public key file removed")]
        [DataRow(false, true, DisplayName = "Private key file removed")]
        [DataRow(true, true, DisplayName = "Both files removed, as after a clean")]
        public void RegeneratesWhenAHalfIsMissing(bool deletePublic, bool deletePrivate)
        {
            var first = CreateTask();
            Assert.IsTrue(first.Execute());

            if (deletePublic)
            {
                File.Delete(_publicKeyPath);
            }

            if (deletePrivate)
            {
                File.Delete(_privateKeyPath);
            }

            var second = CreateTask();
            Assert.IsTrue(second.Execute());

            Assert.IsTrue(second.Regenerated);
            Assert.AreNotEqual(first.PublicKey, second.PublicKey);
            AssertPairIsConsistent(second.PublicKey);
        }

        [TestMethod]
        [DataRow("not json at all")]
        [DataRow("{}")]
        [DataRow("[]")]
        [DataRow("{ \"version\": 1 }")]
        public void RegeneratesWhenTheDocumentIsMalformed(string content)
        {
            var first = CreateTask();
            Assert.IsTrue(first.Execute());

            File.WriteAllText(_privateKeyPath, content);

            var second = CreateTask();
            Assert.IsTrue(second.Execute());

            Assert.IsTrue(second.Regenerated);
            Assert.AreNotEqual(first.PublicKey, second.PublicKey);
            AssertPairIsConsistent(second.PublicKey);
        }

        /// <summary>
        /// A document written by an older SDK, or one that names a different algorithm, has to be
        /// replaced rather than misinterpreted.
        /// </summary>
        [TestMethod]
        [DataRow("version", "2")]
        [DataRow("algorithm", "\"RSA-OAEP-SHA1\"")]
        [DataRow("format", "\"Pkcs8\"")]
        public void RegeneratesWhenTheDocumentHeaderIsNotSupported(string property, string rawValue)
        {
            var first = CreateTask();
            Assert.IsTrue(first.Execute());

            var content = File.ReadAllText(_publicKeyPath);
            using (var document = JsonDocument.Parse(content))
            {
                var original = document.RootElement.GetProperty(property).GetRawText();
                content = content.Replace($"\"{property}\": {original}", $"\"{property}\": {rawValue}");
            }

            File.WriteAllText(_publicKeyPath, content);

            var second = CreateTask();
            Assert.IsTrue(second.Execute());

            Assert.IsTrue(second.Regenerated);
            AssertPairIsConsistent(second.PublicKey);
        }

        /// <summary>
        /// Two halves that do not belong to each other would let the build pin a key the provider
        /// cannot use, which would fail the watch launch. Both the recorded value and the key
        /// derived from the private components are checked.
        /// </summary>
        [TestMethod]
        public void RegeneratesWhenTheHalvesDoNotMatch()
        {
            var first = CreateTask();
            Assert.IsTrue(first.Execute());

            var mismatchedPrivate = File.ReadAllText(_privateKeyPath);
            File.Delete(_publicKeyPath);
            File.Delete(_privateKeyPath);

            var other = CreateTask();
            Assert.IsTrue(other.Execute());

            // Keep the second public half but restore the first private half.
            File.WriteAllText(_privateKeyPath, mismatchedPrivate);

            var third = CreateTask();
            Assert.IsTrue(third.Execute());

            Assert.IsTrue(third.Regenerated);
            Assert.AreNotEqual(first.PublicKey, third.PublicKey);
            Assert.AreNotEqual(other.PublicKey, third.PublicKey);
            AssertPairIsConsistent(third.PublicKey);
        }

        /// <summary>
        /// The recorded public key is not trusted on its own: a document whose header and recorded
        /// key agree but whose private components describe a different key still has to be replaced.
        /// </summary>
        [TestMethod]
        public void RegeneratesWhenThePrivateComponentsDescribeADifferentKey()
        {
            var first = CreateTask();
            Assert.IsTrue(first.Execute());

            var recordedPublicKey = first.PublicKey;

            using var otherKey = RSA.Create(2048);
            var otherParameters = otherKey.ExportParameters(includePrivateParameters: true);

            // Same header and same recorded public key, different private components.
            File.WriteAllText(_privateKeyPath, $$"""
                {
                  "version": 1,
                  "algorithm": "RSA-OAEP-SHA256",
                  "format": "RSAParameters",
                  "publicKey": "{{recordedPublicKey}}",
                  "parameters": {
                    "modulus": "{{Convert.ToBase64String(otherParameters.Modulus)}}",
                    "exponent": "{{Convert.ToBase64String(otherParameters.Exponent)}}",
                    "d": "{{Convert.ToBase64String(otherParameters.D)}}",
                    "p": "{{Convert.ToBase64String(otherParameters.P)}}",
                    "q": "{{Convert.ToBase64String(otherParameters.Q)}}",
                    "dp": "{{Convert.ToBase64String(otherParameters.DP)}}",
                    "dq": "{{Convert.ToBase64String(otherParameters.DQ)}}",
                    "inverseQ": "{{Convert.ToBase64String(otherParameters.InverseQ)}}"
                  }
                }
                """);

            var second = CreateTask();
            Assert.IsTrue(second.Execute());

            Assert.IsTrue(second.Regenerated);
            Assert.AreNotEqual(recordedPublicKey, second.PublicKey);
            AssertPairIsConsistent(second.PublicKey);
        }

        [TestMethod]
        public void CreatesTheIntermediateDirectoryWhenItIsMissing()
        {
            var nested = Path.Combine(_directory, "nested", "dotnet-watch");
            var task = new EnsureDotNetWatchBrowserToolsKey
            {
                BuildEngine = Mock.Of<IBuildEngine>(),
                PublicKeyPath = Path.Combine(nested, "browser-tools-key.public.json"),
                PrivateKeyPath = Path.Combine(nested, "browser-tools-key.private.json"),
            };

            Assert.IsTrue(task.Execute());
            new FileInfo(task.PublicKeyPath).Should().Exist();
            new FileInfo(task.PrivateKeyPath).Should().Exist();
        }

        private RSAParameters ReadPrivateKeyParameters()
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(_privateKeyPath));
            var parameters = document.RootElement.GetProperty("parameters");

            byte[] Read(string name) => Convert.FromBase64String(parameters.GetProperty(name).GetString());

            return new RSAParameters
            {
                Modulus = Read("modulus"),
                Exponent = Read("exponent"),
                D = Read("d"),
                P = Read("p"),
                Q = Read("q"),
                DP = Read("dp"),
                DQ = Read("dq"),
                InverseQ = Read("inverseQ"),
            };
        }

        private void AssertPairIsConsistent(string expectedPublicKey)
        {
            using var publicDocument = JsonDocument.Parse(File.ReadAllBytes(_publicKeyPath));
            Assert.AreEqual(expectedPublicKey, publicDocument.RootElement.GetProperty("publicKey").GetString());

            using var privateDocument = JsonDocument.Parse(File.ReadAllBytes(_privateKeyPath));
            Assert.AreEqual(expectedPublicKey, privateDocument.RootElement.GetProperty("publicKey").GetString());

            using var rsa = RSA.Create();
            rsa.ImportParameters(ReadPrivateKeyParameters());
            Assert.AreEqual(expectedPublicKey, Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));
        }
    }
}
