// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ExclusionsLibrary;

internal class ExclusionsStorage
{
    /// <summary>
    /// Storage for exclusions.
    /// Key: file path.
    /// Value: list of exclusions for the file.
    /// </summary>
    private Dictionary<string, List<Exclusion>> _storage = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ExclusionsStorage"/> class.
    /// </summary>
    public ExclusionsStorage() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExclusionsStorage"/> class.
    /// <param name="other">The storage to copy.</param>
    /// </summary>
    public ExclusionsStorage(ExclusionsStorage other)
    {
        foreach (string file in other._storage.Keys)
        {
            _storage[file] = other._storage[file].Select(e => new Exclusion(e)).ToList();
        }
    }

    /// <summary>
    /// Gets all files in the storage.
    /// </summary>
    public IEnumerable<string> GetFiles() => _storage.Keys;

    /// <summary>
    /// Adds an exclusion to the storage.
    /// <param name="file">The file to add the exclusion to.</param>
    /// <param name="exclusion">The exclusion to add.</param>
    /// </summary>
    public void Add(string file, Exclusion exclusion)
    {
        if (!_storage.ContainsKey(file))
        {
            _storage[file] = new List<Exclusion>();
        }
        _storage[file].Add(exclusion);
    }

    /// <summary>
    /// Removes a suffix from an exclusion in the storage.
    /// If there are no suffixes left, the exclusion will be removed.
    /// <param name="file">The file for the exclusion.</param>
    /// <param name="pattern">The pattern to look for.</param>
    /// <param name="suffix">The suffix to remove.</param>
    /// </summary>
    public void Remove(string file, string pattern, string? suffix)
    {
        if (_storage.ContainsKey(file))
        {
            Exclusion? exclusion = _storage[file].FirstOrDefault(e => e.Pattern == pattern);
            if (exclusion is not null)
            {
                _storage[file].Remove(exclusion);
                exclusion.Suffixes.Remove(suffix);
                if (exclusion.Suffixes.Count > 0)
                {
                    _storage[file].Add(exclusion);
                }
            }
        }
    }

    /// <summary>
    /// Checks if the storage contains a file.
    /// <param name="file">The file to check for.</param>
    /// </summary>
    public bool ContainsFile(string file) => _storage.ContainsKey(file);

    /// <summary>
    /// Gets an exclusion from the storage.
    /// <param name="file">The file to get the exclusion from.</param>
    /// <param name="pattern">The pattern to look for.</param>
    /// </summary>
    public Exclusion? GetExclusion(string file, string pattern) =>
        _storage.ContainsKey(file) ? _storage[file].FirstOrDefault(e => e.Pattern == pattern) : null;

    /// <summary>
    /// Checks if the storage has a match for a file path and suffix input.
    /// <param name="filePath">The file path to check.</param>
    /// <param name="suffix">The suffix to narrow down the scope of exclusions.</param>
    /// <param name="match">The match if found.</param>
    /// </summary>
    public bool HasMatch(string filePath, string? suffix, out (string file, string pattern) match)
    {
        foreach (string file in _storage.Keys)
        {
            foreach (Exclusion exclusion in _storage[file])
            {
                if (exclusion.HasMatch(filePath, suffix))
                {
                    match = (file, exclusion.Pattern);
                    return true;
                }
            }
        }

        match = (string.Empty, string.Empty);
        return false;
    }
}
