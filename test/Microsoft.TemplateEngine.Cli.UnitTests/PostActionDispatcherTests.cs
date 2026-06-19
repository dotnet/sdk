// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    [TestClass]
    public class PostActionDispatcherTests
    {
        // MSTest has no IClassFixture equivalent; a lazily-initialized static helper
        // mirrors the per-class lifetime that xUnit's IClassFixture provides.
        private static readonly Lazy<EnvironmentSettingsHelper> s_environmentSettingsHelper =
            new(() => new EnvironmentSettingsHelper(NullMessageSink.Instance));

        private EnvironmentSettingsHelper _environmentSettingsHelper = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _environmentSettingsHelper = s_environmentSettingsHelper.Value;
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (s_environmentSettingsHelper.IsValueCreated)
            {
                s_environmentSettingsHelper.Value.Dispose();
            }
        }

        [TestMethod]
        public void CanProcessSuccessPostAction()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            var postActionProcessor = new CaptureMePostAction(expectedResult: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), postActionProcessor);
            var postAction = new MockPostAction(default, default, default, default, default!);
            MockCreationResult creationResult = new(new List<IPostAction>() { postAction });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                "TestPath",
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => string.Empty);

            var result = dispatcher.Process(templateCreationResult, isDryRun: false, AllowRunScripts.Prompt);
            Assert.AreEqual(PostActionExecutionStatus.Success, result);
            Assert.HasCount(1, postActionProcessor.Calls);
            Assert.AreEqual(engineEnvironmentSettings, postActionProcessor.Calls.Single().EngineEnvironmentSettings);
            Assert.AreEqual(postAction, postActionProcessor.Calls.Single().PostAction);
            Assert.AreEqual(creationEffects, postActionProcessor.Calls.Single().CreationEffects);
            Assert.AreEqual(creationResult, postActionProcessor.Calls.Single().CreationResult);
            Assert.AreEqual("TestPath", postActionProcessor.Calls.Single().OutputPath);
        }

        [TestMethod]
        public void CanDryRunSuccessPostAction()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            var postActionProcessor = new CaptureMePostAction(expectedResult: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), postActionProcessor);
            var postAction = new MockPostAction(default, default, default, default, default!);

            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                null,
                "TestPath",
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => string.Empty);

            var result = dispatcher.Process(templateCreationResult, isDryRun: true, AllowRunScripts.Prompt);
            Assert.AreEqual(PostActionExecutionStatus.Success, result);
            Assert.IsEmpty(postActionProcessor.Calls);
        }

        [TestMethod]
        public void CanProcessFailedPostAction()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            var postActionProcessor = new CaptureMePostAction(expectedResult: false);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), postActionProcessor);
            var postAction = new MockPostAction(default, default, default, default, default!);
            MockCreationResult creationResult = new(new List<IPostAction>() { postAction });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                "TestPath",
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => string.Empty);

            var result = dispatcher.Process(templateCreationResult, isDryRun: false, AllowRunScripts.Prompt);
            Assert.AreEqual(PostActionExecutionStatus.Failure, result);
            Assert.AreEqual(engineEnvironmentSettings, postActionProcessor.Calls.Single().EngineEnvironmentSettings);
            Assert.AreEqual(postAction, postActionProcessor.Calls.Single().PostAction);
            Assert.AreEqual(creationEffects, postActionProcessor.Calls.Single().CreationEffects);
            Assert.AreEqual(creationResult, postActionProcessor.Calls.Single().CreationResult);
            Assert.AreEqual("TestPath", postActionProcessor.Calls.Single().OutputPath);
        }

        [TestMethod]
        public void CanDryRunFailedPostAction()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            var postActionProcessor = new CaptureMePostAction(expectedResult: false);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), postActionProcessor);
            var postAction = new MockPostAction(default, default, default, default, default!);

            MockCreationResult creationResult = new(new List<IPostAction>() { postAction });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                "TestPath",
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => string.Empty);

            var result = dispatcher.Process(templateCreationResult, isDryRun: true, AllowRunScripts.Prompt);
            Assert.AreEqual(PostActionExecutionStatus.Success, result);
            Assert.IsEmpty(postActionProcessor.Calls);
        }

        [TestMethod]
        public void CanProcessUnknownPostAction()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            var postActionProcessor = new CaptureMePostAction(expectedResult: false);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), postActionProcessor);
            var postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = Guid.NewGuid(),
            };

            MockCreationResult creationResult = new(new List<IPostAction>() { postAction });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                "TestPath",
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => string.Empty);

            var result = dispatcher.Process(templateCreationResult, isDryRun: false, AllowRunScripts.Prompt);
            Assert.AreEqual(PostActionExecutionStatus.Failure, result);
            Assert.IsEmpty(postActionProcessor.Calls);
        }

        [TestMethod]
        public void CanProcessPostActionThrowingException()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), new ThrowExceptionPostAction());
            var postAction = new MockPostAction(default, default, default, default, default!);

            MockCreationResult creationResult = new(new List<IPostAction>() { postAction });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                "TestPath",
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => string.Empty);

            var result = dispatcher.Process(templateCreationResult, isDryRun: false, AllowRunScripts.Yes);
            Assert.AreEqual(PostActionExecutionStatus.Failure, result);
        }

        [TestMethod]
        public void CanContinueOnErrorWhenConfigured()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            var trueProcessorGuid = Guid.NewGuid();
            var falseProcessorGuid = Guid.NewGuid();
            var trueProcessor = new CaptureMePostAction(expectedResult: true, guid: trueProcessorGuid);
            var falseProcessor = new CaptureMePostAction(expectedResult: false, guid: falseProcessorGuid);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), trueProcessor);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), falseProcessor);

            var postAction1 = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = falseProcessorGuid,
                ContinueOnError = true
            };
            var postAction2 = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = trueProcessorGuid,
                ContinueOnError = true
            };

            MockCreationResult creationResult = new(new List<IPostAction>() { postAction1, postAction2 });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                "TestPath",
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => string.Empty);

            var result = dispatcher.Process(templateCreationResult, isDryRun: false, AllowRunScripts.Yes);

            // in case continue on error is true, success status is returned on failure
            Assert.AreEqual(PostActionExecutionStatus.Success, result);

            //2 post actions were executed
            Assert.HasCount(1, trueProcessor.Calls);
            Assert.HasCount(1, falseProcessor.Calls);
            Assert.AreEqual(postAction1, falseProcessor.Calls[0].PostAction);
            Assert.AreEqual(postAction2, trueProcessor.Calls[0].PostAction);
        }

        [TestMethod]
        public void CannotContinueOnErrorWhenNotConfigured()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            var trueProcessorGuid = Guid.NewGuid();
            var falseProcessorGuid = Guid.NewGuid();
            var trueProcessor = new CaptureMePostAction(expectedResult: true, guid: trueProcessorGuid);
            var falseProcessor = new CaptureMePostAction(expectedResult: false, guid: falseProcessorGuid);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), trueProcessor);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), falseProcessor);

            var postAction1 = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = falseProcessorGuid,
                ContinueOnError = false
            };
            var postAction2 = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = trueProcessorGuid,
                ContinueOnError = false
            };

            MockCreationResult creationResult = new(new List<IPostAction>() { postAction1, postAction2 });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                "TestPath",
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => string.Empty);

            var result = dispatcher.Process(templateCreationResult, isDryRun: false, AllowRunScripts.Yes);
            Assert.AreEqual(PostActionExecutionStatus.Failure, result);

            //only first post action was executed
            Assert.IsEmpty(trueProcessor.Calls);
            Assert.HasCount(1, falseProcessor.Calls);
            Assert.AreEqual(postAction1, falseProcessor.Calls[0].PostAction);
        }

        [TestMethod]
        public void CanProcessRunScriptPostAction_WhenRunScriptAllowed()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), new ProcessStartPostActionProcessor());
            var postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = ProcessStartPostActionProcessor.ActionProcessorId,
                //the post action will fail, but that is OK for test purpose
                Args = new Dictionary<string, string>()
                { { "executable", "do-not-exist" } }
            };

            MockCreationResult creationResult = new(new List<IPostAction>() { postAction });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                Path.GetTempPath(),
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => string.Empty);

            var result = dispatcher.Process(templateCreationResult, isDryRun: false, AllowRunScripts.Yes);
            //expect failure as post action fails
            Assert.AreEqual(PostActionExecutionStatus.Failure, result);
        }

        [TestMethod]
        public void CanProcessRunScriptPostAction_WhenRunScriptNotAllowed()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), new ProcessStartPostActionProcessor());
            var postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = ProcessStartPostActionProcessor.ActionProcessorId,
                //the post action will fail, but that is OK for test purpose
                Args = new Dictionary<string, string>()
                { { "executable", "do-not-exist" } }
            };

            MockCreationResult creationResult = new(new List<IPostAction>() { postAction });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                Path.GetTempPath(),
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => string.Empty);

            var result = dispatcher.Process(templateCreationResult, isDryRun: false, AllowRunScripts.No);
            Assert.AreEqual(PostActionExecutionStatus.Cancelled, result);
        }

        [TestMethod]
        public void CanProcessRunScriptPostAction_WhenRunScriptPrompt_Yes()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), new ProcessStartPostActionProcessor());
            var postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = ProcessStartPostActionProcessor.ActionProcessorId,
                //the post action will fail, but that is OK for test purpose
                Args = new Dictionary<string, string>()
                { { "executable", "do-not-exist" } }
            };

            MockCreationResult creationResult = new(new List<IPostAction>() { postAction });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                Path.GetTempPath(),
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => "Y");  // the user allows to run post action

            var result = dispatcher.Process(templateCreationResult, isDryRun: false, AllowRunScripts.Prompt);

            //expect failure as post action fails
            Assert.AreEqual(PostActionExecutionStatus.Failure, result);
        }

        [TestMethod]
        public void CanProcessRunScriptPostAction_WhenRunScriptPrompt_No()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), new ProcessStartPostActionProcessor());
            var postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = ProcessStartPostActionProcessor.ActionProcessorId,
                //the post action will fail, but that is OK for test purpose
                Args = new Dictionary<string, string>()
                { { "executable", "do-not-exist" } }
            };

            MockCreationResult creationResult = new(new List<IPostAction>() { postAction });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                Path.GetTempPath(),
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => "N"); // the user forbids to run post action

            var result = dispatcher.Process(templateCreationResult, isDryRun: false, AllowRunScripts.Prompt);
            Assert.AreEqual(PostActionExecutionStatus.Cancelled, result);
        }

        [TestMethod]
        public void CanProcessRunScriptPostAction_DryRun()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), new ProcessStartPostActionProcessor());
            var postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = ProcessStartPostActionProcessor.ActionProcessorId,
                //the post action will fail, but that is OK for test purpose
                Args = new Dictionary<string, string>()
                { { "executable", "do-not-exist" } }
            };

            MockCreationResult creationResult = new(new List<IPostAction>() { postAction });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                Path.GetTempPath(),
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => "N"); // the user forbids to run post action

            //run script setting doesn't matter for dry run
            var result = dispatcher.Process(templateCreationResult, isDryRun: true, AllowRunScripts.Prompt);
            Assert.AreEqual(PostActionExecutionStatus.Success, result);
            result = dispatcher.Process(templateCreationResult, isDryRun: true, AllowRunScripts.Yes);
            Assert.AreEqual(PostActionExecutionStatus.Success, result);
            result = dispatcher.Process(templateCreationResult, isDryRun: true, AllowRunScripts.No);
            Assert.AreEqual(PostActionExecutionStatus.Success, result);
        }

        [TestMethod]
        public void CanProcessRunScriptPostActionAndFailedPostAction_WhenRunScriptPrompt_No()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            var postActionProcessor = new CaptureMePostAction(expectedResult: false);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), postActionProcessor);

            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), new ProcessStartPostActionProcessor());
            var postAction1 = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = ProcessStartPostActionProcessor.ActionProcessorId,
                //the post action will fail, but that is OK for test purpose
                Args = new Dictionary<string, string>()
                { { "executable", "do-not-exist" } },
                ContinueOnError = true
            };

            var postAction2 = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = Guid.Empty, //CaptureMePostAction
                ContinueOnError = true
            };

            MockCreationResult creationResult = new(new List<IPostAction>() { postAction1, postAction2 });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                Path.GetTempPath(),
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => "N"); // the user forbids to run post action

            var result = dispatcher.Process(templateCreationResult, isDryRun: false, AllowRunScripts.Prompt);
            Assert.AreEqual(PostActionExecutionStatus.Cancelled, result);
            Assert.HasCount(1, postActionProcessor.Calls);
            Assert.AreEqual(postAction2, postActionProcessor.Calls.Single().PostAction);
        }

        [TestMethod]
        public void CanProcessRunScriptPostActionAndSuccessPostAction_WhenRunScriptPrompt_No()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            var postActionProcessor = new CaptureMePostAction(expectedResult: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), postActionProcessor);

            engineEnvironmentSettings.Components.AddComponent(typeof(IPostActionProcessor), new ProcessStartPostActionProcessor());
            var postAction1 = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = ProcessStartPostActionProcessor.ActionProcessorId,
                //the post action will fail, but that is OK for test purpose
                Args = new Dictionary<string, string>()
                { { "executable", "do-not-exist" } },
                ContinueOnError = true
            };

            var postAction2 = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = Guid.Empty, //CaptureMePostAction
                ContinueOnError = true
            };

            MockCreationResult creationResult = new(new List<IPostAction>() { postAction1, postAction2 });
            MockCreationEffects creationEffects = new();
            var templateCreationResult = new TemplateCreationResult(
                CreationResultStatus.Success,
                "TestTemplate",
                null,
                creationResult,
                Path.GetTempPath(),
                creationEffects);

            PostActionDispatcher dispatcher = new(
                engineEnvironmentSettings,
                () => "N"); // the user forbids to run post action

            var result = dispatcher.Process(templateCreationResult, isDryRun: false, AllowRunScripts.Prompt);
            Assert.AreEqual(PostActionExecutionStatus.Cancelled, result);
            Assert.AreNotEqual(PostActionExecutionStatus.Failure, result);
            Assert.HasCount(1, postActionProcessor.Calls);
            Assert.AreEqual(postAction2, postActionProcessor.Calls.Single().PostAction);
        }

        private class CaptureMePostAction : IPostActionProcessor
        {
            private readonly List<(
                IEngineEnvironmentSettings EngineEnvironmentSettings,
                IPostAction PostAction,
                ICreationEffects CreationEffects,
                ICreationResult CreationResult,
                string OutputPath)> _receivedCalls = new();

            private bool _expectedResult;
            private string _testName;
            private Guid _guid;

            public CaptureMePostAction([CallerMemberName] string test = "", bool expectedResult = true, Guid guid = default)
            {
                _testName = test;
                _expectedResult = expectedResult;
                _guid = guid;
            }

            public Guid Id => _guid;

            public IReadOnlyList<(
                IEngineEnvironmentSettings EngineEnvironmentSettings,
                IPostAction PostAction,
                ICreationEffects CreationEffects,
                ICreationResult CreationResult,
                string OutputPath)> Calls => _receivedCalls;

            public bool Process(
                IEngineEnvironmentSettings environment,
                IPostAction action,
                ICreationEffects creationEffects,
                ICreationResult templateCreationResult,
                string outputBasePath)
            {
                _receivedCalls.Add((environment, action, creationEffects, templateCreationResult, outputBasePath));
                return _expectedResult;
            }
        }

        private class ThrowExceptionPostAction : IPostActionProcessor
        {
            public Guid Id => Guid.Empty;

            public bool Process(
                IEngineEnvironmentSettings environment,
                IPostAction action,
                ICreationEffects creationEffects,
                ICreationResult templateCreationResult,
                string outputBasePath)
            {
                throw new Exception("post action exception");
            }
        }
    }
}
