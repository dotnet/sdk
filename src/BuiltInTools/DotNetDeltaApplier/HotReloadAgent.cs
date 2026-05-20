// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;

namespace Microsoft.Extensions.HotReload
{
    internal sealed class HotReloadAgent : IDisposable
    {
        private const string MetadataUpdaterTypeName = "System.Reflection.Metadata.MetadataUpdater";
        private const string ApplyUpdateMethodName = "ApplyUpdate";
        private const string GetCapabilitiesMethodName = "GetCapabilities";

        private delegate void ApplyUpdateDelegate(Assembly assembly, ReadOnlySpan<byte> metadataDelta, ReadOnlySpan<byte> ilDelta, ReadOnlySpan<byte> pdbDelta);

        private readonly AgentReporter _reporter = new();
        private readonly NamedPipeClientStream _pipeClient;
        private readonly Action<string> _stdOutLog;
        private readonly AssemblyLoadEventHandler _assemblyLoad;
        private readonly ConcurrentDictionary<Guid, List<UpdateDelta>> _deltas = new();
        private readonly ConcurrentDictionary<Assembly, Assembly> _appliedAssemblies = new();
        private readonly ApplyUpdateDelegate? _applyUpdate;
        private readonly string? _capabilities;
        private readonly MetadataUpdateHandlerInvoker _metadataUpdateHandlerInvoker;

        public HotReloadAgent(NamedPipeClientStream pipeClient, Action<string> stdOutLog)
        {
            _assemblyLoad = OnAssemblyLoad;
            _pipeClient = pipeClient;
            _stdOutLog = stdOutLog;
            _metadataUpdateHandlerInvoker = new(_reporter);

            GetUpdaterMethods(out _applyUpdate, out _capabilities);
            AppDomain.CurrentDomain.AssemblyLoad += _assemblyLoad;
        }

        private void GetUpdaterMethods(out ApplyUpdateDelegate? applyUpdate, out string? capabilities)
        {
            applyUpdate = null;
            capabilities = null;

            var metadataUpdater = Type.GetType(MetadataUpdaterTypeName + ", System.Runtime.Loader", throwOnError: false);
            if (metadataUpdater == null)
            {
                _reporter.Report($"Type not found: {MetadataUpdaterTypeName}", AgentMessageSeverity.Error);
                return;
            }

            var applyUpdateMethod = metadataUpdater.GetMethod(ApplyUpdateMethodName, BindingFlags.Public | BindingFlags.Static, binder: null, [typeof(Assembly), typeof(ReadOnlySpan<byte>), typeof(ReadOnlySpan<byte>), typeof(ReadOnlySpan<byte>)], modifiers: null);
            if (applyUpdateMethod == null)
            {
                _reporter.Report($"{MetadataUpdaterTypeName}.{ApplyUpdateMethodName} not found.", AgentMessageSeverity.Error);
                return;
            }

            applyUpdate = (ApplyUpdateDelegate)applyUpdateMethod.CreateDelegate(typeof(ApplyUpdateDelegate));

            var getCapabilities = metadataUpdater.GetMethod(GetCapabilitiesMethodName, BindingFlags.NonPublic | BindingFlags.Static, binder: null, Type.EmptyTypes, modifiers: null);
            if (getCapabilities == null)
            {
                _reporter.Report($"{MetadataUpdaterTypeName}.{GetCapabilitiesMethodName} not found.", AgentMessageSeverity.Error);
                return;
            }

            try
            {
                capabilities = getCapabilities.Invoke(obj: null, parameters: null) as string;
            }
            catch (Exception e)
            {
                _reporter.Report($"Error retrieving capabilities: {e.Message}", AgentMessageSeverity.Error);
            }
        }

