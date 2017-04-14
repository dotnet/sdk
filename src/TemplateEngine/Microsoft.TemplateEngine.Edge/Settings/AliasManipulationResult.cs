using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class AliasManipulationResult
    {
        public AliasManipulationResult(AliasManipulationStatus status)
            :this(status, null, null)
        {
        }

        public AliasManipulationResult(AliasManipulationStatus status, string aliasName, string aliasValue)
        {
            Status = status;
            AliasName = aliasName;
            AliasValue = aliasValue;
        }

        public AliasManipulationStatus Status { get; }
        public string AliasName { get; }
        public string AliasValue { get; }
    }
}
