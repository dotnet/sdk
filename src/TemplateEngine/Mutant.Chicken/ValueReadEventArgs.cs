using System;

namespace Mutant.Chicken.Core
{
    public class ValueReadEventArgs : EventArgs
    {
        public ValueReadEventArgs(string key, object value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; set; }

        public object Value { get; set; }
    }
}