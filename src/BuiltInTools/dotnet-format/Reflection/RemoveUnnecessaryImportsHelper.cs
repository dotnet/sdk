// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Reflection;

namespace Microsoft.CodeAnalysis.Tools.Reflection
{
    internal static class RemoveUnnecessaryImportsHelper
    {
        private static readonly Assembly? s_microsoftCodeAnalysisFeaturesAssembly = Assembly.Load(new AssemblyName("Microsoft.CodeAnalysis.Features"));
        private static readonly Type? s_abstractRemoveUnnecessaryImportsCodeFixProviderType = s_microsoftCodeAnalysisFeaturesAssembly?.GetType("Microsoft.CodeAnalysis.RemoveUnnecessaryImports.AbstractRemoveUnnecessaryImportsCodeFixProvider");
        private static readonly MethodInfo? s_removeUnnecessaryImportsAsyncMethod = s_abstractRemoveUnnecessaryImportsCodeFixProviderType?.GetMethod("RemoveUnnecessaryImportsAsync", BindingFlags.Static | BindingFlags.NonPublic);

        public static async Task<Document?> RemoveUnnecessaryImportsAsync(Document document, CancellationToken cancellationToken)
        {
            if (s_removeUnnecessaryImportsAsyncMethod is null)
            {
                return document;
            }

            return await (Task<Document>)s_removeUnnecessaryImportsAsyncMethod.Invoke(obj: null, new object[] { document, cancellationToken })!;
        }
    }
}
