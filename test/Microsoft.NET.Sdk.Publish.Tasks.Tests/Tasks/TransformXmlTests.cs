// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.Publish.Tasks.Xdt;

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests.Tasks
{
    [TestClass]
    public class TransformXmlTests
    {
        private XDocument _webConfigTemplate => XDocument.Parse(
@"<configuration>
  <location path=""."" inheritInChildApplications=""false"">
      <system.webServer>
        <handlers>
          <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified""/>
        </handlers>
        <aspNetCore processPath=""dotnet"" arguments="".\test.dll"" stdoutLogEnabled=""false"" stdoutLogFile="".\logs\stdout"">
          <environmentVariables />
        </aspNetCore>
      </system.webServer>
  </location >
</configuration>");

        private XDocument _webConfigTransformRemoveAll => XDocument.Parse(
@"<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
  <location>
      <system.webServer>
        <handlers xdt:Transform=""RemoveAll"" />
        <aspNetCore xdt:Transform=""RemoveAll"" />
      </system.webServer>
  </location >
</configuration>");


        private XDocument _webConfigTransformAdd => XDocument.Parse(
@"<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
  <location>
      <system.webServer>
        <aspNetCore>
          <environmentVariables>
            <environmentVariable name=""MyCustomEnvVariable"" value=""MyCustomValue"" xdt:Transform=""Insert"" />
          </environmentVariables>
        </aspNetCore>
      </system.webServer>
  </location >
</configuration>");


        [TestMethod]
        public void XmlTransform_AppliesRemoveAllTransform()
        {
            // Arrange
            string sourceFile = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            string transformFile = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            string outputFile = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

            try
            {
                _webConfigTemplate.Save(sourceFile);
                _webConfigTransformRemoveAll.Save(transformFile);

                // Act
                TransformXml transformTask = new()
                {
                    Source = sourceFile,
                    Destination = outputFile,
                    Transform = transformFile,
                    SourceRootPath = Path.GetTempPath(),
                    TransformRootPath = Path.GetTempPath(),
                    StackTrace = true
                };

                bool success = transformTask.RunXmlTransform(isLoggingEnabled: false);


                // Assert
                Assert.IsTrue(success);
                Assert.HasCount(1, XDocument.Parse(File.ReadAllText(sourceFile)).Descendants("handlers"));
                Assert.HasCount(1, XDocument.Parse(File.ReadAllText(sourceFile)).Descendants("aspNetCore"));

                Assert.IsEmpty(XDocument.Parse(File.ReadAllText(outputFile)).Descendants("handlers"));
                Assert.IsEmpty(XDocument.Parse(File.ReadAllText(outputFile)).Descendants("aspNetCore"));
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(transformFile);
                File.Delete(outputFile);
            }

        }


        [TestMethod]
        public void XmlTransform_AppliesAdd()
        {
            // Arrange
            string sourceFile = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            string transformFile = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            string outputFile = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

            try
            {
                _webConfigTemplate.Save(sourceFile);
                _webConfigTransformAdd.Save(transformFile);

                // Act
                TransformXml transformTask = new()
                {
                    Source = sourceFile,
                    Destination = outputFile,
                    Transform = transformFile,
                    SourceRootPath = Path.GetTempPath(),
                    TransformRootPath = Path.GetTempPath(),
                    StackTrace = true
                };

                bool success = transformTask.RunXmlTransform(isLoggingEnabled: false);


                // Assert
                Assert.IsTrue(success);
                Assert.IsEmpty(XDocument.Parse(File.ReadAllText(sourceFile)).Descendants("environmentVariable"));
                Assert.HasCount(1, XDocument.Parse(File.ReadAllText(outputFile)).Descendants("environmentVariable"));
            }
            finally
            {
                File.Delete(sourceFile);
                File.Delete(transformFile);
                File.Delete(outputFile);
            }

        }

    }
}
