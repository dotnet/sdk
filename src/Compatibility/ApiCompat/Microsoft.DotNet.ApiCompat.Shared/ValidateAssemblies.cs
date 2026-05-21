// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiCompatibility.Runner;

namespace Microsoft.DotNet.ApiCompat
{
    internal static class ValidateAssemblies
    {
        public static int Run(Func<ISuppressionEngine, ISuppressibleLog> logFactory,
            ValidateAssembliesOptions options)
        {
            // Initialize the service provider
            ApiCompatServiceProvider serviceProvider = new(logFactory,
                () => SuppressionFileHelper.CreateSuppressionEngine(options.SuppressionFiles, options.NoWarn, options.GenerateSuppressionFile),
                (log) => new RuleFactory(log,
                    options.EnableRuleAttributesMustMatch,
                    options.EnableRuleCannotChangeParameterName),
                options.RespectInternals,
                options.ExcludeAttributesFiles);

            IApiCompatRunner apiCompatRunner = serviceProvider.ApiCompatRunner;
            ApiCompatRunnerOptions apiCompatOptions = new(options.EnableStrictMode);

            // Optionally provide a string transformer if a transformation pattern is passed in.
            RegexStringTransformer? leftAssembliesStringTransformer = options.LeftAssembliesTransformationPatterns != null ? new RegexStringTransformer(options.LeftAssembliesTransformationPatterns) : null;
            RegexStringTransformer? rightAssembliesStringTransformer = options.RightAssembliesTransformationPatterns != null ? new RegexStringTransformer(options.RightAssembliesTransformationPatterns) : null;

            if (options.CreateWorkItemPerAssembly)
            {
                if (options.LeftAssemblies.Length != options.RightAssemblies.Length)
                {
                    throw new Exception(CommonResources.CreateWorkItemPerAssemblyAssembliesNotEqual);
                }

                for (int i = 0; i < options.LeftAssemblies.Length; i++)
                {
                    List<MetadataInformation> leftMetadataInformation = GetMetadataInformation(options.LeftAssemblies[i], GetAssemblyReferences(options.LeftAssembliesReferences, i), leftAssembliesStringTransformer);
                    List<MetadataInformation> rightMetadataInformation = GetMetadataInformation(options.RightAssemblies[i], GetAssemblyReferences(options.RightAssembliesReferences, i), rightAssembliesStringTransformer);

                    // Enqueue the work item
                    ApiCompatRunnerWorkItem workItem = new(leftMetadataInformation, apiCompatOptions, rightMetadataInformation);
                    apiCompatRunner.EnqueueWorkItem(workItem);
                }
            }
            else
            {
                // Create the work item that corresponds to the passed in left assembly.
                List<MetadataInformation> leftAssembliesMetadataInformation = new(options.LeftAssemblies.Length);
                for (int i = 0; i < options.LeftAssemblies.Length; i++)
                {
                    leftAssembliesMetadataInformation.AddRange(GetMetadataInformation(options.LeftAssemblies[i], GetAssemblyReferences(options.LeftAssembliesReferences, i), leftAssembliesStringTransformer));
                }

                List<MetadataInformation> rightAssembliesMetadataInformation = new(options.RightAssemblies.Length);
                for (int i = 0; i < options.RightAssemblies.Length; i++)
                {
                    rightAssembliesMetadataInformation.AddRange(GetMetadataInformation(options.RightAssemblies[i], GetAssemblyReferences(options.RightAssembliesReferences, i), rightAssembliesStringTransformer));
                }

                // Enqueue the work item
                ApiCompatRunnerWorkItem workItem = new(leftAssembliesMetadataInformation, apiCompatOptions, rightAssembliesMetadataInformation);
                apiCompatRunner.EnqueueWorkItem(workItem);
            }

            // Execute the enqueued work item(s).
            apiCompatRunner.ExecuteWorkItems();

            SuppressionFileHelper.LogApiCompatSuccessOrFailure(options.GenerateSuppressionFile, serviceProvider.SuppressibleLog);

            if (options.GenerateSuppressionFile)
            {
                SuppressionFileHelper.GenerateSuppressionFile(serviceProvider.SuppressionEngine,
                    serviceProvider.SuppressibleLog,
                    options.PreserveUnnecessarySuppressions,
                    options.SuppressionFiles,
                    options.SuppressionOutputFile);
            }
            else if (!options.PermitUnnecessarySuppressions)
            {
                SuppressionFileHelper.ValidateUnnecessarySuppressions(serviceProvider.SuppressionEngine, serviceProvider.SuppressibleLog);
            }

            return serviceProvider.SuppressibleLog.HasLoggedErrorSuppressions ? 1 : 0;
        }

        private static string[]? GetAssemblyReferences(string[][]? assemblyReferences, int counter)
        {
            if (assemblyReferences == null || assemblyReferences.Length == 0)
                return null;

            if (assemblyReferences.Length > counter)
            {
                return assemblyReferences[counter];
            }

            // If explicit assembly references weren't provided for an assembly, return the ones provided first
            // so that consumers can provide one shareable set of references for all left/right inputs.
            return assemblyReferences[0];
        }

        private static List<MetadataInformation> GetMetadataInformation(string path,
            IEnumerable<string>? assemblyReferences,
            RegexStringTransformer? regexStringTransformer)
        {
            List<MetadataInformation> metadataInformation = [];
            foreach (string assembly in GetFilesFromPath(path))
            {
                metadataInformation.Add(new MetadataInformation(
                    assemblyName: Path.GetFileNameWithoutExtension(assembly),
                    assemblyId: regexStringTransformer?.Transform(assembly) ?? assembly,
                    fullPath: assembly,
                    references: assemblyReferences));
            }

            return metadataInformation;
        }

        private static IEnumerable<string> GetFilesFromPath(string path)
        {
            // Check if the given path is a directory
            if (Directory.Exists(path))
            {
                return Directory.EnumerateFiles(path, "*.dll");
            }

            // If the path isn't a directory, see if it's a glob expression.
            string filename = Path.GetFileName(path);
            if (filename.Contains('*'))
            {
                string? directoryName = Path.GetDirectoryName(path);
                if (directoryName != null)
                {
                    try
                    {
                        return Directory.EnumerateFiles(directoryName, filename);
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }

            return [path];
        }
    }
}
