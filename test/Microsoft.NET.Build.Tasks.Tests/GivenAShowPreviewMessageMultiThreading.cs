// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAShowPreviewMessageMultiThreading
    {
        [Fact]
        public void HasMultiThreadableTaskAttribute()
        {
            typeof(ShowPreviewMessage)
                .GetCustomAttributes(typeof(MSBuildMultiThreadableTaskAttribute), inherit: false)
                .Should()
                .ContainSingle();
        }

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            typeof(ShowPreviewMessage).GetInterfaces().Should().Contain(typeof(IMultiThreadableTask));
        }

        [Fact]
        public void ExecuteSucceedsWhenTaskEnvironmentIsSupplied()
        {
            var engine = new MockBuildEngine();
            var taskEnvironment = TaskEnvironmentHelper.CreateForTest();
            var task = CreateTask(engine, taskEnvironment);

            task.Execute().Should().BeTrue();

            ((IMultiThreadableTask)task).TaskEnvironment.Should().BeSameAs(taskEnvironment);
            engine.Messages.Should().HaveCount(1);
            engine.RegisteredTaskObjects.Should().HaveCount(1,
                because: "TaskEnvironment injection should not change the registered task object guard behavior");
        }

        [Fact]
        public void ExecutionWithoutPriorRegistrationLogsMessage()
        {
            var engine = new MockBuildEngine();
            var task = CreateTask(engine);

            bool result = task.Execute();

            result.Should().BeTrue();
            engine.Messages.Should().HaveCount(1);
            engine.Messages[0].Importance.Should().Be(MessageImportance.High);
            engine.RegisteredTaskObjects.Should().HaveCount(1);
        }

        [Fact]
        public void SequentialExecutionAfterMessageDisplayedSkipsLogging()
        {
            var engine = new MockBuildEngine();

            var task1 = CreateTask(engine);
            task1.Execute();

            engine.Messages.Should().HaveCount(1);
            engine.RegisteredTaskObjects.Should().HaveCount(1);

            var task2 = CreateTask(engine);
            task2.Execute();

            engine.Messages.Should().HaveCount(1,
                because: "Second execution should find the message already displayed marker in registered task objects");
        }

        [Fact]
        public void MessageFormattingIsCorrect()
        {
            var engine = new MockBuildEngine();
            var task = CreateTask(engine);

            task.Execute();

            engine.Messages.Should().HaveCount(1);
            var message = engine.Messages[0];
            message.Importance.Should().Be(MessageImportance.High);
            message.Message.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void MultipleEnginesCanExecuteTasksIndependently()
        {
            var engine1 = new MockBuildEngine();
            var engine2 = new MockBuildEngine();

            var task1 = CreateTask(engine1);
            task1.Execute();

            engine1.Messages.Should().HaveCount(1);
            engine2.Messages.Should().HaveCount(0);

            var task2 = CreateTask(engine2);
            task2.Execute();

            engine1.Messages.Should().HaveCount(1);
            engine2.Messages.Should().HaveCount(1,
                because: "Each engine should maintain its own registered task objects");
        }

        [Fact]
        public void ConcurrentExecutionWithSharedEngineLogsMessageOnce()
        {
            const int taskCount = 128;
            var engine = new MockBuildEngine();
            using var start = new ManualResetEventSlim(false);

            var executions = Enumerable.Range(0, taskCount)
                .Select(_ => Task.Run(() =>
                {
                    start.Wait();
                    var task = CreateTask(engine);

                    task.Execute().Should().BeTrue();
                }))
                .ToArray();

            start.Set();
            Action waitForExecutions = () => Task.WaitAll(executions);

            waitForExecutions.Should().NotThrow();
            engine.Messages.Should().HaveCount(1);
            engine.RegisteredTaskObjects.Should().HaveCount(1);
            engine.RegisteredTaskObjectsQueries.Should().Be(taskCount,
                because: "each concurrent task should check the shared engine marker under the task lock");
        }

        private static ShowPreviewMessage CreateTask(MockBuildEngine engine, TaskEnvironment? taskEnvironment = null)
        {
            var task = new ShowPreviewMessage
            {
                BuildEngine = engine
            };

            if (taskEnvironment is not null)
            {
                ((IMultiThreadableTask)task).TaskEnvironment = taskEnvironment;
            }

            return task;
        }
    }
}
