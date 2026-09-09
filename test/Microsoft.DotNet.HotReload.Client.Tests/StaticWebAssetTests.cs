// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.HotReload.UnitTests;

[TestClass]
public class StaticWebAssetTests
{
    [TestMethod]
    [DataRow("file.razor.css", true)]
    [DataRow("file.RAZOR.CSS", true)]
    [DataRow("file.Razor.Css", true)]
    [DataRow("file.cshtml.css", true)]
    [DataRow("file.CSHTML.CSS", true)]
    [DataRow("file.Cshtml.Css", true)]
    [DataRow("file.css", false)]
    [DataRow("file.razor.scss", false)]
    [DataRow("file.cshtml.scss", false)]
    [DataRow("razor.css", false)]
    [DataRow("cshtml.css", false)]
    public void IsScopedCssFile_ValidatesCorrectly(string filePath, bool expected)
    {
        Assert.AreEqual(expected, StaticWebAsset.IsScopedCssFile(filePath));
    }

    [TestMethod]
    [DataRow("file.bundle.scp.css", true)]
    [DataRow("file.BUNDLE.SCP.CSS", true)]
    [DataRow("file.Bundle.Scp.Css", true)]
    [DataRow("file.styles.css", true)]
    [DataRow("file.STYLES.CSS", true)]
    [DataRow("file.Styles.Css", true)]
    [DataRow("file.css", false)]
    [DataRow("file.bundle.css", false)]
    [DataRow("file.scp.css", false)]
    [DataRow("bundle.scp.css", false)]
    [DataRow("styles.css", false)]
    public void IsScopedCssBundleFile_ValidatesCorrectly(string filePath, bool expected)
    {
        Assert.AreEqual(expected, StaticWebAsset.IsScopedCssBundleFile(filePath));
    }

