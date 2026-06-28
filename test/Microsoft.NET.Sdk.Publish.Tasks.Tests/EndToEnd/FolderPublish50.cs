using System;
using System.IO;

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests.EndToEnd
{
    [TestClass]
    public class FolderPublish31
    {
        public string BaseTestDirectory
        {
            get
            {
                return Path.Combine(AppContext.BaseDirectory, nameof(FolderPublish31));
            }
        }

        public const string DotNetExeName = "dotnet";
        public const string DotNetNewAdditionalArgs = "";
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DataRow("net5.0", "Release", "core")]
        [DataRow("net5.0", "Debug", "core")]
        public void EmptyWebCore(string templateFramework, string configuration, string msBuildType)
        {
            string projectName = $"{nameof(EmptyWebCore)}_{Path.GetRandomFileName()}";

            // Arrange
            string dotNetNewArguments = $"new web --framework {templateFramework} {DotNetNewAdditionalArgs}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);

            // dotnet new
            int? exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true, testContext: TestContext);
            Assert.IsTrue(exitCode.HasValue && exitCode.Value == 0);

            Publish(testFolder, projectName, configuration, msBuildType);
        }


        [TestMethod]
        [DataRow("net5.0", "Release", "core")]
        [DataRow("net5.0", "Debug", "core")]
        public void WebAPICore(string templateFramework, string configuration, string msBuildType)
        {
            string projectName = $"{nameof(WebAPICore)}_{Path.GetRandomFileName()}";

            // Arrange
            string dotNetNewArguments = $"new webapi --framework {templateFramework} {DotNetNewAdditionalArgs}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);
            // dotnet new
            int? exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.IsTrue(exitCode.HasValue && exitCode.Value == 0);

            Publish(testFolder, projectName, configuration, msBuildType, isStandAlone:false, resultUrl:"http://localhost:5000/api/Values");
        }

        [TestMethod]
        [DataRow("net5.0", "Release", "core", "none", "false")]
        [DataRow("net5.0", "Debug", "core", "none", "false")]
        [DataRow("net5.0", "Release", "core", "Individual", "false")]
        [DataRow("net5.0", "Debug", "core", "Individual", "false")]
        [DataRow("net5.0", "Release", "core", "Individual", "true")]
        [DataRow("net5.0", "Debug", "core", "Individual", "true")]
        public void MvcCore(string templateFramework, string configuration, string msBuildType, string auth, string useLocalDB)
        {
            string projectName = $"{nameof(MvcCore)}_{Path.GetRandomFileName()}";

            string additionalOptions = string.Empty;
            // Arrange
            if (bool.TryParse(useLocalDB, out bool localDBBool) && localDBBool)
            {
                additionalOptions = $"--use-local-db";
            }
            string dotNetNewArguments = $"new mvc --framework {templateFramework} --auth {auth} {DotNetNewAdditionalArgs} {additionalOptions}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);

            // dotnet new
            int? exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.IsTrue(exitCode.HasValue && exitCode.Value == 0);

            Publish(testFolder, projectName, configuration, msBuildType);
        }

        [TestMethod]
        [DataRow("net5.0", "Release", "core", "none", "false")]
        [DataRow("net5.0", "Debug", "core", "none", "false")]
        [DataRow("net5.0", "Release", "core", "Individual", "false")]
        [DataRow("net5.0", "Debug", "core", "Individual", "false")]
        [DataRow("net5.0", "Release", "core", "Individual", "true")]
        [DataRow("net5.0", "Debug", "core", "Individual", "true")]
        public void RazorCore(string templateFramework, string configuration, string msBuildType, string auth, string useLocalDB)
        {
            string projectName = $"{nameof(RazorCore)}_{Path.GetRandomFileName()}";

            string additionalOptions = string.Empty;
            // Arrange
            if (bool.TryParse(useLocalDB, out bool localDBBool) && localDBBool)
            {
                additionalOptions = $"--use-local-db";
            }
            string dotNetNewArguments = $"new razor --framework {templateFramework} --auth {auth} {DotNetNewAdditionalArgs} {additionalOptions}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);

            // dotnet new
            int? exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.IsTrue(exitCode.HasValue && exitCode.Value == 0);

            Publish(testFolder, projectName, configuration, msBuildType);
        }

        private void Publish(string testFolder, string projectName, string configuration, string msBuildType, bool isStandAlone = false, string resultUrl = "http://localhost:5000")
        {
            int? exitCode = 0;

            // dotnet restore
            string dotnetRestoreArguments = "restore --source https://pkgs.dev.azure.com/dnceng/public/_packaging/myget-legacy/nuget/v3/index.json --source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json";
            exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotnetRestoreArguments, testFolder, out int? processId2);
            Assert.IsTrue(exitCode.HasValue && exitCode.Value == 0);

            // dotnet build
            string dotnetBuildArguments = "build";
            exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotnetBuildArguments, testFolder, out int? processId3);
            Assert.IsTrue(exitCode.HasValue && exitCode.Value == 0);

            // msbuild publish
            string fileName = "msbuild";
            string publishOutputFolder = Path.Combine("bin", configuration, "PublishOutput");
            string dotnetPublishArguments = $"{projectName}.csproj /p:DeployOnBuild=true /p:Configuration={configuration} /p:PublishUrl={publishOutputFolder}";
            if (string.Equals(msBuildType, "core"))
            {
                dotnetPublishArguments = $"{fileName} {dotnetPublishArguments}";
                fileName = DotNetExeName;
            }
            exitCode = new ProcessWrapper().RunProcess(fileName, dotnetPublishArguments, testFolder, out int? processId4);
            Assert.IsTrue(exitCode.HasValue && exitCode.Value == 0);

            string publishOutputFolderFullPath = Path.Combine(testFolder, publishOutputFolder);

            Assert.IsTrue(File.Exists(Path.Combine(publishOutputFolderFullPath, "web.config")));

            try
            {
                Directory.Delete(testFolder, true);
            }
            catch { }
        }
    }
}
