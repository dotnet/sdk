﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using ProjectCollection = Microsoft.Build.Evaluation.ProjectCollection;

#nullable disable

namespace Msbuild.Tests.Utilities
{
    /*
     * Class:   MockLogger
     *
     * Mock logger class. Keeps track of errors and warnings and also builds
     * up a raw string (fullLog) that contains all messages, warnings, errors.
     * Thread-safe.
     *
     * This is a copy of the MSBuild MockLogger.
     */
    public sealed class MockLogger : ILogger
    {
        #region Properties

        private readonly object _lockObj = new object();  // Protects _fullLog, _testOutputHelper, lists, counts
        private StringBuilder _fullLog = new StringBuilder();
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly bool _profileEvaluation;
        private readonly bool _printEventsToStdout;

        /// <summary>
        /// Should the build finished event be logged in the log file. This is to work around the fact we have different
        /// localized strings between env and xmake for the build finished event.
        /// </summary>
        public bool LogBuildFinished { get; set; } = true;

        /*
         * Method:  ErrorCount
         *
         * The count of all errors seen so far.
         *
         */
        public int ErrorCount { get; private set; }

        /*
         * Method:  WarningCount
         *
         * The count of all warnings seen so far.
         *
         */
        public int WarningCount { get; private set; }

        /// <summary>
        /// Return the list of logged errors
        /// </summary>
        public List<BuildErrorEventArgs> Errors { get; } = new List<BuildErrorEventArgs>();

        /// <summary>
        /// Returns the list of logged warnings
        /// </summary>
        public List<BuildWarningEventArgs> Warnings { get; } = new List<BuildWarningEventArgs>();

        /// <summary>
        /// When set to true, allows task crashes to be logged without causing an assert.
        /// </summary>
        public bool AllowTaskCrashes { get; set; }

        /// <summary>
        /// List of ExternalProjectStarted events
        /// </summary>
        public List<ExternalProjectStartedEventArgs> ExternalProjectStartedEvents { get; } = new List<ExternalProjectStartedEventArgs>();

        /// <summary>
        /// List of ExternalProjectFinished events
        /// </summary>
        public List<ExternalProjectFinishedEventArgs> ExternalProjectFinishedEvents { get; } = new List<ExternalProjectFinishedEventArgs>();

        /// <summary>
        /// List of ProjectStarted events
        /// </summary>
        public List<ProjectEvaluationStartedEventArgs> EvaluationStartedEvents { get; } = new List<ProjectEvaluationStartedEventArgs>();

        /// <summary>
        /// List of ProjectFinished events
        /// </summary>
        public List<ProjectEvaluationFinishedEventArgs> EvaluationFinishedEvents { get; } = new List<ProjectEvaluationFinishedEventArgs>();

        /// <summary>
        /// List of ProjectStarted events
        /// </summary>
        public List<ProjectStartedEventArgs> ProjectStartedEvents { get; } = new List<ProjectStartedEventArgs>();

        /// <summary>
        /// List of ProjectFinished events
        /// </summary>
        public List<ProjectFinishedEventArgs> ProjectFinishedEvents { get; } = new List<ProjectFinishedEventArgs>();

        /// <summary>
        /// List of TargetStarted events
        /// </summary>
        public List<TargetStartedEventArgs> TargetStartedEvents { get; } = new List<TargetStartedEventArgs>();

        /// <summary>
        /// List of TargetFinished events
        /// </summary>
        public List<TargetFinishedEventArgs> TargetFinishedEvents { get; } = new List<TargetFinishedEventArgs>();

        /// <summary>
        /// List of TaskStarted events
        /// </summary>
        public List<TaskStartedEventArgs> TaskStartedEvents { get; } = new List<TaskStartedEventArgs>();

        /// <summary>
        /// List of TaskFinished events
        /// </summary>
        public List<TaskFinishedEventArgs> TaskFinishedEvents { get; } = new List<TaskFinishedEventArgs>();

        /// <summary>
        /// List of BuildMessage events
        /// </summary>
        public List<BuildMessageEventArgs> BuildMessageEvents { get; } = new List<BuildMessageEventArgs>();

        /// <summary>
        /// List of BuildStarted events, thought we expect there to only be one, a valid check is to make sure this list is length 1
        /// </summary>
        public List<BuildStartedEventArgs> BuildStartedEvents { get; } = new List<BuildStartedEventArgs>();

        /// <summary>
        /// List of BuildFinished events, thought we expect there to only be one, a valid check is to make sure this list is length 1
        /// </summary>
        public List<BuildFinishedEventArgs> BuildFinishedEvents { get; } = new List<BuildFinishedEventArgs>();

        public List<BuildEventArgs> AllBuildEvents { get; } = new List<BuildEventArgs>();

        /*
         * Method:  FullLog
         *
         * The raw concatenation of all messages, errors and warnings seen so far.
         *
         */
        public string FullLog
        {
            get
            {
                lock (_lockObj)
                {
                    return _fullLog.ToString();
                }
            }
        }

