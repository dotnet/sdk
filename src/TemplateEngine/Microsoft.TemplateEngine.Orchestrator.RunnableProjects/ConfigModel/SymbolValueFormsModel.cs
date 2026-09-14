// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    public class SymbolValueFormsModel
    {
        internal SymbolValueFormsModel(IReadOnlyList<string> globalForms)
        {
            GlobalForms = globalForms;
        }

        public IReadOnlyList<string> GlobalForms { get; }

        internal static SymbolValueFormsModel Empty { get; } = new SymbolValueFormsModel([]);

        // by default, symbols get the "identity" value form, for a direct replacement
        internal static SymbolValueFormsModel Default { get; } = new SymbolValueFormsModel(new[] { IdentityValueFormFactory.FormIdentifier });

        internal static SymbolValueFormsModel NameForms { get; } = new SymbolValueFormsModel(new[]
        {
            IdentityValueFormFactory.FormIdentifier,
            DefaultSafeNameValueFormFactory.FormIdentifier,
            DefaultLowerSafeNameValueFormFactory.FormIdentifier,
            DefaultSafeNamespaceValueFormFactory.FormIdentifier,
            DefaultLowerSafeNamespaceValueFormFactory.FormIdentifier
        });

        internal static SymbolValueFormsModel FromJObject(JsonObject configJson)
        {
            JsonNode? globalConfig = JExtensions.GetPropertyCaseInsensitive(configJson, "global");
            List<string> globalForms;
            bool addIdentity;

            if (globalConfig?.GetValueKind() == JsonValueKind.Array)
            {
                // config is just an array of form names.
                globalForms = globalConfig.ArrayAsStrings().ToList();
                addIdentity = true; // default value
            }
            else if (globalConfig?.GetValueKind() == JsonValueKind.Object)
            {
                // config is an object.
                globalForms = globalConfig.ArrayAsStrings("forms").ToList();
                addIdentity = globalConfig.ToBool("addIdentity", true);
            }
            else
            {
                throw new Exception("Malformed global value forms.");
            }

            if (addIdentity && !globalForms.Contains(IdentityValueFormFactory.FormIdentifier, StringComparer.OrdinalIgnoreCase))
            {
                globalForms.Insert(0, IdentityValueFormFactory.FormIdentifier);
            }

            return new SymbolValueFormsModel(globalForms);
        }
    }
}
