using System;

namespace Mutant.Chicken
{
    public class KeysChangedEventArgs : EventArgs
    {
        private static KeysChangedEventArgs _default;

        public static KeysChangedEventArgs Default => _default ?? (_default = new KeysChangedEventArgs());
    }
}