        #endregion

        #region Minimal ILogger implementation

        /*
         * Property:    Verbosity
         *
         * The level of detail to show in the event log.
         *
         */
        public LoggerVerbosity Verbosity
        {
            get => LoggerVerbosity.Normal;
            set {/* do nothing */}
        }

        /*
         * Property:    Parameters
         * 
         * The mock logger does not take parameters.
         * 
         */
        public string Parameters
        {
            get => null;
            set {/* do nothing */}
        }

        /*
         * Method:  Initialize
         *
         * Add a new build event.
         *
         */
        public void Initialize(IEventSource eventSource)
        {
            eventSource.AnyEventRaised += LoggerEventHandler;

            if (_profileEvaluation)
            {
                var eventSource3 = eventSource as IEventSource3;
                eventSource3.Should().NotBeNull();
                eventSource3.IncludeEvaluationProfiles();
            }
        }

        /// <summary>
        /// Clears the content of the log "file"
        /// </summary>
        public void ClearLog()
        {
            lock (_lockObj)
            {
                _fullLog = new StringBuilder();
            }
        }

        /*
         * Method:  Shutdown
         * 
         * The mock logger does not need to release any resources.
         * 
         */
        public void Shutdown()
        {
            // do nothing
        }
        #endregion

        public MockLogger(ITestOutputHelper testOutputHelper = null, bool profileEvaluation = false, bool printEventsToStdout = true)
        {
            _testOutputHelper = testOutputHelper;
            _profileEvaluation = profileEvaluation;
            _printEventsToStdout = printEventsToStdout;
        }

        public List<Action<object, BuildEventArgs>> AdditionalHandlers { get; set; } = new List<Action<object, BuildEventArgs>>();

        /*
         * Method:  LoggerEventHandler
         *
         * Receives build events and logs them the way we like.
         *
         */
        public void LoggerEventHandler(object sender, BuildEventArgs eventArgs)
        {
            lock (_lockObj)
            {
                AllBuildEvents.Add(eventArgs);

                foreach (Action<object, BuildEventArgs> handler in AdditionalHandlers)
                {
                    handler(sender, eventArgs);
                }

                // Log the string part of the event
                switch (eventArgs)
                {
                    case BuildWarningEventArgs w:
                        // hack: disregard the MTA warning.
                        // need the second condition to pass on ploc builds
                        if (w.Code != "MSB4056" && !w.Message.Contains("MSB4056"))
                        {
                            string logMessage = $"{w.File}({w.LineNumber},{w.ColumnNumber}): {w.Subcategory} warning {w.Code}: {w.Message}";

                            _fullLog.AppendLine(logMessage);
                            _testOutputHelper?.WriteLine(logMessage);

                            ++WarningCount;
                            Warnings.Add(w);
                        }
                        break;
                    case BuildErrorEventArgs e:
                    {
                        string logMessage = $"{e.File}({e.LineNumber},{e.ColumnNumber}): {e.Subcategory} error {e.Code}: {e.Message}";
                        _fullLog.AppendLine(logMessage);
                        _testOutputHelper?.WriteLine(logMessage);

                        ++ErrorCount;
                        Errors.Add(e);
                        break;
                    }
                    default:
                    {
                        // Log the message unless we are a build finished event and logBuildFinished is set to false.
                        bool logMessage = !(eventArgs is BuildFinishedEventArgs) || LogBuildFinished;
                        if (logMessage)
                        {
                            _fullLog.AppendLine(eventArgs.Message);
                            _testOutputHelper?.WriteLine(eventArgs.Message);
                        }
                        break;
                    }
                }

                // Log the specific type of event it was
                switch (eventArgs)
                {
                    case ExternalProjectStartedEventArgs args:
                    {
                        ExternalProjectStartedEvents.Add(args);
                        break;
                    }
                    case ExternalProjectFinishedEventArgs finishedEventArgs:
                    {
                        ExternalProjectFinishedEvents.Add(finishedEventArgs);
                        break;
                    }
                    case ProjectEvaluationStartedEventArgs evaluationStartedEventArgs:
                    {
                        EvaluationStartedEvents.Add(evaluationStartedEventArgs);
                        break;
                    }
                    case ProjectEvaluationFinishedEventArgs evaluationFinishedEventArgs:
                    {
                        EvaluationFinishedEvents.Add(evaluationFinishedEventArgs);
                        break;
                    }
                    case ProjectStartedEventArgs startedEventArgs:
                    {
                        ProjectStartedEvents.Add(startedEventArgs);
                        break;
                    }
                    case ProjectFinishedEventArgs finishedEventArgs:
                    {
                        ProjectFinishedEvents.Add(finishedEventArgs);
                        break;
                    }
                    case TargetStartedEventArgs targetStartedEventArgs:
                    {
                        TargetStartedEvents.Add(targetStartedEventArgs);
                        break;
                    }
                    case TargetFinishedEventArgs targetFinishedEventArgs:
                    {
                        TargetFinishedEvents.Add(targetFinishedEventArgs);
                        break;
                    }
                    case TaskStartedEventArgs taskStartedEventArgs:
                    {
                        TaskStartedEvents.Add(taskStartedEventArgs);
                        break;
                    }
                    case TaskFinishedEventArgs taskFinishedEventArgs:
                    {
                        TaskFinishedEvents.Add(taskFinishedEventArgs);
                        break;
                    }
                    case BuildMessageEventArgs buildMessageEventArgs:
                    {
                        BuildMessageEvents.Add(buildMessageEventArgs);
                        break;
                    }
                    case BuildStartedEventArgs buildStartedEventArgs:
                    {
                        BuildStartedEvents.Add(buildStartedEventArgs);
                        break;
                    }
                    case BuildFinishedEventArgs buildFinishedEventArgs:
                    {
                        BuildFinishedEvents.Add(buildFinishedEventArgs);

                        if (!AllowTaskCrashes)
                        {
                            // We should not have any task crashes. Sometimes a test will validate that their expected error
                            // code appeared, but not realize it then crashed.
                            AssertLogDoesntContain("MSB4018");
                        }

                        // We should not have any Engine crashes.
                        AssertLogDoesntContain("MSB0001");

                        // Console.Write in the context of a unit test is very expensive.  A hundred
                        // calls to Console.Write can easily take two seconds on a fast machine.  Therefore, only
                        // do the Console.Write once at the end of the build.

                        PrintFullLog();

                        break;
                    }
                }
            }
        }

