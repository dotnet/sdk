// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.NetCore.Analyzers.Security.Helpers
{
    internal class InsecureObjectGraphResult
    {
        public InsecureObjectGraphResult(
            ISymbol? insecureSymbol,
            AttributeData? insecureAttribute,
            TypedConstant? insecureAttributeTypedConstant,
            ITypeSymbol insecureType)
        {
            if ((insecureSymbol == null && insecureAttribute == null)
                || (insecureSymbol != null && insecureAttribute != null))
            {
                throw new ArgumentException("Either insecureSymbol or insecureAttribute should be non-null");
            }

            if ((insecureAttribute == null && insecureAttributeTypedConstant != null)
                || (insecureAttribute != null && insecureAttributeTypedConstant == null))
            {
                throw new ArgumentException(
                    "Both insecureAttribute and insecureAttributeTypedConstant should be null or non-null");
            }

            InsecureSymbol = insecureSymbol;
            InsecureAttribute = insecureAttribute;
            InsecureAttributeTypedConstant = insecureAttributeTypedConstant;
            InsecureType = insecureType ?? throw new ArgumentNullException(nameof(insecureType));
        }

        /// <summary>
        /// The class / struct or its member field / property referencing an insecure type.
        /// </summary>
        public ISymbol? InsecureSymbol { get; }

        /// <summary>
        /// Attribute referencing an insecure type.
        /// </summary>
        public AttributeData? InsecureAttribute { get; }

        /// <summary>
        /// Typed constant in the attribute referencing an insecure type.
        /// </summary>
        public TypedConstant? InsecureAttributeTypedConstant { get; }

        /// <summary>
        /// The insecure type being referenced.
        /// </summary>
        public ITypeSymbol InsecureType { get; }

        /// <summary>
        /// Gets the <see cref="Location"/> of <see cref="InsecureSymbol"/> or <see cref="InsecureAttribute"/>.
        /// </summary>
        /// <returns><see cref="Location"/> of <see cref="InsecureSymbol"/> or <see cref="InsecureAttribute"/>.</returns>
        public Location GetLocation()
        {
            if (this.InsecureSymbol != null)
            {
                return this.InsecureSymbol.DeclaringSyntaxReferences.First().GetSyntax().GetLocation();
            }
            else if (this.InsecureAttribute != null)
            {
                return this.InsecureAttribute.ApplicationSyntaxReference.GetSyntax().GetLocation();
            }
            else
            {
                throw new NotImplementedException("Unhandled case");
            }
        }

        /// <summary>
        /// Gets the display string of <see cref="InsecureSymbol"/> or <see cref="InsecureAttribute"/>.
        /// </summary>
        /// <returns>Display string of <see cref="InsecureSymbol"/> or <see cref="InsecureAttribute"/>.</returns>
        public string GetDisplayString(Func<TypedConstant, string> typedConstantToString)
        {
            if (this.InsecureSymbol != null)
            {
                return this.InsecureSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            }
            else if (this.InsecureAttributeTypedConstant != null)
            {
                TypedConstant t = (TypedConstant)this.InsecureAttributeTypedConstant!;
                return typedConstantToString(t);
            }
            else
            {
                throw new NotImplementedException("Unhandled case");
            }
        }
    }
}
