// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Utilities;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class DefaultSafeNamespaceValueFormModel : IValueForm
    {
        internal const string FormName = "safe_namespace";
        private readonly string? _name;

        internal DefaultSafeNamespaceValueFormModel()
            : this(null)
        {
        }

        internal DefaultSafeNamespaceValueFormModel(string? name)
        {
            _name = name;
        }

        public virtual string Identifier => _name ?? FormName;

        public string Name => Identifier;

        public virtual IValueForm FromJObject(string name, JObject configuration)
        {
            return new DefaultSafeNamespaceValueFormModel(name);
        }

        public virtual string Process(IReadOnlyDictionary<string, IValueForm>? forms, string value)
        {
            const char invalidCharacterReplacement = '_';

            value = value ?? throw new ArgumentNullException(nameof(value));
            value = value.Trim();

            StringBuilder safeValueStr = new StringBuilder(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                if (i < value.Length - 1 && char.IsSurrogatePair(value[i], value[i + 1]))
                {
                    safeValueStr.Append(invalidCharacterReplacement);
                    // Skip both chars that make up this symbol.
                    i++;
                    continue;
                }

                bool isFirstCharacterOfIdentifier = safeValueStr.Length == 0 || safeValueStr[safeValueStr.Length - 1] == '.';
                bool isValidFirstCharacter = UnicodeCharacterUtilities.IsIdentifierStartCharacter(value[i]);
                bool isValidPartCharacter = UnicodeCharacterUtilities.IsIdentifierPartCharacter(value[i]);

                if (isFirstCharacterOfIdentifier && !isValidFirstCharacter && isValidPartCharacter)
                {
                    // This character cannot be at the beginning, but is good otherwise. Prefix it with something valid.
                    safeValueStr.Append(invalidCharacterReplacement);
                    safeValueStr.Append(value[i]);
                }
                else if ((isFirstCharacterOfIdentifier && isValidFirstCharacter) ||
                        (!isFirstCharacterOfIdentifier && isValidPartCharacter) ||
                        (safeValueStr.Length > 0 && i < value.Length - 1 && value[i] == '.'))
                {
                    // This character is allowed to be where it is.
                    safeValueStr.Append(value[i]);
                }
                else
                {
                    safeValueStr.Append(invalidCharacterReplacement);
                }
            }

            return safeValueStr.ToString();
        }
    }
}
