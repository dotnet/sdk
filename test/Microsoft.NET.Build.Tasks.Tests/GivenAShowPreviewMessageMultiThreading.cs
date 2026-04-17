// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAShowPreviewMessageMultiThreading
    {
        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            // Verify that ShowPreviewMessage implements IMultiThreadableTask
            typeof(ShowPreviewMessage).GetInterfaces().Should().Contain(typeof(IMultiThreadableTask));
        }

        [Fact]
        public void TaskEnvironmentPropertyExistsAndCanBeSet()
        {
            var task = new ShowPreviewMessage();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;

            // Verify TaskEnvironment property exists and can be assigned (for multi-threaded scenarios)
            task.Should().BeAssignableTo<IMultiThreadableTask>();
            
            var multiThreadable = task as IMultiThreadableTask;
            multiThreadable.Should().NotBeNull();
            
            // Verify property is settable
            var property = typeof(ShowPreviewMessage).GetProperty(nameof(IMultiThreadableTask.TaskEnvironment));
            property.Should().NotBeNull();
            property.CanRead.Should().BeTrue();
            property.CanWrite.Should().BeTrue();
        }

        [Fact]
        public void ExecutionWithoutPriorRegistrationLogsMessage()
        {
            // Test the normal execution path where no message has been logged yet
            var task = new ShowPreviewMessage();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;

            bool result = task.Execute();

            result.Should().BeTrue();
            engine.Messages.Should().HaveCount(1);
            engine.Messages[0].Importance.Should().Be(MessageImportance.High);
            
            // Verify that the task registered a guard object to prevent future duplicate messages
            engine.RegisteredTaskObjects.Should().HaveCount(1);
        }

        [Fact]
        public void SequentialExecutionAfterMessageDisplayedSkipsLogging()
        {
            // Test that after a message has been displayed (marked in the registered task objects),
            // subsequent executions don't log duplicate messages
            var engine = new MockBuildEngine();

            // First execution
            var task1 = new ShowPreviewMessage();
            task1.BuildEngine = engine;
            task1.Execute();

            engine.Messages.Should().HaveCount(1);
            engine.RegisteredTaskObjects.Should().HaveCount(1);

            // Second execution with fresh task but same engine
            var task2 = new ShowPreviewMessage();
            task2.BuildEngine = engine;
            task2.Execute();

            // Message count should still be 1 because the guard is still in place
            engine.Messages.Should().HaveCount(1, 
                because: "Second execution should find the message already displayed marker in registered task objects");
        }

        [Fact]
        public void MessageFormattingIsCorrect()
        {
            // Verify that the task logs the correct message with high importance
            var task = new ShowPreviewMessage();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;

            task.Execute();

            engine.Messages.Should().HaveCount(1);
            var message = engine.Messages[0];
            message.Importance.Should().Be(MessageImportance.High);
            // The message content comes from Strings.UsingPreviewSdk resource
            message.Message.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void MultipleEnginesCanExecuteTasksIndependently()
        {
            // Verify that different build engines maintain separate message-displayed guards
            var engine1 = new MockBuildEngine();
            var engine2 = new MockBuildEngine();

            // Execute with first engine
            var task1 = new ShowPreviewMessage();
            task1.BuildEngine = engine1;
            task1.Execute();

            engine1.Messages.Should().HaveCount(1);
            engine2.Messages.Should().HaveCount(0);

            // Execute with second engine - should also log because it's a different engine
            var task2 = new ShowPreviewMessage();
            task2.BuildEngine = engine2;
            task2.Execute();

            engine1.Messages.Should().HaveCount(1);
            engine2.Messages.Should().HaveCount(1, 
                because: "Each engine should maintain its own registered task objects");
        }
    }
}
