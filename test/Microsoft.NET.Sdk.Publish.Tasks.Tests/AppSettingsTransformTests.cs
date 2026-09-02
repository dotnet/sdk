// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests
{
    [TestClass]
    public class AppSettingsTransformTests
    {
        [TestMethod]
        public void GenerateDefaultAppSettingsJsonFile_CreatesCorrectDefaultFile()
        {
            // Act 
            string resultFile = AppSettingsTransform.GenerateDefaultAppSettingsJsonFile();

            // Assert
            Assert.IsTrue(File.Exists(resultFile));
            JToken defaultConnectionString = JObject.Parse(File.ReadAllText(resultFile))["ConnectionStrings"]["DefaultConnection"];
            Assert.AreEqual(defaultConnectionString.ToString(), string.Empty);
        }


        [TestMethod]
        [DataRow("DefaultConnection", @"Server=(localdb)\mssqllocaldb;Database=defaultDB;Trusted_Connection=True;MultipleActiveResultSets=true")]
        [DataRow("EmptyConnection", @"")]
        [DataRow("", @"SomeConnectionStringValue")]
        public void AppSettingsTransform_UpdatesSingleConnectionString(string connectionName, string connectionString)
        {
            // Arrange
            ITaskItem[] taskItemArray = new ITaskItem[1];
            TaskItem connectionstringTaskItem = new(connectionName);
            connectionstringTaskItem.SetMetadata("Value", connectionString);
            taskItemArray[0] = connectionstringTaskItem;

            string appsettingsFile = AppSettingsTransform.GenerateDefaultAppSettingsJsonFile();

            // Act 
            AppSettingsTransform.UpdateDestinationConnectionStringEntries(appsettingsFile, taskItemArray);

            // Assert
            JToken connectionStringValue = JObject.Parse(File.ReadAllText(appsettingsFile))["ConnectionStrings"][connectionName];
            Assert.AreEqual(connectionStringValue.ToString(), connectionString);

            if (File.Exists(appsettingsFile))
            {
                File.Delete(appsettingsFile);
            }
        }

        [TestMethod]
        [DataRow("DefaultConnection", @"Server=(localdb)\mssqllocaldb;Database=defaultDB;Trusted_Connection=True;MultipleActiveResultSets=true")]
        [DataRow("EmptyConnection", @"")]
        [DataRow("", @"SomeConnectionStringValue")]
        public void AppSettingsTransform_DoesNotFailsIfEntryIsMissinginAppSettings(string connectionName, string connectionString)
        {
            // Arrange
            ITaskItem[] taskItemArray = new ITaskItem[1];
            TaskItem connectionstringTaskItem = new(connectionName);
            connectionstringTaskItem.SetMetadata("Value", connectionString);
            taskItemArray[0] = connectionstringTaskItem;

            string appsettingsFile = AppSettingsTransform.GenerateDefaultAppSettingsJsonFile();
            File.WriteAllText(appsettingsFile, "{}");

            // Act 
            bool succeed = AppSettingsTransform.UpdateDestinationConnectionStringEntries(appsettingsFile, taskItemArray);

            // Assert
            Assert.IsTrue(succeed);

            if (File.Exists(appsettingsFile))
            {
                File.Delete(appsettingsFile);
            }
        }

        private static readonly ITaskItem DefaultConnectionTaskItem = new TaskItem("DefaultConnection", new Dictionary<string, string>() { { "Value", @"Server=(localdb)\mssqllocaldb; Database=defaultDB;Trusted_Connection=True;MultipleActiveResultSets=true" } });
        private static readonly ITaskItem CarConnectionTaskItem = new TaskItem("CarConnection", new Dictionary<string, string>() { { "Value", @"Server=(localdb)\mssqllocaldb; Database=CarDB;Trusted_Connection=True;MultipleActiveResultSets=true" } });
        private static readonly ITaskItem PersonConnectionTaskItem = new TaskItem("PersonConnection", new Dictionary<string, string>() { { "Value", @"Server=(localdb)\mssqllocaldb; Database=PersonDb;Trusted_Connection=True;MultipleActiveResultSets=true" } });

        private static readonly List<object[]> testData = new()
        {
            new object[] {new ITaskItem[] { DefaultConnectionTaskItem } },
            new object[] {new ITaskItem[] { DefaultConnectionTaskItem, CarConnectionTaskItem, PersonConnectionTaskItem } }
        };

        public static IEnumerable<object[]> ConnectionStringsData
        {
            get { return testData; }
        }

        [TestMethod]
        [DynamicData(nameof(ConnectionStringsData), typeof(AppSettingsTransformTests))]
        public void AppSettingsTransform_UpdatesMultipleConnectionStrings(ITaskItem[] values)
        {
            // Arrange
            string destinationAppSettingsFile = AppSettingsTransform.GenerateDefaultAppSettingsJsonFile();

            //Act
            AppSettingsTransform.UpdateDestinationConnectionStringEntries(destinationAppSettingsFile, values);

            // Assert
            foreach (var eachValue in values)
            {
                JToken connectionStringValue = JObject.Parse(File.ReadAllText(destinationAppSettingsFile))["ConnectionStrings"][eachValue.ItemSpec];
                Assert.AreEqual(connectionStringValue.ToString(), eachValue.GetMetadata("Value"));
            }

            if (File.Exists(destinationAppSettingsFile))
            {
                File.Delete(destinationAppSettingsFile);
            }
        }

        [TestMethod]
        [DataRow("DefaultConnection", @"Server=(localdb)\mssqllocaldb;Database=defaultDB;Trusted_Connection=True;MultipleActiveResultSets=true")]
        [DataRow("EmptyConnection", @"")]
        [DataRow("", @"SomeConnectionStringValue")]
        public void AppSettingsTransform_UpdateConnectionStringEvenIfConnectionStringSectionMissing(string connectionName, string connectionString)
        {
            // Arrange
            ITaskItem[] taskItemArray = new ITaskItem[1];
            var connectionstringTaskItem = new TaskItem(connectionName);
            connectionstringTaskItem.SetMetadata("Value", connectionString);
            taskItemArray[0] = connectionstringTaskItem;

            // appSettings.json with no ConnectionStrings (empty)
            var appsettingsFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllText(appsettingsFile, "{}");

            // Act 
            AppSettingsTransform.UpdateDestinationConnectionStringEntries(appsettingsFile, taskItemArray);

            // Assert
            JToken connectionStringValue = JObject.Parse(File.ReadAllText(appsettingsFile))["ConnectionStrings"][connectionName];
            Assert.AreEqual(connectionStringValue.ToString(), connectionString);

            if (File.Exists(appsettingsFile))
            {
                File.Delete(appsettingsFile);
            }
        }
    }
}
