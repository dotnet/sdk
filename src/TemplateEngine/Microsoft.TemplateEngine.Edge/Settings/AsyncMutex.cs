// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    /// <summary>
    /// Helper class to work with <see cref="Mutex"/> in <c>async</c> method, since <c>await</c>
    /// can switch to different thread and <see cref="Mutex.ReleaseMutex"/> must be called from same thread.
    /// Hence this helper class.
    /// </summary>
    internal sealed class AsyncMutex : IDisposable
    {
        private readonly TaskCompletionSource<AsyncMutex> _taskCompletionSource;
        private readonly ManualResetEvent _blockReleasingMutex = new ManualResetEvent(false);
        private readonly string _mutexName;
        private readonly CancellationToken _token;
        private volatile bool _disposed;
        private volatile bool _isLocked = true;

        public bool IsLocked { get { return _isLocked; } }

        private AsyncMutex(string mutexName, CancellationToken token)
        {
            _mutexName = mutexName;
            _token = token;
            _taskCompletionSource = new TaskCompletionSource<AsyncMutex>();
            ThreadPool.QueueUserWorkItem(WaitLoop);
        }

        public static Task<AsyncMutex> WaitAsync(string mutexName, CancellationToken token)
        {
            var mutex = new AsyncMutex(mutexName, token);
            return mutex._taskCompletionSource.Task;
        }

        private void WaitLoop(object state)
        {
            var mutex = new Mutex(false, _mutexName);
            var mutexAcquired = false;
            try
            {
                while (true)
                {
                    if (_token.IsCancellationRequested)
                    {
                        _taskCompletionSource.SetCanceled();
                        return;
                    }
                    if (mutex.WaitOne(100))
                    {
                        mutexAcquired = true;
                        break;
                    }
                }
                _taskCompletionSource.SetResult(this);
                _blockReleasingMutex.WaitOne();
            }
            finally
            {
                if (mutexAcquired)
                {
                    mutex.ReleaseMutex();
                }
                mutex.Dispose();
                _blockReleasingMutex.Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _isLocked = false;

            _blockReleasingMutex.Set();
        }
    }
}
