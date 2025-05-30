
using System;
using System.Collections.Immutable;
using System.Net.Http;
using System.Threading.Tasks;

namespace Dnvm;

internal static class ImmutableArrayExt
{
    public static async Task<ImmutableArray<U>> SelectAsArray<T, U>(this ImmutableArray<T> e, Func<T, Task<U>> f)
    {
        var builder = ImmutableArray.CreateBuilder<U>(e.Length);
        foreach (var item in e)
        {
            builder.Add(await f(item));
        }
        return builder.MoveToImmutable();
    }

    public static T? SingleOrNull<T>(this ImmutableArray<T> e, Func<T, bool> func)
        where T : class
    {
        T? result = null;
        foreach (var elem in e)
        {
            if (func(elem))
            {
                if (result is not null)
                {
                    return null;
                }
                result = elem;
            }
        }
        return result;
    }
}

internal static class ImmutableArrayExt2
{
    public static T? SingleOrNull<T>(this ImmutableArray<T> e, Func<T, bool> func)
        where T : struct
    {
        T? result = null;
        foreach (var elem in e)
        {
            if (func(elem))
            {
                if (result is not null)
                {
                    return null;
                }
                result = elem;
            }
        }
        return result;
    }
}