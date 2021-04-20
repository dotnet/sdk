// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Expressions
{
    public interface ITypeConverter
    {
        ITypeConverter Register<T>(TypeConverterDelegate<T> converter);

        bool TryConvert<T>(object source, out T result);

        bool TryCoreConvert<T>(object source, out T result);
    }
}
