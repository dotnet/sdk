// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// Provides support for asynchronous lazy initialization.
    /// </summary>
    /// <typeparam name="T">The type to be lazily initialized.</typeparam>
    public class AsyncLazy<T> : Lazy<Task<T>>
    {
        // inspired by https://devblogs.microsoft.com/pfxteam/asynclazyt/

        /// <summary>
        /// Creates a lazy type that performs the value construction asynchronously on a first access.
        /// </summary>
        /// <param name="valueFactory">Synchronous value factory that will be executed asynchronously on first access in separate task.</param>
        public AsyncLazy(Func<T> valueFactory)
            : base(() => Task.Factory.StartNew(valueFactory, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default))
        { }

        /// <summary>
        /// Creates a lazy type that performs the value construction asynchronously on a first access.
        /// </summary>
        /// <param name="taskFactory">Asynchronous value factory that will be executed on a first access.</param>
        public AsyncLazy(Func<Task<T>> taskFactory)
            : base(() => Task.Factory.StartNew(
                () => taskFactory(), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default)
                .Unwrap())
        { }

        /// <summary>
        /// The awaiter to be awaited in order to trigger asynchronous value creation.
        /// </summary>
        /// <returns></returns>
        public TaskAwaiter<T> GetAwaiter() { return Value.GetAwaiter(); }
    }
}