        public async Task ReceiveDeltasAsync()
        {
            _reporter.Report("Writing capabilities: " + Capabilities, AgentMessageSeverity.Verbose);

            var initPayload = new ClientInitializationPayload(Capabilities);
            initPayload.Write(_pipeClient);

            while (_pipeClient.IsConnected)
            {
                var update = await UpdatePayload.ReadAsync(_pipeClient, CancellationToken.None);

                _stdOutLog($"ResponseLoggingLevel = {update.ResponseLoggingLevel}");

                _reporter.Report("Attempting to apply deltas.", AgentMessageSeverity.Verbose);

                ApplyDeltas(update.Deltas);

                _pipeClient.WriteByte(UpdatePayload.ApplySuccessValue);

                UpdatePayload.WriteLog(_pipeClient, _reporter.GetAndClearLogEntries(update.ResponseLoggingLevel));
            }
        }

        public string Capabilities => _capabilities ?? string.Empty;

        private void OnAssemblyLoad(object? _, AssemblyLoadEventArgs eventArgs)
        {
            _metadataUpdateHandlerInvoker.Clear();

            var loadedAssembly = eventArgs.LoadedAssembly;
            var moduleId = TryGetModuleId(loadedAssembly);
            if (moduleId is null)
            {
                return;
            }

            if (_deltas.TryGetValue(moduleId.Value, out var updateDeltas) && _appliedAssemblies.TryAdd(loadedAssembly, loadedAssembly))
            {
                // A delta for this specific Module exists and we haven't called ApplyUpdate on this instance of Assembly as yet.
                ApplyDeltas(loadedAssembly, updateDeltas);
            }
        }

        public void ApplyDeltas(IReadOnlyList<UpdateDelta> deltas)
        {
            Debug.Assert(Capabilities.Length > 0);
            Debug.Assert(_applyUpdate != null);

            for (var i = 0; i < deltas.Count; i++)
            {
                var item = deltas[i];
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (TryGetModuleId(assembly) is Guid moduleId && moduleId == item.ModuleId)
                    {
                        _applyUpdate(assembly, item.MetadataDelta, item.ILDelta, pdbDelta: []);
                    }
                }

                // Additionally stash the deltas away so it may be applied to assemblies loaded later.
                var cachedDeltas = _deltas.GetOrAdd(item.ModuleId, static _ => new());
                cachedDeltas.Add(item);
            }

            _metadataUpdateHandlerInvoker.Invoke(GetMetadataUpdateTypes(deltas));
        }

        private Type[] GetMetadataUpdateTypes(IReadOnlyList<UpdateDelta> deltas)
        {
            List<Type>? types = null;

            foreach (var delta in deltas)
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => TryGetModuleId(assembly) is Guid moduleId && moduleId == delta.ModuleId);
                if (assembly is null)
                {
                    continue;
                }

                foreach (var updatedType in delta.UpdatedTypes)
                {
                    // Must be a TypeDef.
                    Debug.Assert(updatedType >> 24 == 0x02);

                    // The type has to be in the manifest module since Hot Reload does not support multi-module assemblies:
                    try
                    {
                        var type = assembly.ManifestModule.ResolveType(updatedType);
                        types ??= new();
                        types.Add(type);
                    }
                    catch (Exception e)
                    {
                        _reporter.Report($"Failed to load type 0x{updatedType:X8}: {e.Message}", AgentMessageSeverity.Warning);
                    }
                }
            }

            return types?.ToArray() ?? Type.EmptyTypes;
        }

        public void ApplyDeltas(Assembly assembly, IReadOnlyList<UpdateDelta> deltas)
        {
            Debug.Assert(_applyUpdate != null);

            try
            {
                foreach (var item in deltas)
                {
                    _applyUpdate(assembly, item.MetadataDelta, item.ILDelta, ReadOnlySpan<byte>.Empty);
                }

                _reporter.Report("Deltas applied.", AgentMessageSeverity.Verbose);
            }
            catch (Exception ex)
            {
                _reporter.Report(ex.ToString(), AgentMessageSeverity.Warning);
            }
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.AssemblyLoad -= _assemblyLoad;
        }

        private static Guid? TryGetModuleId(Assembly loadedAssembly)
        {
            try
            {
                return loadedAssembly.Modules.FirstOrDefault()?.ModuleVersionId;
            }
            catch
            {
                // Assembly.Modules might throw. See https://github.com/dotnet/aspnetcore/issues/33152
            }

            return default;
        }
    }
}