    [TestMethod]
    [DataRow("file.gz", true)]
    [DataRow("file.GZ", true)]
    [DataRow("file.Gz", true)]
    [DataRow("file.br", true)]
    [DataRow("file.BR", true)]
    [DataRow("file.Br", true)]
    [DataRow("file.zip", false)]
    [DataRow("file.tar.gz", true)]
    [DataRow("file.tar", false)]
    [DataRow("gz", false)]
    [DataRow("br", false)]
    public void IsCompressedAssetFile_ValidatesCorrectly(string filePath, bool expected)
    {
        Assert.AreEqual(expected, StaticWebAsset.IsCompressedAssetFile(filePath));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("MyApp.csproj", "MyApp.csproj", "MyApp.styles.css")]
    [DataRow("MyApp.csproj", "MYAPP.CSPROJ", "MYAPP.bundle.scp.css")]
    [DataRow("MyApp.csproj", "myapp.csproj", "myapp.bundle.scp.css")]
    [DataRow("MyApp.csproj", "OtherProject.csproj", "OtherProject.bundle.scp.css")]
    [DataRow("MyApp.csproj", "MyLibrary.csproj", "MyLibrary.bundle.scp.css")]
    public void GetScopedCssBundleFileName_GeneratesCorrectName_Linux(string appProject, string containingProject, string expected)
    {
        var result = StaticWebAsset.GetScopedCssBundleFileName(appProject, containingProject);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("MyApp.csproj", "MyApp.csproj", "MyApp.styles.css")]
    [DataRow("MyApp.csproj", "MYAPP.CSPROJ", "MYAPP.styles.css")]
    [DataRow("MyApp.csproj", "myapp.csproj", "myapp.styles.css")]
    [DataRow("MyApp.csproj", "OtherProject.csproj", "OtherProject.bundle.scp.css")]
    [DataRow("MyApp.csproj", "MyLibrary.csproj", "MyLibrary.bundle.scp.css")]
    public void GetScopedCssBundleFileName_GeneratesCorrectName_Windows(string appProject, string containingProject, string expected)
    {
        var result = StaticWebAsset.GetScopedCssBundleFileName(appProject, containingProject);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("MyApp.csproj", "MyApp.csproj", "wwwroot/MyApp.styles.css")]
    [DataRow("MyApp.csproj", "OtherProject.csproj", "wwwroot/OtherProject.bundle.scp.css")]
    [DataRow("MyApp.csproj", "MyLibrary.csproj", "wwwroot/MyLibrary.bundle.scp.css")]
    public void GetScopedCssRelativeUrl_GeneratesCorrectUrl(string appProject, string containingProject, string expected)
    {
        var result = StaticWebAsset.GetScopedCssRelativeUrl(appProject, containingProject);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\css\site.css", "wwwroot/css/site.css")]
    [DataRow(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\js\app.js", "wwwroot/js/app.js")]
    [DataRow(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\favicon.ico", "wwwroot/favicon.ico")]
    [DataRow(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\lib\bootstrap\bootstrap.min.css", "wwwroot/lib/bootstrap/bootstrap.min.css")]
    [DataRow(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\css\site.css", null)]
    [DataRow(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\OtherApp\wwwroot\css\site.css", null)]
    public void GetAppRelativeUrlFomDiskPath_Windows_GeneratesCorrectUrl(string projectPath, string assetPath, string? expected)
    {
        var result = StaticWebAsset.GetAppRelativeUrlFomDiskPath(projectPath, assetPath);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/wwwroot/css/site.css", "wwwroot/css/site.css")]
    [DataRow("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/wwwroot/js/app.js", "wwwroot/js/app.js")]
    [DataRow("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/wwwroot/favicon.ico", "wwwroot/favicon.ico")]
    [DataRow("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/css/site.css", null)]
    [DataRow("/home/user/MyApp/MyApp.csproj", "/home/user/OtherApp/wwwroot/css/site.css", null)]
    public void GetAppRelativeUrlFomDiskPath_Unix_GeneratesCorrectUrl(string projectPath, string assetPath, string? expected)
    {
        var result = StaticWebAsset.GetAppRelativeUrlFomDiskPath(projectPath, assetPath);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\Pages\Counter.razor.css", "wwwroot/MyApp.styles.css")]
    [DataRow(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyLibrary\MyLibrary.csproj", @"C:\Projects\MyLibrary\Components\Component.razor.css", "wwwroot/MyLibrary.bundle.scp.css")]
    [DataRow(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\css\site.css", "wwwroot/css/site.css")]
    [DataRow(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\css\site.css", null)]
    public void GetRelativeUrl_Windows_GeneratesCorrectUrl(string appProject, string containingProject, string assetPath, string? expected)
    {
        var result = StaticWebAsset.GetRelativeUrl(appProject, containingProject, assetPath);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/Pages/Counter.razor.css", "wwwroot/MyApp.styles.css")]
    [DataRow("/home/user/MyApp/MyApp.csproj", "/home/user/MyLibrary/MyLibrary.csproj", "/home/user/MyLibrary/Components/Component.razor.css", "wwwroot/MyLibrary.bundle.scp.css")]
    [DataRow("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/wwwroot/css/site.css", "wwwroot/css/site.css")]
    [DataRow("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/css/site.css", null)]
    public void GetRelativeUrl_Unix_GeneratesCorrectUrl(string appProject, string containingProject, string assetPath, string? expected)
    {
        var result = StaticWebAsset.GetRelativeUrl(appProject, containingProject, assetPath);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(@"C:\Projects\MYAPP\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\css\site.css", "wwwroot/css/site.css")]
    public void GetAppRelativeUrlFomDiskPath_IsCaseInsensitive(string projectPath, string assetPath, string? expected)
    {
        var result = StaticWebAsset.GetAppRelativeUrlFomDiskPath(projectPath, assetPath);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\WWWROOT\css\site.css", "WWWROOT/css/site.css")]
    public void GetAppRelativeUrlFomDiskPath_WwwrootIsCaseInsensitive(string projectPath, string assetPath, string? expected)
    {
        var result = StaticWebAsset.GetAppRelativeUrlFomDiskPath(projectPath, assetPath);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\css\site.css")]
    [DataRow(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\CSS\site.css")]
    public void GetAppRelativeUrlFomDiskPath_NormalizesBackslashesToForwardSlashes(string projectPath, string assetPath)
    {
        var result = StaticWebAsset.GetAppRelativeUrlFomDiskPath(projectPath, assetPath);
        Assert.IsNotNull(result);
        Assert.DoesNotContain("\\", result!);
        Assert.Contains("/", result!);
    }
}
