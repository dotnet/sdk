// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Class that represents a mapping in between two objects of type <see cref="T"/>.
    /// </summary>
    public class ElementMapper<T>
    {
        private IReadOnlyList<IEnumerable<CompatDifference>> _differences;

        /// <summary>
        /// Property representing the Left hand side of the mapping.
        /// </summary>
        public T Left { get; private set; }

        /// <summary>
        /// Property representing the Right hand side of the mapping.
        /// </summary>
        public T[] Right { get; private set; }

        /// <summary>
        /// The <see cref="ComparingSettings"/> used to diff <see cref="Left"/> and <see cref="Right"/>.
        /// </summary>
        public ComparingSettings Settings { get; }

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        /// <param name="leftSetSize">The number of elements in the left set to compare.</param>
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public ElementMapper(ComparingSettings settings, int rightSetSize)
        {
            if (rightSetSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(rightSetSize), "Should be greater than 0");

            Settings = settings;
            Right = new T[rightSetSize];
        }

        public virtual void AddElement(T element, ElementSide side) => AddElement(element, side, 0);

        /// <summary>
        /// Adds an element to the mapping given the index, 0 (Left) or 1 (Right).
        /// </summary>
        /// <param name="element">The element to add to the mapping.</param>
        /// <param name="side">Value representing the side of the mapping.</param>
        /// <param name="setIndex">Value representing the index on the set of elements corresponding to the compared side.</param>
        public virtual void AddElement(T element, ElementSide side, int setIndex)
        {
            if (side == ElementSide.Left)
            {
                Left = element;
            }
            else
            {
                if ((uint)setIndex >= Right.Length)
                    throw new ArgumentOutOfRangeException(nameof(setIndex), "the index should be within the right set size");

                Right[setIndex] = element;
            }
        }

        /// <summary>
        /// Runs the rules found by the rule driver on the element mapper and returns a list of differences.
        /// </summary>
        /// <returns>The list of <see cref="CompatDifference"/>.</returns>
        public IReadOnlyList<IEnumerable<CompatDifference>> GetDifferences()
        {
            return _differences ??= Settings.RuleRunnerFactory.GetRuleRunner().Run(this);
        }
    }
}
