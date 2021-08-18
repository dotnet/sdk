// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.Helpers
{
    internal static class EnumHelpers
    {
        internal static bool TryConvertToUInt64(object value, SpecialType specialType, out ulong convertedValue)
        {
            bool success = false;
            convertedValue = 0;
            if (value != null)
            {
                switch (specialType)
                {
                    case SpecialType.System_Int16:
                        convertedValue = unchecked((ulong)(short)value);
                        success = true;
                        break;
                    case SpecialType.System_Int32:
                        convertedValue = unchecked((ulong)(int)value);
                        success = true;
                        break;
                    case SpecialType.System_Int64:
                        convertedValue = unchecked((ulong)(long)value);
                        success = true;
                        break;
                    case SpecialType.System_UInt16:
                        convertedValue = (ushort)value;
                        success = true;
                        break;
                    case SpecialType.System_UInt32:
                        convertedValue = (uint)value;
                        success = true;
                        break;
                    case SpecialType.System_UInt64:
                        convertedValue = (ulong)value;
                        success = true;
                        break;
                    case SpecialType.System_Byte:
                        convertedValue = (byte)value;
                        success = true;
                        break;
                    case SpecialType.System_SByte:
                        convertedValue = unchecked((ulong)(sbyte)value);
                        success = true;
                        break;
                    case SpecialType.System_Char:
                        convertedValue = (char)value;
                        success = true;
                        break;
                }
            }

            return success;
        }

        internal static bool TryGetEnumMemberValues(INamedTypeSymbol enumType, out IList<ulong> values)
        {
            Debug.Assert(enumType.TypeKind == TypeKind.Enum);

            values = new List<ulong>();
            foreach (var member in enumType.GetMembers())
            {
                if (member is IFieldSymbol field && !field.IsImplicitlyDeclared)
                {
                    if (!field.HasConstantValue)
                    {
                        return false;
                    }

                    if (!TryConvertToUInt64(field.ConstantValue, enumType.EnumUnderlyingType.SpecialType, out ulong convertedValue))
                    {
                        return false;
                    }

                    values.Add(convertedValue);
                }
            }

            return true;
        }
    }
}
