// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.Sdk.Publish.Tasks;
using Microsoft.NET.Sdk.Publish.Tasks.Tests;

// Some of the tests Copied from https://raw.githubusercontent.com/aspnet/IISIntegration/50f066579a96c6f2b2a4c47524c684e1ef3dfdf0/test/Microsoft.AspNetCore.Server.IISIntegration.Tools.Tests/WebConfigTransformFacts.cs

namespace Microsoft.Net.Sdk.Publish.Tasks.Tests
{
    [TestClass]
    public class WebConfigTransformTests
    {
        [TestMethod]
        public void WebConfigTransform_creates_new_config_if_one_does_not_exist()
        {
            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate,
                    WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)));

            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplatePortable,
                    WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: false, extension: null, aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)));
        }

        [TestMethod]
        public void WebConfigTransform_creates_ProcessPath_WithCorrectExtension()
        {
            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate,
                    WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)));

            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplateWithOutExe,
                    WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: null, aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)));
        }

        [TestMethod]
        public void WebConfigTransform_creates_new_config_if_one_has_unexpected_format()
        {
            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate,
                WebConfigTransform.Transform(XDocument.Parse("<unexpected />"), "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)));
        }

        [TestMethod]
        [DataRow(new object[] { new[] { "system.webServer" } })]
        [DataRow(new object[] { new[] { "add" } })]
        [DataRow(new object[] { new[] { "handlers" } })]
        [DataRow(new object[] { new[] { "aspNetCore" } })]
        [DataRow(new object[] { new[] { "environmentVariables" } })]
        [DataRow(new object[] { new[] { "environmentVariable" } })]
        [DataRow(new object[] { new[] { "handlers", "aspNetCore", "environmentVariables" } })]
        public void WebConfigTransform_adds_missing_elements(string[] elementNames)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            foreach (var elementName in elementNames)
            {
                input.Descendants(elementName).Remove();
            }

            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate,
                WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)));
        }

        [TestMethod]
        [DataRow("add", "path", "test")]
        [DataRow("add", "verb", "test")]
        [DataRow("add", "modules", "mods")]
        [DataRow("add", "resourceType", "Either")]
        [DataRow("aspNetCore", "stdoutLogEnabled", "true")]
        [DataRow("aspNetCore", "startupTimeLimit", "1200")]
        [DataRow("aspNetCore", "arguments", "arg1")]
        [DataRow("aspNetCore", "stdoutLogFile", "logfile")]
        public void WebConfigTransform_wont_override_custom_values(string elementName, string attributeName, string attributeValue)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants(elementName).Single().SetAttributeValue(attributeName, attributeValue);

            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.AreEqual(attributeValue, (string)output.Descendants(elementName).Single().Attribute(attributeName));
        }


        [TestMethod]
        [DataRow("aspNetCore", "hostingModel", "inprocess")]
        [DataRow("aspNetCore", "hostingModel", "InProcess")]
        [DataRow("aspNetCore", "hostingModel", "outofprocess")]
        [DataRow("aspNetCore", "hostingModel", "OutOfProcess")]
        public void WebConfigTransform_will_UseHostingModel_FromWebConfigIfPresent(string elementName, string attributeName, string attributeValue)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants(elementName).Single().SetAttributeValue(attributeName, attributeValue);

            var guid = Guid.NewGuid();
            string projectPath = Path.Combine(Path.GetTempPath(), guid.ToString(), "sample.csproj");
            string projectDirectory = Path.GetDirectoryName(projectPath);
            if (!Directory.Exists(projectDirectory))
            {
                Directory.CreateDirectory(projectDirectory);
            }
            string webConfigPath = Path.Combine(projectDirectory, "web.config");
            File.WriteAllText(webConfigPath, input.ToString());

            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: "foo", environmentName: null, projectFullPath: projectPath);
            Assert.AreEqual(attributeValue, (string)output.Descendants(elementName).Single().Attribute(attributeName));
        }

        [TestMethod]
        [DataRow("add", "modules", "AspNetCoreModuleV2")]
        [DataRow("add", "modules", "AspNetCoreModule")]
        public void WebConfigTransform_UsesAspNetCoreHostingVersion_ForHostingModule(string elementName, string attributeName, string attributeValue)
        {
            var output = WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: attributeValue, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.AreEqual(attributeValue, (string)output.Descendants(elementName).Single().Attribute(attributeName));
        }

        [TestMethod]
        [DataRow("add", "modules", "AspNetCoreModuleV2")]
        [DataRow("add", "modules", "AspNetCoreModule")]
        [DataRow("add", "modules", "UnKnownValue")]
        public void WebConfigTransform_UsesAspNetCoreHostingVersion_FromWebConfigIfPresent(string elementName, string attributeName, string attributeValue)
        {
            var output = WebConfigTransform.Transform(WebConfigTransformTemplates.WebConfigTemplate, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: attributeValue, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.AreEqual("AspNetCoreModule", (string)output.Descendants(elementName).Single().Attribute(attributeName));
        }

        [TestMethod]
        [DataRow("aspNetCore", "hostingModel", "outofprocess")]
        [DataRow("aspNetCore", "hostingModel", "OutOfProcess")]
        public void WebConfigTransform_UsingAspNetCoreModule_SupportsOutOfProc(string elementName, string attributeName, string attributeValue)
        {
            // Template uses AspNetCoreModuleV1
            var input = WebConfigTransformTemplates.WebConfigTemplate;

            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: attributeValue, environmentName: null, projectFullPath: null);
            Assert.AreEqual(attributeValue, (string)output.Descendants(elementName).Single().Attribute(attributeName));
        }

        [TestMethod]
        [DataRow("inprocess")]
        [DataRow("InProcess")]
        public void WebConfigTransform_Throws_ForInValidHostingModel(string attributeValue)
        {
            // Template uses AspNetCoreModuleV1
            var input = WebConfigTransformTemplates.WebConfigTemplate;

            Assert.ThrowsExactly<Exception>(() => WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: attributeValue, environmentName: null, projectFullPath: null));
        }

        [TestMethod]
        [DataRow("aspNetCore", "hostingModel", "inprocess", "AspNetCoreModuleV2")]
        [DataRow("aspNetCore", "hostingModel", "InProcess", "AspNetCoreModuleV2")]
        [DataRow("aspNetCore", "hostingModel", "outofprocess", "AspNetCoreModuleV2")]
        [DataRow("aspNetCore", "hostingModel", "OutOfProcess", "AspNetCoreModuleV2")]
        [DataRow("aspNetCore", "hostingModel", "outofprocess", "AspNetCoreModule")]
        [DataRow("aspNetCore", "hostingModel", "OutOfProcess", "AspNetCoreModule")]
        public void WebConfigTransform_UsesAspNetCoreHostingModelValue_ForHostingModel(string elementName, string attributeName, string attributeValue, string aspNetCoreModuleName)
        {
            var output = WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: aspNetCoreModuleName, aspNetCoreHostingModel: attributeValue, environmentName: null, projectFullPath: null);
            Assert.AreEqual(attributeValue, (string)output.Descendants(elementName).Single().Attribute(attributeName));
        }

        [TestMethod]
        [DataRow("foo")]
        public void WebConfigTransform_Throws_IfHostingModelValueIsUndefined(string attributeValue)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;

            Assert.ThrowsExactly<Exception>(() => WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: attributeValue, environmentName: null, projectFullPath: null));
        }

        [TestMethod]
        [DataRow("aspNetCore", "hostingModel", "")]
        public void WebConfigTransform_DoesNotSet_HostingModelIfEmpty(string elementName, string attributeName, string attributeValue)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;

            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: attributeValue, environmentName: null, projectFullPath: null);
            Assert.IsNull((string)output.Descendants(elementName).Single().Attribute(attributeName));
        }

        [TestMethod]
        public void WebConfigTransform_will_append_Env_IfPassed()
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;

            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: "Production", projectFullPath: null);
            Assert.IsTrue(XNode.DeepEquals(output, WebConfigTransformTemplates.WebConfigTemplateWithEnvironmentVariable));
        }

        [TestMethod]
        public void WebConfigTransform_will_Override_Env_IfUpdated()
        {
            var input = WebConfigTransformTemplates.WebConfigTemplateWithEnvironmentVariable;

            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: "Staging", projectFullPath: null);
            Assert.AreEqual("Staging", (string)output.Descendants("environmentVariable").Single().Attribute("value"));
        }


        private static readonly List<object[]> testData = new()
        {
            new object[] {new XDocument(WebConfigTransformTemplates.WebConfigTemplateWithoutLocation) },
            new object[] {new XDocument(WebConfigTransformTemplates.WebConfigTemplateWithNonRelevantLocationFirst) },
            new object[] {new XDocument(WebConfigTransformTemplates.WebConfigTemplateWithNonRelevantLocationLast) },
            new object[] {new XDocument(WebConfigTransformTemplates.WebConfigTemplateWithRelevantLocationFirst) },
            new object[] {new XDocument(WebConfigTransformTemplates.WebConfigTemplateWithRelevantLocationLast) },
        };

        public static IEnumerable<object[]> TemplatesToTest
        {
            get { return testData; }
        }

        [TestMethod]
        [DynamicData(nameof(TemplatesToTest))]
        public void WebConfigTransform_HandlesLocations_Correctly(XDocument template)
        {
            var input = template;

            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.IsTrue(XNode.DeepEquals(output, template));
        }


        [TestMethod]
        public void WebConfigTransform_overwrites_processPath()
        {
            var newProcessPath =
                (string)WebConfigTransform.Transform(WebConfigTransformTemplates.WebConfigTemplate, "app.exe", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants("aspNetCore").Single().Attribute("processPath");

            Assert.AreEqual(@".\app.exe", newProcessPath);
        }

        [TestMethod]
        public void WebConfigTransform_fixes_aspnetcore_casing()
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants("add").Single().SetAttributeValue("name", "aspnetcore");

            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate,
                WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)));
        }

        [TestMethod]
        public void WebConfigTransform_does_not_remove_children_of_aspNetCore_element()
        {
            var envVarElement =
                new XElement("environmentVariable", new XAttribute("name", "ENVVAR"), new XAttribute("value", "123"));

            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants("aspNetCore").Single().Add(envVarElement);

            Assert.IsTrue(XNode.DeepEquals(envVarElement,
                WebConfigTransform.Transform(input, "app.exe", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: "Test", projectFullPath: null)
                    .Descendants("environmentVariable").SingleOrDefault(e => (string)e.Attribute("name") == "ENVVAR")));

            var output = WebConfigTransform.Transform(input, "app.exe", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: "Test", projectFullPath: null);

            Assert.AreEqual("Test", (string)output.Descendants("environmentVariable").SingleOrDefault(e => (string)e.Attribute("name") == "ASPNETCORE_ENVIRONMENT").Attribute("value"));
        }

        [TestMethod]
        public void WebConfigTransform_adds_stdoutLogEnabled_if_attribute_is_missing()
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants("aspNetCore").Attributes("stdoutLogEnabled").Remove();

            Assert.AreEqual(
                "false",
                (string)WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants().Attributes("stdoutLogEnabled").Single());
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("false")]
        [DataRow("true")]
        public void WebConfigTransform_adds_stdoutLogFile_if_attribute_is_missing(string stdoutLogFile)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;

            var aspNetCoreElement = input.Descendants("aspNetCore").Single();
            aspNetCoreElement.Attribute("stdoutLogEnabled").Remove();
            if (stdoutLogFile != null)
            {
                aspNetCoreElement.SetAttributeValue("stdoutLogEnabled", stdoutLogFile);
            }

            Assert.AreEqual(
                @".\logs\stdout",
                (string)WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants().Attributes("stdoutLogFile").Single());
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("true")]
        [DataRow("false")]
        public void WebConfigTransform_does_not_change_existing_stdoutLogEnabled(string stdoutLogEnabledValue)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            var aspNetCoreElement = input.Descendants("aspNetCore").Single();

            aspNetCoreElement.SetAttributeValue("stdoutLogFile", "mylog.txt");
            aspNetCoreElement.Attributes("stdoutLogEnabled").Remove();
            if (stdoutLogEnabledValue != null)
            {
                input.Descendants("aspNetCore").Single().SetAttributeValue("stdoutLogEnabled", stdoutLogEnabledValue);
            }

            Assert.AreEqual(
                "mylog.txt",
                (string)WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants().Attributes("stdoutLogFile").Single());
        }

        [TestMethod]
        public void WebConfigTransform_correctly_configures_for_Azure()
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants("aspNetCore").Attributes().Remove();

            var aspNetCoreElement = WebConfigTransform.Transform(input, "test.dll", configureForAzure: true, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                .Descendants("aspNetCore").Single();
            aspNetCoreElement.Elements().Remove();

            Assert.IsTrue(XNode.DeepEquals(
                XDocument.Parse(@"<aspNetCore processPath="".\test.exe"" stdoutLogEnabled=""false""
                    stdoutLogFile=""\\?\%home%\LogFiles\stdout"" />").Root,
                aspNetCoreElement));
        }

        [TestMethod]
        public void WebConfigTransform_overwrites_stdoutLogPath_for_Azure()
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            var output = WebConfigTransform.Transform(input, "test.dll", configureForAzure: true, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);

            Assert.AreEqual(
                @"\\?\%home%\LogFiles\stdout",
                (string)output.Descendants("aspNetCore").Single().Attribute("stdoutLogFile"));
        }

        [TestMethod]
        public void WebConfigTransform_configures_portable_apps_correctly()
        {
            var aspNetCoreElement =
                WebConfigTransform.Transform(WebConfigTransformTemplates.WebConfigTemplate, "test.dll", configureForAzure: false, useAppHost: false, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants("aspNetCore").Single();

            Assert.IsTrue(XNode.DeepEquals(
                XDocument.Parse(@"<aspNetCore processPath=""dotnet"" arguments="".\test.dll"" stdoutLogEnabled=""false""
                     stdoutLogFile="".\logs\stdout"" />").Root,
                aspNetCoreElement));
        }

        [TestMethod]
        public void WebConfigTransform_configures_full_framework_apps_correctly()
        {
            var aspNetCoreElement =
                WebConfigTransform.Transform(WebConfigTransformTemplates.WebConfigTemplate, "test.exe", configureForAzure: false, useAppHost: false, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants("aspNetCore").Single();

            Assert.IsTrue(XNode.DeepEquals(
                XDocument.Parse(@"<aspNetCore processPath="".\test.exe"" stdoutLogEnabled=""false""
                     stdoutLogFile="".\logs\stdout"" />").Root,
                aspNetCoreElement));
        }

        [TestMethod]
        [DataRow("%LAUNCHER_ARGS%", "")]
        [DataRow(" %launcher_ARGS%", "")]
        [DataRow("%LAUNCHER_args% ", "")]
        [DataRow("%LAUNCHER_ARGS% %launcher_args%", "")]
        [DataRow(" %LAUNCHER_ARGS% %launcher_args% ", "")]
        [DataRow(" %launcher_args% -my-switch", "-my-switch")]
        [DataRow("-my-switch %LaUnChEr_ArGs%", "-my-switch")]
        [DataRow("-switch-1 %LAUNCHER_ARGS% -switch-2", "-switch-1  -switch-2")]
        [DataRow("%LAUNCHER_ARGS% -switch %launcher_args%", "-switch")]
        [DataRow("-argFile IISExeLauncherArgs.txt", "")]
        [DataRow(" -argFile IISExeLauncherArgs.txt", "")]
        [DataRow("-argFile IISExeLauncherArgs.txt ", "")]
        [DataRow("-argFile IISExeLauncherArgs.txt -argFile IISExeLauncherArgs.txt", "")]
        [DataRow(" -argFile IISExeLauncherArgs.txt %launcher_args% ", "")]
        [DataRow(" -argFile IISExeLauncherArgs.txt -my-switch", "-my-switch")]
        [DataRow("-my-switch -argFile IISExeLauncherArgs.txt", "-my-switch")]
        [DataRow("%launcher_args% -switch-1 -argFile IISExeLauncherArgs.txt -switch-2", "-switch-1  -switch-2")]
        [DataRow("-argFile IISExeLauncherArgs.txt -switch -argFile IISExeLauncherArgs.txt %launcher_args%", "-switch")]
        public void WebConfigTransform_removes_LAUNCHER_ARGS_from_arguments_for_standalone_apps(string inputArguments, string outputArguments)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants("aspNetCore").Single().SetAttributeValue("arguments", inputArguments);

            var aspNetCoreElement =
                WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants("aspNetCore").Single();

            Assert.AreEqual(outputArguments, (string)aspNetCoreElement.Attribute("arguments"));
        }

        [TestMethod]
        [DataRow("", ".\\myapp.dll")]
        [DataRow("%LAUNCHER_ARGS%", ".\\myapp.dll")]
        [DataRow("%LAUNCHER_ARGS% %launcher_args%", ".\\myapp.dll")]
        [DataRow("-my-switch", ".\\myapp.dll -my-switch")]
        [DataRow(" %launcher_args% -my-switch", ".\\myapp.dll -my-switch")]
        [DataRow("-my-switch %LaUnChEr_ArGs%", ".\\myapp.dll -my-switch")]
        [DataRow("-switch-1 -switch-2", ".\\myapp.dll -switch-1 -switch-2")]
        [DataRow("-switch-1 %LAUNCHER_ARGS% -switch-2", ".\\myapp.dll -switch-1  -switch-2")]
        [DataRow("%LAUNCHER_ARGS% -switch %launcher_args%", ".\\myapp.dll -switch")]
        [DataRow("%LAUNCHER_ARGS% -argFile IISExeLauncherArgs.txt", ".\\myapp.dll")]
        [DataRow("-my-switch %LaUnChEr_ArGs% -argFile iisexelauncherargs.txt", ".\\myapp.dll -my-switch")]
        [DataRow("-argFile iisexelauncherargs.txt", ".\\myapp.dll")]
        [DataRow("-argFile iisexelauncherargs.txt -argFile iisexelauncherargs.txt", ".\\myapp.dll")]
        public void WebConfigTransform_wont_override_existing_args_for_portable_apps(string inputArguments, string outputArguments)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            input.Descendants("aspNetCore").Single().SetAttributeValue("arguments", inputArguments);

            var aspNetCoreElement =
                WebConfigTransform.Transform(input, "myapp.dll", configureForAzure: false, useAppHost: false, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null)
                    .Descendants("aspNetCore").Single();

            Assert.AreEqual(outputArguments, (string)aspNetCoreElement.Attribute("arguments"));
        }


        private bool VerifyMissingElementCreated(params string[] elementNames)
        {
            var input = WebConfigTransformTemplates.WebConfigTemplate;
            foreach (var elementName in elementNames)
            {
                input.Descendants(elementName).Remove();
            }

            return XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate,
                WebConfigTransform.Transform(input, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null));
        }

        [TestMethod]
        [DataRow("66964EC2-712A-451A-AB4F-33F18D8F54F1")]
        [DataRow("  66964EC2-712A-451A-AB4F-33F18D8F54F1  ")]
        [DataRow("{ 66964EC2-712A-451A-AB4F-33F18D8F54F1 }")]
        [DataRow("{66964EC2-712A-451A-AB4F-33F18D8F54F1}")]
        [DataRow("( 66964EC2-712A-451A-AB4F-33F18D8F54F1 )")]
        [DataRow("(66964EC2-712A-451A-AB4F-33F18D8F54F1)")]

        public void WebConfigTransform_Adds_ProjectGuid_IfNotPresent(string projectGuid)
        {
            // Arrange
            XDocument transformedWebConfig = WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate, transformedWebConfig));

            // Act
            XDocument transformedWebConfigWithGuid = WebConfigTransform.AddProjectGuidToWebConfig(transformedWebConfig, projectGuid, false);

            // Assert
            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplateWithProjectGuid, transformedWebConfigWithGuid));
        }

        [TestMethod]
        [DataRow("66964EC2-712A-451A-AB4F-33F18D8F54F1")]
        [DataRow(" 66964EC2-712A-451A-AB4F-33F18D8F54F1 ")]
        [DataRow("{ 66964EC2-712A-451A-AB4F-33F18D8F54F1 }")]
        [DataRow("{66964EC2-712A-451A-AB4F-33F18D8F54F1}")]
        [DataRow("( 66964EC2-712A-451A-AB4F-33F18D8F54F1 )")]
        [DataRow("(66964EC2-712A-451A-AB4F-33F18D8F54F1)")]

        public void WebConfigTransform_Removes_ProjectGuid_IfIgnorePropertyIsSet(string projectGuid)
        {
            // Arrange
            XDocument transformedWebConfig = WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate, transformedWebConfig));
            XDocument transformedWebConfigWithGuid = WebConfigTransform.AddProjectGuidToWebConfig(transformedWebConfig, projectGuid, false);
            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplateWithProjectGuid, transformedWebConfigWithGuid));

            // Act
            transformedWebConfigWithGuid = WebConfigTransform.AddProjectGuidToWebConfig(transformedWebConfig, projectGuid, true);

            //Assert
            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate, transformedWebConfigWithGuid));

        }

        [TestMethod]
        public void WebConfigTransform_DoesNothingWithProjectGuid_IfAbsent()
        {
            // Arrange
            XDocument transformedWebConfig = WebConfigTransform.Transform(null, "test.dll", configureForAzure: false, useAppHost: true, extension: ".exe", aspNetCoreModuleName: null, aspNetCoreHostingModel: null, environmentName: null, projectFullPath: null);
            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate, transformedWebConfig));

            // Act
            XDocument transformedWebConfigWithGuid = WebConfigTransform.AddProjectGuidToWebConfig(transformedWebConfig, null, false);

            // Assert
            Assert.IsTrue(XNode.DeepEquals(WebConfigTransformTemplates.WebConfigTemplate, transformedWebConfigWithGuid));
        }
    }
}