        private void PrintFullLog()
        {
            if (_printEventsToStdout)
            {
                Console.Write(FullLog);
            }
        }

        // Lazy-init property returning the MSBuild engine resource manager
        private static ResourceManager EngineResourceManager => s_engineResourceManager ?? (s_engineResourceManager = new ResourceManager(
            "Microsoft.Build.Strings",
            typeof(ProjectCollection).GetTypeInfo().Assembly));

        private static ResourceManager s_engineResourceManager;

        // Gets the resource string given the resource ID
        public static string GetString(string stringId) => EngineResourceManager.GetString(stringId, CultureInfo.CurrentUICulture);

        /// <summary>
        /// Assert that the log file contains the given strings, in order.
        /// </summary>
        /// <param name="contains"></param>
        public void AssertLogContains(params string[] contains) => AssertLogContains(true, contains);

        /// <summary>
        /// Assert that the log file contains the given string, in order. Includes the option of case invariance
        /// </summary>
        /// <param name="isCaseSensitive">False if we do not care about case sensitivity</param>
        /// <param name="contains"></param>
        public void AssertLogContains(bool isCaseSensitive, params string[] contains)
        {
            lock (_lockObj)
            {
                var reader = new StringReader(FullLog);
                int index = 0;

                string currentLine = reader.ReadLine();
                if (!isCaseSensitive)
                {
                    currentLine = currentLine.ToUpper();
                }

                while (currentLine != null)
                {
                    string comparer = contains[index];
                    if (!isCaseSensitive)
                    {
                        comparer = comparer.ToUpper();
                    }

                    if (currentLine.Contains(comparer))
                    {
                        index++;
                        if (index == contains.Length) break;
                    }

                    currentLine = reader.ReadLine();
                    if (!isCaseSensitive)
                    {
                        currentLine = currentLine?.ToUpper();
                    }
                }

                if (index != contains.Length)
                {
                    if (_testOutputHelper != null)
                    {
                        _testOutputHelper.WriteLine(FullLog);
                    }
                    else
                    {
                        PrintFullLog();
                    }

                    Assert.True(
                        false,
                        $"Log was expected to contain '{contains[index]}', but did not. Full log:\n=======\n{FullLog}\n=======");
                }
            }
        }

        /// <summary>
        /// Assert that the log file does not contain the given string.
        /// </summary>
        /// <param name="contains"></param>
        public void AssertLogDoesntContain(string contains)
        {
            lock (_lockObj)
            {
                if (FullLog.Contains(contains))
                {
                    if (_testOutputHelper != null)
                    {
                        _testOutputHelper.WriteLine(FullLog);
                    }
                    else
                    {
                        PrintFullLog();
                    }

                    Assert.True(false, $"Log was not expected to contain '{contains}', but did.");
                }
            }
        }

        /// <summary>
        /// Assert that no errors were logged
        /// </summary>
        public void AssertNoErrors() => Assert.Equal(0, ErrorCount);

        /// <summary>
        /// Assert that no warnings were logged
        /// </summary>
        public void AssertNoWarnings() => Assert.Equal(0, WarningCount);

        public void AssertMessageCount(string message, int expectedCount)
        {
            var matches = Regex.Matches(FullLog, message);
            matches.Count.Should().Be(expectedCount);
        }
    }
}
