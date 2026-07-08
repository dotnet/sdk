// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.HotReload.UnitTests;

public class StaticWebAssetTests
{
    [Theory]
    [InlineData("file.razor.css", true)]
    [InlineData("file.RAZOR.CSS", true)]
    [InlineData("file.Razor.Css", true)]
    [InlineData("file.cshtml.css", true)]
    [InlineData("file.CSHTML.CSS", true)]
    [InlineData("file.Cshtml.Css", true)]
    [InlineData("file.css", false)]
    [InlineData("file.razor.scss", false)]
    [InlineData("file.cshtml.scss", false)]
    [InlineData("razor.css", false)]
    [InlineData("cshtml.css", false)]
    public void IsScopedCssFile_ValidatesCorrectly(string filePath, bool expected)
    {
        Assert.Equal(expected, StaticWebAsset.IsScopedCssFile(filePath));
    }

    [Theory]
    [InlineData("file.bundle.scp.css", true)]
    [InlineData("file.BUNDLE.SCP.CSS", true)]
    [InlineData("file.Bundle.Scp.Css", true)]
    [InlineData("file.styles.css", true)]
    [InlineData("file.STYLES.CSS", true)]
    [InlineData("file.Styles.Css", true)]
    [InlineData("file.css", false)]
    [InlineData("file.bundle.css", false)]
    [InlineData("file.scp.css", false)]
    [InlineData("bundle.scp.css", false)]
    [InlineData("styles.css", false)]
    public void IsScopedCssBundleFile_ValidatesCorrectly(string filePath, bool expected)
    {
        Assert.Equal(expected, StaticWebAsset.IsScopedCssBundleFile(filePath));
    }

    [Theory]
    [InlineData("file.gz", true)]
    [InlineData("file.GZ", true)]
    [InlineData("file.Gz", true)]
    [InlineData("file.br", true)]
    [InlineData("file.BR", true)]
    [InlineData("file.Br", true)]
    [InlineData("file.zip", false)]
    [InlineData("file.tar.gz", true)]
    [InlineData("file.tar", false)]
    [InlineData("gz", false)]
    [InlineData("br", false)]
    public void IsCompressedAssetFile_ValidatesCorrectly(string filePath, bool expected)
    {
        Assert.Equal(expected, StaticWebAsset.IsCompressedAssetFile(filePath));
    }

    [PlatformSpecificTheory(TestPlatforms.AnyUnix)]
    [InlineData("MyApp.csproj", "MyApp.csproj", "MyApp.styles.css")]
    [InlineData("MyApp.csproj", "MYAPP.CSPROJ", "MYAPP.bundle.scp.css")]
    [InlineData("MyApp.csproj", "myapp.csproj", "myapp.bundle.scp.css")]
    [InlineData("MyApp.csproj", "OtherProject.csproj", "OtherProject.bundle.scp.css")]
    [InlineData("MyApp.csproj", "MyLibrary.csproj", "MyLibrary.bundle.scp.css")]
    public void GetScopedCssBundleFileName_GeneratesCorrectName_Linux(string appProject, string containingProject, string expected)
    {
        var result = StaticWebAsset.GetScopedCssBundleFileName(appProject, containingProject);
        Assert.Equal(expected, result);
    }

    [PlatformSpecificTheory(TestPlatforms.Windows)]
    [InlineData("MyApp.csproj", "MyApp.csproj", "MyApp.styles.css")]
    [InlineData("MyApp.csproj", "MYAPP.CSPROJ", "MYAPP.styles.css")]
    [InlineData("MyApp.csproj", "myapp.csproj", "myapp.styles.css")]
    [InlineData("MyApp.csproj", "OtherProject.csproj", "OtherProject.bundle.scp.css")]
    [InlineData("MyApp.csproj", "MyLibrary.csproj", "MyLibrary.bundle.scp.css")]
    public void GetScopedCssBundleFileName_GeneratesCorrectName_Windows(string appProject, string containingProject, string expected)
    {
        var result = StaticWebAsset.GetScopedCssBundleFileName(appProject, containingProject);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("MyApp.csproj", "MyApp.csproj", "wwwroot/MyApp.styles.css")]
    [InlineData("MyApp.csproj", "OtherProject.csproj", "wwwroot/OtherProject.bundle.scp.css")]
    [InlineData("MyApp.csproj", "MyLibrary.csproj", "wwwroot/MyLibrary.bundle.scp.css")]
    public void GetScopedCssRelativeUrl_GeneratesCorrectUrl(string appProject, string containingProject, string expected)
    {
        var result = StaticWebAsset.GetScopedCssRelativeUrl(appProject, containingProject);
        Assert.Equal(expected, result);
    }

