// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Tasks.DoNotUseConfigureAwaitWithSuppressThrowing,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Tasks.UnitTests
{
    public class DoNotUseConfigureAwaitWithSuppressThrowingTests
    {
        [Fact]
        public async Task ValidateCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;
                using System.Runtime.CompilerServices;
                using System.Threading.Tasks;
                
                namespace System.Threading.Tasks
                {
                    [Flags]
                    public enum ConfigureAwaitOptions
                    {
                        None = 0,
                        ContinueOnCapturedContext = 1,
                        SuppressThrowing = 2,
                        ForceYielding = 4,
                    }
                
                    public class Task
                    {
                        public ConfiguredTaskAwaitable ConfigureAwait(bool continueOnCapturedContext) => throw null;
                        public ConfiguredTaskAwaitable ConfigureAwait(ConfigureAwaitOptions options) => throw null;
                    }
                
                    public class Task<TResult> : Task
                    {
                        public ConfiguredTaskAwaitable<TResult> ConfigureAwait(bool continueOnCapturedContext) => throw null;
                        public ConfiguredTaskAwaitable<TResult> ConfigureAwait(ConfigureAwaitOptions options) => throw null;
                    }

                    public class DerivedGenericTask : Task<int>
                    {
                    }
                }

                class C
                {
                    public void Test(ConfigureAwaitOptions options)
                    {
                        // No diagnostics
                        Task nonGenericTask = new Task();
                        nonGenericTask.ConfigureAwait(false);
                        nonGenericTask.ConfigureAwait(true);
                        nonGenericTask.ConfigureAwait(ConfigureAwaitOptions.None);
                        nonGenericTask.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext);
                        nonGenericTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                        nonGenericTask.ConfigureAwait( (   (( (     ConfigureAwaitOptions.SuppressThrowing    ))  )   ));
                        nonGenericTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
                        nonGenericTask.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.ForceYielding);
                        nonGenericTask.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ForceYielding);
                        nonGenericTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ForceYielding);
                        nonGenericTask.ConfigureAwait((ConfigureAwaitOptions)0x7);
                        nonGenericTask.ConfigureAwait(((ConfigureAwaitOptions)0x1) | ((ConfigureAwaitOptions)0x2));
                        nonGenericTask.ConfigureAwait(options);
                
                        // Diagnostics when SuppressThrowing is used
                        Task<int> genericTask = new Task<int>();
                        genericTask.ConfigureAwait(false);
                        genericTask.ConfigureAwait(true);
                        genericTask.ConfigureAwait(ConfigureAwaitOptions.None);
                        genericTask.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext);
                        genericTask.ConfigureAwait([|ConfigureAwaitOptions.SuppressThrowing|]);
                        genericTask.ConfigureAwait( (   (( (     [|ConfigureAwaitOptions.SuppressThrowing|]    ))  )   ));
                        genericTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
                        genericTask.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.ForceYielding);
                        genericTask.ConfigureAwait([|ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ForceYielding|]);
                        genericTask.ConfigureAwait([|ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ForceYielding|]);
                        genericTask.ConfigureAwait([|(ConfigureAwaitOptions)0x7|]);
                        genericTask.ConfigureAwait([|((ConfigureAwaitOptions)0x1) | ((ConfigureAwaitOptions)0x2)|]);
                        genericTask.ConfigureAwait(options);

                        DerivedGenericTask derivedGenericTask = new DerivedGenericTask();
                        derivedGenericTask.ConfigureAwait(false);
                        derivedGenericTask.ConfigureAwait(true);
                        derivedGenericTask.ConfigureAwait(ConfigureAwaitOptions.None);
                        derivedGenericTask.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext);
                        derivedGenericTask.ConfigureAwait([|ConfigureAwaitOptions.SuppressThrowing|]);
                        derivedGenericTask.ConfigureAwait( (   (( (     [|ConfigureAwaitOptions.SuppressThrowing|]    ))  )   ));
                        derivedGenericTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
                        derivedGenericTask.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.ForceYielding);
                        derivedGenericTask.ConfigureAwait([|ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ForceYielding|]);
                        derivedGenericTask.ConfigureAwait([|ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ForceYielding|]);
                        derivedGenericTask.ConfigureAwait([|(ConfigureAwaitOptions)0x7|]);
                        derivedGenericTask.ConfigureAwait([|((ConfigureAwaitOptions)0x1) | ((ConfigureAwaitOptions)0x2)|]);
                        derivedGenericTask.ConfigureAwait(options);

                        // No diagnostics
                        ((Task)genericTask).ConfigureAwait(false);
                        ((Task)genericTask).ConfigureAwait(true);
                        ((Task)genericTask).ConfigureAwait(ConfigureAwaitOptions.None);
                        ((Task)genericTask).ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext);
                        ((Task)genericTask).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                        ((Task)genericTask).ConfigureAwait( (   (( (     ConfigureAwaitOptions.SuppressThrowing    ))  )   ));
                        ((Task)genericTask).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
                        ((Task)genericTask).ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.ForceYielding);
                        ((Task)genericTask).ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ForceYielding);
                        ((Task)genericTask).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ForceYielding);
                        ((Task)genericTask).ConfigureAwait((ConfigureAwaitOptions)0x7);
                        ((Task)genericTask).ConfigureAwait(((ConfigureAwaitOptions)0x1) | ((ConfigureAwaitOptions)0x2));
                        ((Task)genericTask).ConfigureAwait(options);
                    }
                }
                """);
        }
    }
}
