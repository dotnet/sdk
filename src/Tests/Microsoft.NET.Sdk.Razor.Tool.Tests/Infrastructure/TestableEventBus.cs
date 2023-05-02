﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.NET.Sdk.Razor.Tool;

namespace Microsoft.NET.Sdk.Razor.Tool.Tests
{
    internal class TestableEventBus : EventBus
    {
        public event EventHandler Listening;
        public event EventHandler CompilationComplete;

        public int ListeningCount { get; private set; }

        public int ConnectionCount { get; private set; }

        public int CompletedCount { get; private set; }

        public DateTime? LastProcessedTime { get; private set; }

        public TimeSpan? KeepAlive { get; private set; }

        public bool HasDetectedBadConnection { get; private set; }

        public bool HitKeepAliveTimeout { get; private set; }

        public override void ConnectionListening()
        {
            ListeningCount++;
            Listening?.Invoke(this, EventArgs.Empty);
        }

        public override void ConnectionReceived()
        {
            ConnectionCount++;
        }

        public override void ConnectionCompleted(int count)
        {
            CompletedCount += count;
            LastProcessedTime = DateTime.Now;
        }

        public override void CompilationCompleted()
        {
            CompilationComplete?.Invoke(this, EventArgs.Empty);
        }

        public override void UpdateKeepAlive(TimeSpan timeSpan)
        {
            KeepAlive = timeSpan;
        }

        public override void ConnectionRudelyEnded()
        {
            HasDetectedBadConnection = true;
        }

        public override void KeepAliveReached()
        {
            HitKeepAliveTimeout = true;
        }
    }
}