    [PlatformSpecificTheory(TestPlatforms.Windows)]
    [InlineData(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\css\site.css", "wwwroot/css/site.css")]
    [InlineData(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\js\app.js", "wwwroot/js/app.js")]
    [InlineData(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\favicon.ico", "wwwroot/favicon.ico")]
    [InlineData(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\lib\bootstrap\bootstrap.min.css", "wwwroot/lib/bootstrap/bootstrap.min.css")]
    [InlineData(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\css\site.css", null)]
    [InlineData(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\OtherApp\wwwroot\css\site.css", null)]
    public void GetAppRelativeUrlFomDiskPath_Windows_GeneratesCorrectUrl(string projectPath, string assetPath, string? expected)
    {
        var result = StaticWebAsset.GetAppRelativeUrlFomDiskPath(projectPath, assetPath);
        Assert.Equal(expected, result);
    }

    [PlatformSpecificTheory(TestPlatforms.AnyUnix)]
    [InlineData("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/wwwroot/css/site.css", "wwwroot/css/site.css")]
    [InlineData("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/wwwroot/js/app.js", "wwwroot/js/app.js")]
    [InlineData("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/wwwroot/favicon.ico", "wwwroot/favicon.ico")]
    [InlineData("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/css/site.css", null)]
    [InlineData("/home/user/MyApp/MyApp.csproj", "/home/user/OtherApp/wwwroot/css/site.css", null)]
    public void GetAppRelativeUrlFomDiskPath_Unix_GeneratesCorrectUrl(string projectPath, string assetPath, string? expected)
    {
        var result = StaticWebAsset.GetAppRelativeUrlFomDiskPath(projectPath, assetPath);
        Assert.Equal(expected, result);
    }

    [PlatformSpecificTheory(TestPlatforms.Windows)]
    [InlineData(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\Pages\Counter.razor.css", "wwwroot/MyApp.styles.css")]
    [InlineData(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyLibrary\MyLibrary.csproj", @"C:\Projects\MyLibrary\Components\Component.razor.css", "wwwroot/MyLibrary.bundle.scp.css")]
    [InlineData(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\css\site.css", "wwwroot/css/site.css")]
    [InlineData(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\css\site.css", null)]
    public void GetRelativeUrl_Windows_GeneratesCorrectUrl(string appProject, string containingProject, string assetPath, string? expected)
    {
        var result = StaticWebAsset.GetRelativeUrl(appProject, containingProject, assetPath);
        Assert.Equal(expected, result);
    }

    [PlatformSpecificTheory(TestPlatforms.AnyUnix)]
    [InlineData("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/Pages/Counter.razor.css", "wwwroot/MyApp.styles.css")]
    [InlineData("/home/user/MyApp/MyApp.csproj", "/home/user/MyLibrary/MyLibrary.csproj", "/home/user/MyLibrary/Components/Component.razor.css", "wwwroot/MyLibrary.bundle.scp.css")]
    [InlineData("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/wwwroot/css/site.css", "wwwroot/css/site.css")]
    [InlineData("/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/MyApp.csproj", "/home/user/MyApp/css/site.css", null)]
    public void GetRelativeUrl_Unix_GeneratesCorrectUrl(string appProject, string containingProject, string assetPath, string? expected)
    {
        var result = StaticWebAsset.GetRelativeUrl(appProject, containingProject, assetPath);
        Assert.Equal(expected, result);
    }

    [PlatformSpecificTheory(TestPlatforms.Windows)]
    [InlineData(@"C:\Projects\MYAPP\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\css\site.css", "wwwroot/css/site.css")]
    public void GetAppRelativeUrlFomDiskPath_IsCaseInsensitive(string projectPath, string assetPath, string? expected)
    {
        var result = StaticWebAsset.GetAppRelativeUrlFomDiskPath(projectPath, assetPath);
        Assert.Equal(expected, result);
    }

    [PlatformSpecificTheory(TestPlatforms.Windows)]
    [InlineData(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\WWWROOT\css\site.css", "WWWROOT/css/site.css")]
    public void GetAppRelativeUrlFomDiskPath_WwwrootIsCaseInsensitive(string projectPath, string assetPath, string? expected)
    {
        var result = StaticWebAsset.GetAppRelativeUrlFomDiskPath(projectPath, assetPath);
        Assert.Equal(expected, result);
    }

    [PlatformSpecificTheory(TestPlatforms.Windows)]
    [InlineData(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\css\site.css")]
    [InlineData(@"C:\Projects\MyApp\MyApp.csproj", @"C:\Projects\MyApp\wwwroot\CSS\site.css")]
    public void GetAppRelativeUrlFomDiskPath_NormalizesBackslashesToForwardSlashes(string projectPath, string assetPath)
    {
        var result = StaticWebAsset.GetAppRelativeUrlFomDiskPath(projectPath, assetPath);
        Assert.NotNull(result);
        Assert.DoesNotContain("\\", result);
        Assert.Contains("/", result);
    }
}
