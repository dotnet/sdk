// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//using System.Text;
//using FakeItEasy;
//using Microsoft.DotNet.Cli.Utils;
//using Microsoft.TemplateEngine.Abstractions;
//using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
//using Microsoft.TemplateEngine.Cli.NuGet;
//using Microsoft.TemplateEngine.Cli.TabularOutput;
//using Microsoft.TemplateEngine.Edge;
//using Microsoft.TemplateEngine.Edge.Installers.NuGet;
//using Microsoft.TemplateEngine.Edge.Settings;
//using static Microsoft.TemplateEngine.Cli.NuGet.NugetApiManager;

//namespace Microsoft.TemplateEngine.Cli.UnitTests
//{
//    public class TemplatePackageCoordinatorTests
//    {
//        //[Fact]
//        //public void DisplayPackageMetadataAllData()
//        //{
//        //    ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
//        //    IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
//        //    TemplatePackageManager templatePackageManager = A.Fake<TemplatePackageManager>();

//        //    var packageCoordinator = new TemplatePackageCoordinator(settings, templatePackageManager);
//        //    var packageMetadata = new NugetPackageMetadata();

//        //    var fakeTemplates = new List<ITemplateInfo>();
//        //    var templatesToDisplay = TemplateGroupDisplay.GetTemplateGroupsForListDisplay(fakeTemplates, null, null, settings.Environment);

//        //    var fakeOutputReporter = new FakeReporter();
//        //    packageCoordinator.DisplayPackageMetadata(
//        //        packageMetadata,
//        //        templatesToDisplay,
//        //        fakeOutputReporter);


//        //}

//        //[Fact]
//        //public void DisplayPackageMetadataEmptyData()
//        //{

//        //}

//        //[Fact]
//        //public void DisplayPackageMetadataMultipleAuthors()
//        //{

//        //}

//        //[Fact]
//        //public void DisplayPackageMetadataMultipleOwners()
//        //{

//        //}

//        //private class FakeReporter : IReporter
//        //{
//        //    public StringBuilder ReportedStrings { get; set; } = new StringBuilder();

//        //    public void Write(string message) => ReportedStrings.Append(message);

//        //    public void WriteLine(string message) => ReportedStrings.AppendLine(message);

//        //    public void WriteLine() => ReportedStrings.AppendLineN();

//        //    public void WriteLine(string format, params object?[] args) => WriteLine(string.Format(format, args));

//        //}
//    }
//}
