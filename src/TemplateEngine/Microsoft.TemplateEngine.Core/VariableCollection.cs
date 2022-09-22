// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core
{
    public class VariableCollection : IVariableCollection
    {
        private static readonly IEnumerable<string> NoKeys = Array.Empty<string>();
        private readonly IDictionary<string, object> _values;
        private IVariableCollection? _parent;

        public VariableCollection()
            : this(null)
        {
        }

        public VariableCollection(VariableCollection? parent)
            : this(parent, new Dictionary<string, object>())
        {
        }

        public VariableCollection(IVariableCollection? parent, IDictionary<string, object> values)
        {
            if (values != null)
            {
                if (values.Values.Any(o => o is null))
                {
                    throw new ArgumentException($"The {nameof(values)} should not contain null.", nameof(values));
                }
            }

            _parent = parent;
            _values = values ?? new Dictionary<string, object>();

            if (_parent != null)
            {
                _parent.KeysChanged += RelayKeysChanged;
            }
        }

        public event KeysChangedEventHander? KeysChanged;

        public event ValueReadEventHander? ValueRead;

        public int Count => Keys.Count;

        public bool IsReadOnly => false;

        public ICollection<string> Keys => _values.Keys.Union(_parent?.Keys ?? NoKeys).ToList();

        public IVariableCollection? Parent
        {
            get => _parent;

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
                    ValueReadEventArgs args = new(key, result);
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
                _values[key] = value ?? throw new ArgumentNullException(nameof(value));
                if (changing)
                {
                    OnKeysChanged();
                }
            }
        }

        public static VariableCollection Root() => Root(new Dictionary<string, object>());

        public static VariableCollection Root(IDictionary<string, object> values) => new(null, values);

        public static IVariableCollection SetupVariables(IParameterSetData parameters, IVariableConfig variableConfig)
        {
            IVariableCollection variables = Root();

            Dictionary<string, VariableCollection> collections = new Dictionary<string, VariableCollection>();

            foreach (KeyValuePair<string, string> source in variableConfig.Sources)
            {
                VariableCollection? variablesForSource = null;
                string format = source.Value;

                switch (source.Key)
                {
                    //may be extended for other categories in future if needed.
                    case "user":
                        variablesForSource = VariableCollectionFromParameters(parameters, format);

                        if (variableConfig.FallbackFormat != null)
                        {
                            VariableCollection variablesFallback = VariableCollectionFromParameters(parameters, variableConfig.FallbackFormat);
                            variablesFallback.Parent = variablesForSource;
                            variablesForSource = variablesFallback;
                        }
                        break;
                }
                if (variablesForSource != null)
                {
                    collections[source.Key] = variablesForSource;
                }
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

        public void Add(KeyValuePair<string, object> item)
        {
            if (_parent?.ContainsKey(item.Key) ?? false)
            {
                throw new InvalidOperationException("Key already added");
            }

            if (item.Value is null)
            {
                throw new ArgumentException($"The value of key-value pair {nameof(item)} should not be null.", nameof(item));
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

            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
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

        private static VariableCollection VariableCollectionFromParameters(IParameterSetData parameters, string format)
        {
            VariableCollection vc = new VariableCollection();
            foreach (ITemplateParameter param in parameters.ParametersDefinition)
            {
                string key = string.Format(format ?? "{0}", param.Name);

                if (parameters.TryGetValue(param, out ParameterData value) &&
                    value.IsEnabled && value.Value != null)
                {
                    vc[key] = value.Value;
                }
            }

            return vc;
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
