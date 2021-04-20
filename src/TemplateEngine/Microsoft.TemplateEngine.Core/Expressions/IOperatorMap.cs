// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Expressions
{
    public interface IOperatorMap<TOperator, TToken>
        where TToken : struct
    {
        ISet<TToken> BadSyntaxTokens { get; }

        TToken CloseGroupToken { get; }

        TOperator Identity { get; }

        ISet<TToken> LiteralSequenceBoundsMarkers { get; }

        TToken LiteralToken { get; }

        ISet<TToken> NoOpTokens { get; }

        TToken OpenGroupToken { get; }

        IReadOnlyDictionary<TOperator, Func<IEvaluable, IEvaluable>> OperatorScopeLookupFactory { get; }

        ISet<TToken> Terminators { get; }

        IReadOnlyDictionary<TToken, TOperator> TokensToOperatorsMap { get; }

        bool TryConvert<T>(object source, out T result);

        string Decode(string value);

        string Encode(string value);
    }
}
