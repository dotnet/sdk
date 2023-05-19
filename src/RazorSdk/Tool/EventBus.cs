﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal abstract class EventBus
    {
        public static readonly EventBus Default = new DefaultEventBus();

        /// <summary>
        /// Called when the server updates the keep alive value.
        /// </summary>
        public virtual void UpdateKeepAlive(TimeSpan timeSpan)
        {
        }

        /// <summary>
        /// Called each time the server listens for new connections.
        /// </summary>
        public virtual void ConnectionListening()
        {
        }

        /// <summary>
        /// Called when a connection to the server occurs.
        /// </summary>
        public virtual void ConnectionReceived()
        {
        }

        /// <summary>
        /// Called when one or more connections have completed processing.  The number of connections
        /// processed is provided in <paramref name="count"/>.
        /// </summary>
        public virtual void ConnectionCompleted(int count)
        {
        }
        
        /// <summary>
        /// Called when a compilation is completed successfully and the response is written to the stream.
        /// </summary>
        public virtual void CompilationCompleted()
        {
        }

        /// <summary>
        /// Called when a bad client connection was detected and the server will be shutting down as a 
        /// result.
        /// </summary>
        public virtual void ConnectionRudelyEnded()
        {
        }

        /// <summary>
        /// Called when the server is shutting down because the keep alive timeout was reached.
        /// </summary>
        public virtual void KeepAliveReached()
        {
        }

        private class DefaultEventBus : EventBus
        {

        }
    }
}
