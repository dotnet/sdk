// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core
{
    public class VariableCollection : IVariableCollection
    {
        private static readonly IEnumerable<string> NoKeys = Array.Empty<string>();
        private readonly IDictionary<string, object> _values;
        private IVariableCollection _parent;

        public VariableCollection()
            : this(null)
        {
        }

        public VariableCollection(VariableCollection parent)
            : this(parent, new Dictionary<string, object>())
        {
        }

        public VariableCollection(IVariableCollection parent, IDictionary<string, object> values)
        {
            _parent = parent;
            _values = values ?? new Dictionary<string, object>();

            if (_parent != null)
            {
                _parent.KeysChanged += RelayKeysChanged;
            }
        }

        public event KeysChangedEventHander KeysChanged;

        public event ValueReadEventHander ValueRead;

        public int Count => Keys.Count;

        public bool IsReadOnly => false;

        public ICollection<string> Keys => _values.Keys.Union(_parent?.Keys ?? NoKeys).ToList();

        public IVariableCollection Parent
        {
            get { return _parent; }

            set
            {
                _parent = value;
                OnKeysChanged();
            }
        }

        public ICollection<object> Values => Keys.Select(x => this[x]).ToList();

        public object this[string key]
        {
            get
            {
                if (_values.TryGetValue(key, out object result))
                {
                    ValueReadEventArgs args = new ValueReadEventArgs(key, result);
                    OnValueRead(args);
                    return result;
                }

                if (_parent?.TryGetValue(key, out result) ?? false)
                {
                    return result;
                }

                throw new KeyNotFoundException($"No entry was found for key: {key}");
            }

            set
            {
                bool changing = !_values.ContainsKey(key);
                _values[key] = value;
                if (changing)
                {
                    OnKeysChanged();
                }
            }
        }

        public static VariableCollection Environment(IEngineEnvironmentSettings environmentSettings) => Environment(environmentSettings, null, true, true, "{0}");

        public static VariableCollection Environment(IEngineEnvironmentSettings environmentSettings, string formatString) => Environment(environmentSettings, null, true, true, formatString);

        public static VariableCollection Environment(IEngineEnvironmentSettings environmentSettings, VariableCollection parent) => Environment(environmentSettings, parent, true, true, "{0}");

        public static VariableCollection Environment(IEngineEnvironmentSettings environmentSettings, VariableCollection parent, string formatString) => Environment(environmentSettings, parent, true, true, formatString);

        public static VariableCollection Environment(IEngineEnvironmentSettings environmentSettings, bool changeCase, bool upperCase) => Environment(environmentSettings, null, changeCase, upperCase, "{0}");

        public static VariableCollection Environment(IEngineEnvironmentSettings environmentSettings, bool changeCase, bool upperCase, string formatString) => Environment(environmentSettings, null, changeCase, upperCase, formatString);

        public static VariableCollection Environment(IEngineEnvironmentSettings environmentSettings, VariableCollection parent, bool changeCase, bool upperCase, string formatString)
        {
            VariableCollection vc = new VariableCollection(parent);
            IReadOnlyDictionary<string, string> variables = environmentSettings.Environment.GetEnvironmentVariables();

            foreach (KeyValuePair<string, string> entry in variables)
            {
                string name = string.Format(formatString, !changeCase ? entry.Key : upperCase ? entry.Key.ToUpperInvariant() : entry.Key.ToLowerInvariant());
                vc[name] = entry.Value;
            }

            return vc;
        }

        public static VariableCollection Root() => Root(null);

        public static VariableCollection Root(IDictionary<string, object> values) => new VariableCollection(null, values);

        public static IVariableCollection SetupVariables(IEngineEnvironmentSettings environmentSettings, IParameterSet parameters, IVariableConfig variableConfig)
        {
            IVariableCollection variables = Root();

            Dictionary<string, VariableCollection> collections = new Dictionary<string, VariableCollection>();

            foreach (KeyValuePair<string, string> source in variableConfig.Sources)
            {
                VariableCollection variablesForSource = null;
                string format = source.Value;

                switch (source.Key)
                {
                    case "environment":
                        variablesForSource = Environment(environmentSettings, format);

                        if (variableConfig.FallbackFormat != null)
                        {
                            variablesForSource = Environment(environmentSettings, variablesForSource, variableConfig.FallbackFormat);
                        }
                        break;
                    case "user":
                        variablesForSource = VariableCollectionFromParameters(environmentSettings, parameters, format);

                        if (variableConfig.FallbackFormat != null)
                        {
                            VariableCollection variablesFallback = VariableCollectionFromParameters(environmentSettings, parameters, variableConfig.FallbackFormat);
                            variablesFallback.Parent = variablesForSource;
                            variablesForSource = variablesFallback;
                        }
                        break;
                }

                collections[source.Key] = variablesForSource;
            }

            foreach (string order in variableConfig.Order)
            {
                IVariableCollection current = collections[order.ToString()];

                IVariableCollection tmp = current;
                while (tmp.Parent != null)
                {
                    tmp = tmp.Parent;
                }

                tmp.Parent = variables;
                variables = current;
            }

            return variables;
        }

        public static VariableCollection VariableCollectionFromParameters(IEngineEnvironmentSettings environmentSettings, IParameterSet parameters, string format)
        {
            VariableCollection vc = new VariableCollection();
            foreach (ITemplateParameter param in parameters.ParameterDefinitions)
            {
                string key = string.Format(format ?? "{0}", param.Name);

                if (!parameters.ResolvedValues.TryGetValue(param, out object value))
                {
                    if (param.Priority != TemplateParameterPriority.Optional && param.Priority != TemplateParameterPriority.Suggested)
                    {
                        while (environmentSettings.Host.OnParameterError(param, null, "ParameterValueNotSpecified", out string val))
                        {
                        }

                        parameters.ResolvedValues[param] = value;
                    }
                }
                else if (value == null)
                {
                    if (param.Priority != TemplateParameterPriority.Optional && param.Priority != TemplateParameterPriority.Suggested)
                    {
                        while (environmentSettings.Host.OnParameterError(param, null, "ParameterValueNull", out string val))
                        {
                        }

                        parameters.ResolvedValues[param] = value;
                    }
                }
                else
                {
                    vc[key] = value;
                }
            }

            return vc;
        }

        public void Add(KeyValuePair<string, object> item)
        {
            if (_parent?.ContainsKey(item.Key) ?? false)
            {
                throw new InvalidOperationException("Key already added");
            }

            _values.Add(item);
            OnKeysChanged();
        }

        public void Add(string key, object value)
        {
            if (_parent?.ContainsKey(key) ?? false)
            {
                throw new InvalidOperationException("Key already added");
            }

            _values.Add(key, value);
            OnKeysChanged();
        }

        public void Clear()
        {
            if (_values.Count > 0)
            {
                _values.Clear();
                OnKeysChanged();
            }
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return _values.Contains(item) || (_parent?.Contains(item) ?? false);
        }

        public bool ContainsKey(string key) => _values.ContainsKey(key) || (_parent?.ContainsKey(key) ?? false);

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            int index = arrayIndex;
            foreach (string key in Keys)
            {
                if (index >= array.Length)
                {
                    break;
                }

                array[index++] = new KeyValuePair<string, object>(key, this[key]);
            }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => Keys.Select(x => new KeyValuePair<string, object>(x, this[x])).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_values).GetEnumerator();

        public bool Remove(KeyValuePair<string, object> item) => Remove(item.Key);

        public bool Remove(string key)
        {
            if (_values.Remove(key))
            {
                if (!(_parent?.ContainsKey(key) ?? false))
                {
                    OnKeysChanged();
                }

                return true;
            }

            return false;
        }

        public bool TryGetValue(string key, out object value)
        {
            if (_values.TryGetValue(key, out value))
            {
                OnValueRead(key, value);
                return true;
            }

            return _parent?.TryGetValue(key, out value) ?? false;
        }

        private void OnKeysChanged()
        {
            KeysChanged?.Invoke(this, KeysChangedEventArgs.Default);
        }

        private void OnValueRead(string key, object value)
        {
            OnValueRead(new ValueReadEventArgs(key, value));
        }

        private void OnValueRead(IValueReadEventArgs args)
        {
            ValueRead?.Invoke(this, args);
        }

        private void RelayKeysChanged(object sender, IKeysChangedEventArgs args)
        {
            OnKeysChanged();
        }
    }
}
