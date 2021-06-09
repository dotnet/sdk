// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Xunit;

namespace Microsoft.DotNet.ValidationSuppression.Tests
{
    public class SuppressionEngineTests
    {
        [Fact]
        public void AddingASuppressionTwiceDoesntThrow()
        {
            var testEngine = SuppressionEngine.Create();
            AddSuppression(testEngine);
            AddSuppression(testEngine);

            static void AddSuppression(SuppressionEngine testEngine) => testEngine.AddSuppression("PKG004", "A.B()", "ref/net6.0/mylib.dll", "lib/net6.0/mylib.dll");

        }

        [Fact]
        public void SuppressionEngineCanParseInputSuppressionFile()
        {
            TestSuppressionEngine testEngine = TestSuppressionEngine.CreateTestSuppressionEngine();

            // Parsed the right ammount of suppressions
            Assert.Equal(10, testEngine.GetSuppressionCount());

            // Test IsErrorSuppressed string overload.
            Assert.True(testEngine.IsErrorSuppressed("CP0001", "T:A.B", "ref/netstandard2.0/tempValidation.dll", "lib/net6.0/tempValidation.dll"));
            Assert.False(testEngine.IsErrorSuppressed("CP0001", "T:A.C", "ref/netstandard2.0/tempValidation.dll", "lib/net6.0/tempValidation.dll"));
            Assert.False(testEngine.IsErrorSuppressed("CP0001", "T:A.B", "lib/netstandard2.0/tempValidation.dll", "lib/net6.0/tempValidation.dll"));
            Assert.True(testEngine.IsErrorSuppressed("PKV004", ".netframework,Version=v4.8"));
            Assert.False(testEngine.IsErrorSuppressed(string.Empty, string.Empty));
            Assert.False(testEngine.IsErrorSuppressed("PKV004", ".netframework,Version=v4.8", "lib/net6.0/mylib.dll"));
            Assert.False(testEngine.IsErrorSuppressed("PKV004", ".NETStandard,Version=v2.0"));

            // Test IsErrorSuppressed Suppression overload.
            Assert.True(testEngine.IsErrorSuppressed(new Suppression
            {
                DiagnosticId = "CP0001",
                Target = "T:A.B",
                Left = "ref/netstandard2.0/tempValidation.dll",
                Right = "lib/net6.0/tempValidation.dll"
            }));
        }

        [Fact]
        public void SuppressionEngineDoesNotNeedFileToBeCreated()
        {
            SuppressionEngine engine = SuppressionEngine.CreateFromSuppressionFile("AFileThatDoesNotExist.xml");
        }

        [Fact]
        public void SuppressionEngineSuppressionsRoundTrip()
        {
            string output = string.Empty;
            TestSuppressionEngine engine = TestSuppressionEngine.CreateTestSuppressionEngine(
            (stream) =>
            {
                stream.Position = 0;
                using StreamReader reader = new StreamReader(stream);
                output = reader.ReadToEnd();
            });
            engine.WriteSuppressionsToFile("DummyFile");

            Assert.True(StringComparer.OrdinalIgnoreCase.Equals(engine.sampleSuppressionFile.Trim(), output.Trim()));

        }

        [Fact]
        public void SuppressionEngineSupportsGlobalCompare()
        {
            SuppressionEngine engine = SuppressionEngine.Create();
            // Engine has a suppression with no left and no right. This should be treated global for any left and any right.
            engine.AddSuppression("CP0001", "T:A.B");

            Assert.True(engine.IsErrorSuppressed("CP0001", "T:A.B", "ref/net6.0/myLib.dll", "lib/net6.0/myLib.dll"));
        }
    }

    public class TestSuppressionEngine : SuppressionEngine
    {
        private MemoryStream _stream;
        private StreamWriter _writer;
        public readonly string sampleSuppressionFile = @"<?xml version=""1.0""?>
<ArrayOfSuppression xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:A.B</Target>
    <Left>ref/netstandard2.0/tempValidation.dll</Left>
    <Right>lib/net6.0/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0002</DiagnosticId>
    <Target>M:tempValidation.Class1.Bar(System.Int32)</Target>
    <Left>ref/netstandard2.0/tempValidation.dll</Left>
    <Right>lib/net6.0/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0002</DiagnosticId>
    <Target>M:tempValidation.Class1.SomeOtherGenericMethod``1(``0)</Target>
    <Left>ref/netstandard2.0/tempValidation.dll</Left>
    <Right>lib/net6.0/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0002</DiagnosticId>
    <Target>M:tempValidation.Class1.SomeNewBreakingChange</Target>
    <Left>ref/netstandard2.0/tempValidation.dll</Left>
    <Right>lib/net6.0/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:tempValidation.SomeGenericType`1</Target>
    <Left>ref/netstandard2.0/tempValidation.dll</Left>
    <Right>lib/net6.0/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:A</Target>
    <Left>[tempvalidation.1.0.0]ref/netstandard1.3/tempValidation.dll</Left>
    <Right>[tempValidation.2.0.0]ref/netstandard1.3/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:tempValidation.Class1</Target>
    <Left>[tempvalidation.1.0.0]ref/netstandard1.3/tempValidation.dll</Left>
    <Right>[tempValidation.2.0.0]ref/netstandard1.3/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:A</Target>
    <Left>[tempvalidation.1.0.0]lib/netstandard1.3/tempValidation.dll</Left>
    <Right>[tempValidation.2.0.0]lib/netstandard1.3/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>CP0001</DiagnosticId>
    <Target>T:tempValidation.Class1</Target>
    <Left>[tempvalidation.1.0.0]lib/netstandard1.3/tempValidation.dll</Left>
    <Right>[tempValidation.2.0.0]lib/netstandard1.3/tempValidation.dll</Right>
  </Suppression>
  <Suppression>
    <DiagnosticId>PKV004</DiagnosticId>
    <Target>.NETFramework,Version=v4.8</Target>
  </Suppression>
</ArrayOfSuppression>";

        private MemoryStream _outputStream = new MemoryStream();
        private Action<Stream> _callback;

        public TestSuppressionEngine(string baselineFile, Action<Stream> callback)
            : base(baselineFile)
        {
            if (callback == null)
            {
                callback = (s) => { };
            }
            _callback = callback;
        }

        public static TestSuppressionEngine CreateTestSuppressionEngine(Action<Stream> callback = null)
            => new TestSuppressionEngine("NonExistentFile.xml", callback);

        public int GetSuppressionCount() => _validationSuppressions.Count;

        protected override Stream GetReadableStream(string baselineFile)
        {
            // Not Disposing stream since it will be disposed by caller.
            _stream = new MemoryStream();
            _writer = new StreamWriter(_stream);
            _writer.Write(sampleSuppressionFile);
            _writer.Flush();
            _stream.Position = 0;
            return _stream;
        }

        protected override Stream GetWritableStream(string validationSuppressionFile) => _outputStream;

        protected override void AfterWrittingSuppressionsCallback(Stream stream) => _callback(stream);
    }
}
