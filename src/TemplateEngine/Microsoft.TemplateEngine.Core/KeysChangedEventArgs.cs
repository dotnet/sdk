using System;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core
{
    public class KeysChangedEventArgs : EventArgs, IKeysChangedEventArgs
    {
        private static KeysChangedEventArgs _default;

        public static KeysChangedEventArgs Default => _default ?? (_default = new KeysChangedEventArgs());
    }
}
