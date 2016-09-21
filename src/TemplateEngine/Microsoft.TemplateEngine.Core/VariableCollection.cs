using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Core
{
    public class VariableCollection : IVariableCollection
    {
        private static readonly IEnumerable<string> NoKeys = new string[0];
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
                object result;
                if (_values.TryGetValue(key, out result))
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

        public static VariableCollection Environment() => Environment(null, true, true, "{0}");

        public static VariableCollection Environment(string formatString) => Environment(null, true, true, formatString);

        public static VariableCollection Environment(VariableCollection parent) => Environment(parent, true, true, "{0}");

        public static VariableCollection Environment(VariableCollection parent, string formatString) => Environment(parent, true, true, formatString);

        public static VariableCollection Environment(bool changeCase, bool upperCase) => Environment(null, changeCase, upperCase, "{0}");

        public static VariableCollection Environment(bool changeCase, bool upperCase, string formatString) => Environment(null, changeCase, upperCase, formatString);

        public static VariableCollection Environment(VariableCollection parent, bool changeCase, bool upperCase, string formatString)
        {
            VariableCollection vc = new VariableCollection(parent);
            IDictionary variables = System.Environment.GetEnvironmentVariables();

            foreach (string key in variables.Keys.OfType<string>())
            {
                string name = string.Format(formatString, !changeCase ? key : upperCase ? key.ToUpperInvariant() : key.ToLowerInvariant());
                vc[name] = variables[key];
            }

            return vc;
        }

        public static VariableCollection Root() => Root(null);

        public static VariableCollection Root(IDictionary<string, object> values) => new VariableCollection(null, values);

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


        public static IVariableCollection SetupVariables(IParameterSet parameters, IVariableConfig variableConfig)
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
                        variablesForSource = VariableCollection.Environment(format);

                        if (variableConfig.FallbackFormat != null)
                        {
                            variablesForSource = VariableCollection.Environment(variablesForSource, variableConfig.FallbackFormat);
                        }
                        break;
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

        public static VariableCollection VariableCollectionFromParameters(IParameterSet parameters, string format)
        {
            VariableCollection vc = new VariableCollection();
            foreach (ITemplateParameter param in parameters.ParameterDefinitions)
            {
                object value;
                string key = string.Format(format ?? "{0}", param.Name);

                if (!parameters.ResolvedValues.TryGetValue(param, out value))
                {
                    throw new TemplateParamException("Parameter value was not specified", param.Name, null, param.DataType);
                }
                else if (value == null)
                {
                    throw new TemplateParamException("Parameter value is null", param.Name, null, param.DataType);
                }
                else
                {
                    vc[key] = value;
                }
            }

            return vc;
        }
    }
}