// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    public class HashCollisionProxy : DispatchProxy
    {
        private object _target;
        private Func<MethodInfo, object[], object> _intercept;

        public static T Create<T>(T target, Func<MethodInfo, object[], object> intercept = null)
            where T : class
        {
            var proxy = DispatchProxy.Create<T, HashCollisionProxy>();
            var state = (HashCollisionProxy)(object)proxy;
            state._target = target;
            state._intercept = intercept;
            return proxy;
        }

        public override int GetHashCode() => 0;

        public override bool Equals(object obj)
            => _target is ISymbol symbol
                ? obj is HashCollisionProxy other && other._target is ISymbol otherSymbol &&
                    SymbolEqualityComparer.Default.Equals(symbol, otherSymbol)
                : ReferenceEquals(this, obj);

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (_intercept != null)
            {
                return _intercept(targetMethod, args);
            }

            if (_target is ISymbol && targetMethod.Name == nameof(Equals))
            {
                return Equals(args[0]);
            }

            return targetMethod.Invoke(_target, args);
        }
    }
}
