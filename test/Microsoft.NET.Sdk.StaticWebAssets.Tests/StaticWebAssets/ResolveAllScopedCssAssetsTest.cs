// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;


using Microsoft.Build.Utilities;


using Microsoft.VisualStudio.TestTools.UnitTesting;




namespace Microsoft.NET.Sdk.Razor.Test


{


    [TestClass]

    public class ResolveAllScopedCssAssetsTest


    {


        [TestMethod]


        public void ResolveAllScopedCssAssets_IgnoresRegularCssFiles()


        {


            // Arrange


            var taskInstance = new ResolveAllScopedCssAssets()


            {


                StaticWebAssets = new[]


                {


                    new TaskItem("TestFiles/Pages/Counter.razor.rz.scp.css", new Dictionary<string,string>


                    {


                        ["RelativePath"] = "Pages/Counter.razor.rz.scp.css"


                    }),


                    new TaskItem("site.css", new Dictionary<string,string>


                    {


                        ["RelativePath"] = "site.css"


                    }),


                }


            };





            // Act


            var result = taskInstance.Execute();





            // Assert


            result.Should().BeTrue();


            taskInstance.ScopedCssAssets.Should().ContainSingle();


            taskInstance.ScopedCssAssets.Should().NotContain(scopedCssAsset => scopedCssAsset.ItemSpec == "site.css");


        }





        [TestMethod]


        public void ResolveAllScopedCssAssets_DetectsScopedCssFiles()


        {


            // Arrange


            var taskInstance = new ResolveAllScopedCssAssets()


            {


                StaticWebAssets = new[]


                {


                    new TaskItem("TestFiles/Pages/Counter.razor.rz.scp.css", new Dictionary<string,string>


                    {


                        ["RelativePath"] = "Pages/Counter.razor.rz.scp.css"


                    }),


                    new TaskItem("site.css", new Dictionary<string,string>


                    {


                        ["RelativePath"] = "site.css"


                    }),


                }


            };





            // Act


            var result = taskInstance.Execute();





            // Assert


            result.Should().BeTrue();


            taskInstance.ScopedCssAssets.Should().ContainSingle();


            taskInstance.ScopedCssAssets.Should().Contain(scopedCssAsset => scopedCssAsset.ItemSpec == "TestFiles/Pages/Counter.razor.rz.scp.css");


        }





        [TestMethod]


        public void ResolveAllScopedCssAssets_DetectsScopedCssProjectBundleFiles()


        {


            // Arrange


            var taskInstance = new ResolveAllScopedCssAssets()


            {


                StaticWebAssets = new[]


                {


                    new TaskItem("Folder/Project.bundle.scp.css", new Dictionary<string,string>


                    {


                        ["RelativePath"] = "Project.bundle.scp.css"


                    }),


                    new TaskItem("site.css", new Dictionary<string,string>


                    {


                        ["RelativePath"] = "site.css"


                    }),


                }


            };





            // Act


            var result = taskInstance.Execute();





            // Assert


            result.Should().BeTrue();


            taskInstance.ScopedCssProjectBundles.Should().ContainSingle();


            taskInstance.ScopedCssProjectBundles.Should().Contain(scopedCssBundle => scopedCssBundle.ItemSpec == "Folder/Project.bundle.scp.css");


        }





        [TestMethod]


        public void ResolveAllScopedCssAssets_IgnoresScopedCssApplicationBundleFiles()


        {


            // Arrange


            var taskInstance = new ResolveAllScopedCssAssets()


            {


                StaticWebAssets = new[]


                {


                    new TaskItem("Folder/Project.styles.css", new Dictionary<string,string>


                    {


                        ["RelativePath"] = "Project.styles.css"


                    }),


                    new TaskItem("site.css", new Dictionary<string,string>


                    {


                        ["RelativePath"] = "site.css"


                    }),


                }


            };





            // Act


            var result = taskInstance.Execute();





            // Assert


            result.Should().BeTrue();


            taskInstance.ScopedCssProjectBundles.Should().BeEmpty();


        }


    }


}


