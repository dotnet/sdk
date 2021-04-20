// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class DefaultSafeNameValueFormModel : IValueForm
    {
        private readonly string _name;

        internal DefaultSafeNameValueFormModel()
            : this(null)
        {
        }

        internal DefaultSafeNameValueFormModel(string name)
        {
            _name = name;
        }

        internal const string FormName = "safe_name";

        public virtual string Identifier => FormName;

        public virtual string Name => _name ?? Identifier;

        public virtual IValueForm FromJObject(string name, JObject configuration)
        {
            return new DefaultSafeNameValueFormModel(name);
        }

        public virtual string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            string workingValue = Regex.Replace(value, @"(^\s+|\s+$)", "");
            workingValue = Regex.Replace(workingValue, @"(((?<=\.)|^)(?=\d)|\W)", "_");

            return workingValue;
        }
    }
}
