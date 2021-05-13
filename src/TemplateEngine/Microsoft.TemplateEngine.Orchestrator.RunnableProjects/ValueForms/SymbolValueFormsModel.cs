// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class SymbolValueFormsModel
    {
        private const string IdentityValueFormName = IdentityValueForm.FormName;

        private SymbolValueFormsModel(IReadOnlyList<string> globalForms)
        {
            GlobalForms = globalForms;
        }

        internal static SymbolValueFormsModel Empty { get; } = new SymbolValueFormsModel(Array.Empty<string>());

        // by default, symbols get the "identity" value form, for a direct replacement
        internal static SymbolValueFormsModel Default { get; } = new SymbolValueFormsModel(new List<string>() { IdentityValueFormName });

        internal IReadOnlyList<string> GlobalForms { get; }

        // Sets up the value forms for a symbol, based on configuration from template.json
        // There are two acceptable configuration formats for each forms specification.
        //
        // Note: in the examples below, "global" is used. But we'll be extending this to allow
        //  conditional forms, which will have other names.
        //  The same format will be used for other named form definitions.
        //
        // Simple:
        // "forms": {
        //   "global": [ <strings representing value form names> ]
        // }
        //
        // Detailed:
        // "forms" {
        //   "global": {
        //     "forms": [ <strings representing value form names> ],
        //     "addIdentity": <bool default true>,
        //     // other future extensions, e.g. conditionals
        //   },
        //
        // If the symbol doesn't include an "identity" form and the addIdentity flag isn't false,
        // an identity specification is added to the beginning of the symbol's value form list.
        // If there is an identity form listed, its position remains intact irrespective of the addIdentity flag.
        internal static SymbolValueFormsModel FromJObject(JObject configJson)
        {
            JToken globalConfig = configJson.Property("global").Value;
            List<string> globalForms;
            bool addIdentity;
            if (globalConfig.Type == JTokenType.Array)
            {
                // config is just an array of form names.
                globalForms = globalConfig.ArrayAsStrings().ToList();
                addIdentity = true; // default value
            }
            else if (globalConfig.Type == JTokenType.Object)
            {
                // config is an object.
                globalForms = globalConfig.ArrayAsStrings("forms").ToList();
                addIdentity = globalConfig.ToBool("addIdentity", true);
            }
            else
            {
                throw new Exception("Malformed global value forms.");
            }

            if (addIdentity && !globalForms.Contains(IdentityValueFormName, StringComparer.OrdinalIgnoreCase))
            {
                globalForms.Insert(0, IdentityValueFormName);
            }

            return new SymbolValueFormsModel(globalForms);
        }
    }
}
