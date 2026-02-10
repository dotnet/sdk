// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.System.Variant;
using static Windows.Win32.System.Com.ADVANCED_FEATURE_FLAGS;
using static Windows.Win32.System.Variant.VARENUM;

namespace Windows.Win32.System.Com;

internal unsafe partial struct SAFEARRAY
{
    /// <summary>
    ///  Gets the <see cref="SAFEARRAYBOUND"/> of the <paramref name="dimension"/>.
    /// </summary>
    public SAFEARRAYBOUND GetBounds(int dimension = 0)
    {
        fixed (void* b = &rgsabound)
        {
            return new ReadOnlySpan<SAFEARRAYBOUND>(b, cDims)[dimension];
        }
    }

    /// <summary>
    ///  Gets the <see cref="VARENUM"/> type of the elements in the <see cref="SAFEARRAY"/>.
    /// </summary>
    public VARENUM VarType
    {
        get
        {
            // Match CLR behavior.
            ADVANCED_FEATURE_FLAGS hardwiredType = fFeatures & (FADF_BSTR | FADF_UNKNOWN | FADF_DISPATCH | FADF_VARIANT);
            if (hardwiredType == FADF_BSTR && cbElements == sizeof(char*))
            {
                return VT_BSTR;
            }
            else if (hardwiredType == FADF_UNKNOWN && cbElements == sizeof(IntPtr))
            {
                return VT_UNKNOWN;
            }
            else if (hardwiredType == FADF_DISPATCH && cbElements == sizeof(IntPtr))
            {
                return VT_DISPATCH;
            }
            else if (hardwiredType == FADF_VARIANT && cbElements == sizeof(VARIANT))
            {
                return VT_VARIANT;
            }

            // Call native API.
            VARENUM vt = VT_EMPTY;
            fixed (SAFEARRAY* pThis = &this)
            {
                PInvoke.SafeArrayGetVartype(pThis, &vt).ThrowOnFailure();
                return vt;
            }
        }
    }
}
