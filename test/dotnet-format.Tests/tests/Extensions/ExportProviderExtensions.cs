// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using System.Composition.Hosting.Core;
using System.Reflection;
using Microsoft.VisualStudio.Composition;

namespace Microsoft.CodeAnalysis.Tools.Tests
{
    internal static class ExportProviderExtensions
    {
        public static CompositionContext AsCompositionContext(this ExportProvider exportProvider)
        {
            return new CompositionContextShim(exportProvider);
        }

        private class CompositionContextShim : CompositionContext
        {
            private readonly ExportProvider _exportProvider;

            public CompositionContextShim(ExportProvider exportProvider)
            {
                _exportProvider = exportProvider;
            }

            public override bool TryGetExport(CompositionContract contract, out object export)
            {
                var importMany = contract.MetadataConstraints.Contains(new KeyValuePair<string, object>("IsImportMany", true));
                var (contractType, metadataType, isArray) = GetContractType(contract.ContractType, importMany);

                if (metadataType != null)
                {
                    var methodInfo = (from method in _exportProvider.GetType().GetTypeInfo().GetMethods()
                                      where method.Name == nameof(ExportProvider.GetExports)
                                      where method.IsGenericMethod && method.GetGenericArguments().Length == 2
                                      where method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(string)
                                      select method).Single();
                    var parameterizedMethod = methodInfo.MakeGenericMethod(contractType, metadataType);
                    export = parameterizedMethod.Invoke(_exportProvider, new[] { contract.ContractName });
                }
                else if (!isArray)
                {
                    var methodInfo = (from method in _exportProvider.GetType().GetTypeInfo().GetMethods()
                                      where method.Name == nameof(ExportProvider.GetExports)
                                      where method.IsGenericMethod && method.GetGenericArguments().Length == 1
                                      where method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(string)
                                      select method).Single();
                    var parameterizedMethod = methodInfo.MakeGenericMethod(contractType);
                    export = parameterizedMethod.Invoke(_exportProvider, new[] { contract.ContractName });
                }
                else
                {
                    var methodInfo = (from method in _exportProvider.GetType().GetTypeInfo().GetMethods()
                                      where method.Name == nameof(ExportProvider.GetExportedValues)
                                      where method.IsGenericMethod && method.GetGenericArguments().Length == 1
                                      where method.GetParameters().Length == 0
                                      select method).Single();
                    var parameterizedMethod = methodInfo.MakeGenericMethod(contractType);
                    export = parameterizedMethod.Invoke(_exportProvider, null);
                }

                return true;
            }

            private (Type exportType, Type metadataType, bool isArray) GetContractType(Type contractType, bool importMany)
            {
                if (importMany && contractType.BaseType == typeof(Array))
                {
                    return (contractType.GetElementType(), null, true);
                }

                if (importMany && contractType.IsConstructedGenericType)
                {
                    if (contractType.GetGenericTypeDefinition() == typeof(IList<>)
                        || contractType.GetGenericTypeDefinition() == typeof(ICollection<>)
                        || contractType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        contractType = contractType.GenericTypeArguments[0];
                    }
                }

                if (contractType.IsConstructedGenericType)
                {
                    if (contractType.GetGenericTypeDefinition() == typeof(Lazy<>))
                    {
                        return (contractType.GenericTypeArguments[0], null, false);
                    }
                    else if (contractType.GetGenericTypeDefinition() == typeof(Lazy<,>))
                    {
                        return (contractType.GenericTypeArguments[0], contractType.GenericTypeArguments[1], false);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }

                throw new NotSupportedException();
            }
        }
    }
}
