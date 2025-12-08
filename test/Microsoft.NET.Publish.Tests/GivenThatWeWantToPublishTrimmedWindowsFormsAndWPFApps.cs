// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishTrimmedWindowsFormsAndWPFApps : SdkTest
    {
        public GivenThatWeWantToPublishTrimmedWindowsFormsAndWPFApps(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_builds_windows_Forms_app_with_error()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new()
            {
                Name = "WinformsBuildErrorFailTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true
            };
            testProject.AdditionalProperties["UseWindowsForms"] = "true";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1175");
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_builds_windows_Forms_app_with_error_suppressed()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new()
            {
                Name = "WinformsBuildErrorSuppressPassTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true
            };
            testProject.AdditionalProperties["UseWindowsForms"] = "true";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["_SuppressWinFormsTrimError"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass()
                .And
                //cannot check for absence of NETSDK1175 since that is used with /nowarn: in some configurations
                .NotHaveStdOutContaining(Strings.@TrimmingWindowsFormsIsNotSupported);
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_publishes_windows_Forms_app_with_error()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new()
            {
                Name = "WinformsErrorPresentFailTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true,
                SelfContained = "true"
            };
            testProject.AdditionalProperties["UseWindowsForms"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x64";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1175");
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_publishes_windows_Forms_app_with_error_suppressed()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new()
            {
                Name = "WinformsErrorSuppressedPassTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true,
                SelfContained = "true"
            };
            testProject.AdditionalProperties["UseWindowsForms"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x64";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["_SuppressWinFormsTrimError"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute()
                .Should()
                .Pass()
                .And
                //cannot check for absence of NETSDK1175 since that is used with /nowarn: in some configurations
                .NotHaveStdOutContaining(Strings.@TrimmingWindowsFormsIsNotSupported);
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_publishes_and_runs_windows_forms_app_with_no_wpf()
        {
            var testDir = _testAssetsManager.CreateTestDirectory();

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(testDir.Path)
                .Execute("winforms")
                .Should()
                .Pass();

            var project = XDocument.Load(Path.Combine(testDir.Path, Path.GetFileName(testDir.Path) + ".csproj"));
            var ns = project.Root.Name.Namespace;
            string targetFramework = project.Root.Elements(ns + "PropertyGroup")
                .Elements(ns + "TargetFramework")
                .Single().Value;

            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            string mainWindowXamlCsPath = Path.Combine(testDir.Path, "Form1.cs");
            string csContents = File.ReadAllText(mainWindowXamlCsPath);
            csContents = csContents.Replace("InitializeComponent();", @"InitializeComponent();

            Shown += delegate { Close(); };");

            File.WriteAllText(mainWindowXamlCsPath, csContents);

            var restoreCommand = new RestoreCommand(Log, testDir.Path);
            restoreCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should()
                .Pass();

            var publishCommand = new PublishCommand(Log, testDir.Path);

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true")
                .Should()
                .Pass();

            var publishDirectory = OutputPathCalculator.FromProject(Path.Combine(testDir.Path, Path.GetFileName(testDir.Path) + ".csproj")).GetPublishDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: rid);

            // Wpf assemblies should not be present and winforms assemblies should be present in the output directory
            // Wpf/WinForms assemblies, like Accessibility.dll should present in the output directory
            var wpfPresentationCoreDll = Path.Combine(publishDirectory, "PresentationCore.dll");
            var wpfPresentationFxDll = Path.Combine(publishDirectory, "PresentationFramework.dll");
            var winFormsDll = Path.Combine(publishDirectory, "System.Windows.Forms.dll");
            var accessibilitysDll = Path.Combine(publishDirectory, "Accessibility.dll");

            File.Exists(wpfPresentationCoreDll).Should().BeFalse();
            File.Exists(wpfPresentationFxDll).Should().BeFalse();
            File.Exists(winFormsDll).Should().BeTrue();
            File.Exists(accessibilitysDll).Should().BeTrue();

            // Run the App
            var runAppCommand = new SdkCommandSpec()
            {
                FileName = Path.Combine(publishDirectory, Path.GetFileName(testDir.Path) + ".exe")
            };

            runAppCommand.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath);

            var result = runAppCommand.ToCommand()
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute();

            result.ExitCode.Should().Be(0);
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_publishes_and_runs_wpf_app_with_no_winforms()
        {
            // It_publishes_and_runs_self_contained_wpf_app also tests a Wpf app run successfully. This test also checks that the right files are present.
            var testDir = _testAssetsManager.CreateTestDirectory();

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(testDir.Path)
                .Execute("wpf")
                .Should()
                .Pass();

            var project = XDocument.Load(Path.Combine(testDir.Path, Path.GetFileName(testDir.Path) + ".csproj"));
            var ns = project.Root.Name.Namespace;
            string targetFramework = project.Root.Elements(ns + "PropertyGroup")
                .Elements(ns + "TargetFramework")
                .Single().Value;

            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            string mainWindowXamlCsPath = Path.Combine(testDir.Path, "MainWindow.xaml.cs");
            string csContents = File.ReadAllText(mainWindowXamlCsPath);
            csContents = csContents.Replace("InitializeComponent();", @"InitializeComponent();

    this.Loaded += delegate { Application.Current.Shutdown(42); };");

            File.WriteAllText(mainWindowXamlCsPath, csContents);

            var restoreCommand = new RestoreCommand(Log, testDir.Path);
            restoreCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should()
                .Pass();

            var publishCommand = new PublishCommand(Log, testDir.Path);

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true")
                .Should()
                .Pass();

            var publishDirectory = OutputPathCalculator.FromProject(Path.Combine(testDir.Path, Path.GetFileName(testDir.Path) + ".csproj")).GetPublishDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: rid);

            // Wpf assemblies should  be present and winforms assemblies should not be present in the output directory
            // Wpf/WinForms assemblies, like Accessibility.dll should present in the output directory
            var wpfPresentationCoreDll = Path.Combine(publishDirectory, "PresentationCore.dll");
            var wpfPresentationFxDll = Path.Combine(publishDirectory, "PresentationFramework.dll");
            var winFormsDll = Path.Combine(publishDirectory, "System.Windows.Forms.dll");
            var accessibilitysDll = Path.Combine(publishDirectory, "Accessibility.dll");

            File.Exists(wpfPresentationCoreDll).Should().BeTrue();
            File.Exists(wpfPresentationFxDll).Should().BeTrue();
            File.Exists(accessibilitysDll).Should().BeTrue();
            File.Exists(winFormsDll).Should().BeFalse();

            // Run the application
            var runAppCommand = new SdkCommandSpec()
            {
                FileName = Path.Combine(publishDirectory, Path.GetFileName(testDir.Path) + ".exe")
            };

            runAppCommand.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath);

            var result = runAppCommand.ToCommand()
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute();

            result.ExitCode.Should().Be(42);
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_publishes_windows_forms_wpf_app()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            var projectName = "WinformsWpfAssemblies";

            var testProject = CreateWpfTestProject(targetFramework, projectName, true);

            testProject.AdditionalProperties["UseWindowsForms"] = "true";
            testProject.AdditionalProperties["UseWPF"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x64";
            testProject.AdditionalProperties["SelfContained"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute()
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, configuration: "Debug", runtimeIdentifier: "win-x64").FullName;

            var wpfPresentationCoreDll = Path.Combine(publishDirectory, "PresentationCore.dll");
            var wpfPresentationFxDll = Path.Combine(publishDirectory, "PresentationFramework.dll");
            var winFormsDll = Path.Combine(publishDirectory, "System.Windows.Forms.dll");
            var accessibilitysDll = Path.Combine(publishDirectory, "Accessibility.dll");

            // Wpf assemblies should  be present and winforms assemblies should not be present in the output directory
            // Wpf/WinForms assemblies, like Accessibility.dll should present in the output directory
            File.Exists(wpfPresentationCoreDll).Should().BeTrue();
            File.Exists(wpfPresentationFxDll).Should().BeTrue();
            File.Exists(accessibilitysDll).Should().BeTrue();
            File.Exists(winFormsDll).Should().BeTrue();
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_builds_wpf_app_with_error()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new()
            {
                Name = "WpfErrorPresentFailTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true
            };
            testProject.AdditionalProperties["UseWPF"] = "true";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1168");
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_builds_wpf_app_with_error_suppressed()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new()
            {
                Name = "WpfErrorSuppressedPassTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true
            };
            testProject.AdditionalProperties["UseWPF"] = "true";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["_SuppressWpfTrimError"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass()
                .And
                //cannot check for absence of NETSDK1168 since that is used with /nowarn: in some configurations
                .NotHaveStdOutContaining(Strings.@TrimmingWpfIsNotSupported);
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_publishes_wpf_app_with_error()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new()
            {
                Name = "WpfErrorPresentPassTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true,
                SelfContained = "true"
            };
            testProject.AdditionalProperties["UseWPF"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x64";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1168");
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_publishes_wpf_app_with_error_Suppressed()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new()
            {
                Name = "WpfPassTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true,
                SelfContained = "true"
            };
            testProject.AdditionalProperties["UseWPF"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x64";
            testProject.AdditionalProperties["_SuppressWpfTrimError"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute()
                .Should()
                .Pass()
                .And
                //cannot check for absence of NETSDK1168 since that is used with /nowarn: in some configurations
                .NotHaveStdOutContaining(Strings.@TrimmingWpfIsNotSupported);
        }

        private TestProject CreateWpfTestProject(string targetFramework, string projectName, bool isExecutable)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework,
                IsWinExe = true,
                SelfContained = "true"
            };

            testProject.SourceFiles[$"{projectName}.cs"] = @"
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using wpfTest;


namespace wpfTest {
    
    
    /// <summary>
    /// App
    /// </summary>
    public partial class App : System.Windows.Application {
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute(""PresentationBuildTasks"", ""9.0.0.0"")]
        public void InitializeComponent() {
            
            //#line 5 ""..\..\..\App.xaml""
            this.StartupUri = new System.Uri(""MainWindow.xaml"", System.UriKind.Relative);
            
            #line default
            #line hidden
        }
        
        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute(""PresentationBuildTasks"", ""9.0.0.0"")]
        public static void Main() {
            wpfTest.App app = new wpfTest.App();
            app.InitializeComponent();
            app.Run();
        }
    }

    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute(""PresentationBuildTasks"", ""9.0.0.0"")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri(""/wpfTest;component/mainwindow.xaml"", System.UriKind.Relative);
            
            //#line 1 ""..\..\..\MainWindow.xaml""
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute(""PresentationBuildTasks"", ""9.0.0.0"")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute(""Microsoft.Design"", ""CA1033:InterfaceMethodsShouldBeCallableByChildTypes"")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute(""Microsoft.Maintainability"", ""CA1502:AvoidExcessiveComplexity"")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute(""Microsoft.Performance"", ""CA1800:DoNotCastUnnecessarily"")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            this._contentLoaded = true;
        }
    }

}
";

            return testProject;
        }
    }
}
