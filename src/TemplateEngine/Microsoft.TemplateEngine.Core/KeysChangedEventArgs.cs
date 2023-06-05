// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core
{
    public class KeysChangedEventArgs : EventArgs, IKeysChangedEventArgs
    {
        private static KeysChangedEventArgs? s_default;

        public static KeysChangedEventArgs Default => s_default ??= new KeysChangedEventArgs();
    }
}
