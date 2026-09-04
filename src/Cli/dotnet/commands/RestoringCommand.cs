// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.Restore;
using Microsoft.DotNet.Workloads.Workload.Install;

namespace Microsoft.DotNet.Tools
{
    public class RestoringCommand : MSBuildForwardingApp
    {
        public RestoreCommand SeparateRestoreCommand { get; }

        private bool AdvertiseWorkloadUpdates;

        public RestoringCommand(
            IEnumerable<string> msbuildArgs,
            bool noRestore,
            string msbuildPath = null,
            string userProfileDir = null,
            bool? advertiseWorkloadUpdates = null)
            : base(GetCommandArguments(msbuildArgs, noRestore), msbuildPath)
        {
            userProfileDir = CliFolderPathCalculator.DotnetUserProfileFolderPath;
            Task.Run(() => WorkloadManifestUpdater.BackgroundUpdateAdvertisingManifestsAsync(userProfileDir));
            SeparateRestoreCommand = GetSeparateRestoreCommand(msbuildArgs, noRestore, msbuildPath);
            AdvertiseWorkloadUpdates = advertiseWorkloadUpdates ?? msbuildArgs.All(arg => FlagsThatTriggerSilentRestore.All(f => !arg.Contains(f, StringComparison.OrdinalIgnoreCase)));

            if (!noRestore)
            {
                NuGetSignatureVerificationEnabler.ConditionallyEnable(this);
            }
        }

        private static IEnumerable<string> GetCommandArguments(
            IEnumerable<string> arguments,
            bool noRestore)
        {
            if (noRestore)
            {
                return arguments;
            }

            if (HasArgumentToExcludeFromRestore(arguments))
            {
                return Prepend("-nologo", arguments);
            }

            return Prepend("-restore", arguments);
        }

        private static RestoreCommand GetSeparateRestoreCommand(
            IEnumerable<string> arguments,
            bool noRestore,
            string msbuildPath)
        {
            if (noRestore || !HasArgumentToExcludeFromRestore(arguments))
            {
                return null;
            }

            IEnumerable<string> restoreArguments = ["-target:Restore"];
            if (arguments != null)
            {
                (var newArgumentsToAdd, var existingArgumentsToForward) = ProcessForwardedArgumentsForSeparateRestore(arguments);
                restoreArguments = [.. restoreArguments, .. newArgumentsToAdd, .. existingArgumentsToForward];
            }

            return new RestoreCommand(restoreArguments, msbuildPath);
        }

        private static IEnumerable<string> Prepend(string argument, IEnumerable<string> arguments)
            => new[] { argument }.Concat(arguments);

        private static bool HasArgumentToExcludeFromRestore(IEnumerable<string> arguments)
            => arguments.Any(a => IsExcludedFromRestore(a));

        private static readonly string[] switchPrefixes = ["-", "/", "--"];

        // these properties trigger a separate restore
        private static readonly string[] PropertiesToExcludeFromRestore =
        [
            "TargetFramework"
        ];

        private static readonly string[] FlagsThatTriggerSilentRestore =
            [
                "getProperty",
                "getItem",
                "getTargetResult"
            ];

        //  These arguments don't by themselves require that restore be run in a separate process,
        //  but if there is a separate restore process they shouldn't be passed to it
        private static readonly string[] FlagsToExcludeFromRestore =
        [
            ..FlagsThatTriggerSilentRestore,
            "t",
            "target",
            "consoleloggerparameters",
            "clp"
        ];

        private static List<string> FlagsToExcludeFromSeparateRestore =
            ComputeFlags(FlagsToExcludeFromRestore).ToList();

        private static List<string> FlagsThatTriggerSilentSeparateRestore =
            ComputeFlags(FlagsThatTriggerSilentRestore).ToList();

        private static List<string> PropertiesToExcludeFromSeparateRestore =
            ComputePropertySwitches(PropertiesToExcludeFromRestore).ToList();

        // We investigate the arguments we're about to send to a separate restore call and filter out
        // arguments that negatively influence the restore. In addition, some flags signal different modes of execution
        // that we need to compensate for, so we might yield new arguments that should be included in the overall restore call.
        private static (string[] newArgumentsToAdd, string[] existingArgumentsToForward) ProcessForwardedArgumentsForSeparateRestore(IEnumerable<string> forwardedArguments)
        {
            HashSet<string> newArgumentsToAdd = new();
            List<string> existingArgumentsToForward = new();

            foreach (var argument in forwardedArguments)
            {

                if (!IsExcludedFromSeparateRestore(argument) && !IsExcludedFromRestore(argument))
                {
                    existingArgumentsToForward.Add(argument);
                }

                if (TriggersSilentSeparateRestore(argument))
                {
                    newArgumentsToAdd.Add("-nologo");
                    newArgumentsToAdd.Add("-verbosity:quiet");
                }
            }
            return (newArgumentsToAdd.ToArray(), existingArgumentsToForward.ToArray());
        }
        private static IEnumerable<string> ComputePropertySwitches(string[] properties)
        {
            foreach (var prefix in switchPrefixes)
            {
                foreach (var property in properties)
                {
                    yield return $"{prefix}property:{property}=";
                    yield return $"{prefix}p:{property}=";
                }
            }
        }

        private static IEnumerable<string> ComputeFlags(string[] flags)
        {
            foreach (var prefix in switchPrefixes)
            {
                foreach (var flag in flags)
                {
                    yield return $"{prefix}{flag}:";
                }
            }
        }

        private static bool IsExcludedFromRestore(string argument)
            => PropertiesToExcludeFromSeparateRestore.Any(flag => argument.StartsWith(flag, StringComparison.OrdinalIgnoreCase));


        private static bool IsExcludedFromSeparateRestore(string argument)
            => FlagsToExcludeFromSeparateRestore.Any(p => argument.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        private static bool TriggersSilentSeparateRestore(string argument)
            => FlagsThatTriggerSilentSeparateRestore.Any(p => argument.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        public override int Execute()
        {
            int exitCode;
            if (SeparateRestoreCommand != null)
            {
                exitCode = SeparateRestoreCommand.Execute();
                if (exitCode != 0)
                {
                    return exitCode;
                }
            }

            exitCode = base.Execute();
            if (AdvertiseWorkloadUpdates)
            {
                WorkloadManifestUpdater.AdvertiseWorkloadUpdates();
            }
            return exitCode;
        }
    }
}
