// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions.Parameters;

/// <summary>
/// The set of descriptors of template parameters extracted from the template.
/// </summary>
public interface IParameterDefinitionSet : IReadOnlyList<ITemplateParameter>
{
    //
    // Following methods are taken from the IReadOnlyDictionary<TKey, TValue>
    // That interface is not explicitly inherited as it would lead to ambiguous implementation of GetEnumerator()
    //  and hence not clean way of using in foreach. As that's more often scenario, we decided to inherit IEnumerable<ITemplateParameter>
    //

    /// <summary>Gets an enumerable collection that contains the keys in the read-only dictionary.</summary>
    /// <returns>An enumerable collection that contains the keys in the read-only dictionary.</returns>
    IEnumerable<string> Keys { get; }

    /// <summary>Gets an enumerable collection that contains the values in the read-only dictionary.</summary>
    /// <returns>An enumerable collection that contains the values in the read-only dictionary.</returns>
    IEnumerable<ITemplateParameter> Values { get; }

    /// <summary>Gets the element that has the specified key in the read-only dictionary.</summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>The element that has the specified key in the read-only dictionary.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key">key</paramref> is null.</exception>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The property is retrieved and <paramref name="key">key</paramref> is not found.</exception>
    ITemplateParameter this[string key] { get; }

    /// <summary>Determines whether the read-only dictionary contains an element that has the specified key.</summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>true if the read-only dictionary contains an element that has the specified key; otherwise, false.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key">key</paramref> is null.</exception>
    bool ContainsKey(string key);

    /// <summary>Gets the value that is associated with the specified key.</summary>
    /// <param name="key">The key to locate.</param>
    /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns>true if the object that implements the <see cref="T:System.Collections.Generic.IReadOnlyDictionary`2"></see> interface contains an element that has the specified key; otherwise, false.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key">key</paramref> is null.</exception>
    bool TryGetValue(string key, out ITemplateParameter value);

    // End of definitions pulled from IReadOnlyDictionary<TKey, TValue>

    /// <summary>
    /// Casts the Parameter set to IReadOnlyDictionary.
    /// </summary>
    /// <returns></returns>
    IReadOnlyDictionary<string, ITemplateParameter> AsReadonlyDictionary();
}
