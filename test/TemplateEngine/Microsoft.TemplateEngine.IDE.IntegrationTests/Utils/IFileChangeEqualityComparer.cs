using Microsoft.TemplateEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests.Utils
{
    internal class IFileChangeComparer : IEqualityComparer<IFileChange>, IComparer<IFileChange>
    {
        public int Compare(IFileChange x, IFileChange y)
        {
            if (Equals(x,y))
            {
                return 0;
            }

            if (x.ChangeKind > y.ChangeKind)
            {
                return 1;
            }
            if (x.ChangeKind < y.ChangeKind)
            {
                return -1;
            }

            int compareResult = string.Compare(x.TargetRelativePath, y.TargetRelativePath, StringComparison.OrdinalIgnoreCase);
            if (compareResult != 0)
            {
                return compareResult;
            }

            return string.Compare((x as IFileChange2)?.SourceRelativePath, (y as IFileChange2)?.SourceRelativePath, StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(IFileChange x, IFileChange y)
        {
            return string.Equals(x.TargetRelativePath, y.TargetRelativePath, StringComparison.OrdinalIgnoreCase)
                    && x.ChangeKind == y.ChangeKind
                    && (x is IFileChange2 x2 && y is IFileChange2 y2 && string.Equals(x2.SourceRelativePath, y2.SourceRelativePath, StringComparison.OrdinalIgnoreCase));
        }

        public int GetHashCode([DisallowNull] IFileChange obj)
        {
            return new { obj.TargetRelativePath, obj.ChangeKind, a = obj is IFileChange2 obj2 ? obj2.SourceRelativePath : null }.GetHashCode();
        }
    }
}
