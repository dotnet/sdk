using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions.Engine
{
    public interface IKeysChangedEventArgs
    {
    }

    public interface IValueReadEventArgs
    {
        string Key { get; set; }

        object Value { get; set; }
    }

    public delegate void KeysChangedEventHander(object sender, IKeysChangedEventArgs args);

    public delegate void ValueReadEventHander(object sender, IValueReadEventArgs args);

    public interface IVariableCollection : IDictionary<string, object>
    {
        IVariableCollection Parent { get; set; }

        event KeysChangedEventHander KeysChanged;

        event ValueReadEventHander ValueRead;
    }

    public interface IEngineConfig
    {
        IReadOnlyList<string> LineEndings { get; }

        string VariableFormatString { get; }

        IVariableCollection Variables { get; }

        IReadOnlyList<string> Whitespaces { get; }

        IDictionary<string, bool> Flags { get; }
    }
}