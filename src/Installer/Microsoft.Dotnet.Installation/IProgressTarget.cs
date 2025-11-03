// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Dotnet.Installation;

public interface IProgressTarget
{
    public IProgressReporter CreateProgressReporter();
}

public interface IProgressReporter : IDisposable
{
    public IProgressTask AddTask(string description, double maxValue);
}

public interface IProgressTask
{
    string Description { get; set; }
    double Value { get; set; }
    double MaxValue { get; set; }


}

public class NullProgressTarget : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new NullProgressReporter();
    class NullProgressReporter : IProgressReporter
    {
        public void Dispose()
        {
        }
        public IProgressTask AddTask(string description, double maxValue)
        {
            return new NullProgressTask(description);
        }
    }
    class NullProgressTask : IProgressTask
    {
        public NullProgressTask(string description)
        {
            Description = description;
        }

        public double Value { get; set; }
        public string Description { get; set; }
        public double MaxValue { get; set; }
    }
}
