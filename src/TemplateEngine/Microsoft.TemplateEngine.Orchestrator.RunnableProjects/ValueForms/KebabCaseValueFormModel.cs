using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    public class KebabCaseValueFormModel : IValueForm
    {
        public string Identifier => "kebabCase";

        public string Name { get; }

        public KebabCaseValueFormModel()
        {
        }

        public KebabCaseValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new KebabCaseValueFormModel(name);
        }

        /// <summary>
        /// PascalCase to kebab-case using Microsoft's capitalization conventions (https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/capitalization-conventions).
        /// Todd Skelton's solution (https://stackoverflow.com/a/54012346/164680)
        /// </summary>
        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            if (value is null)
            {
                return null;
            }
            if (value.Length == 0)
            {
                return string.Empty;
            }
            var builder = new StringBuilder();

            for (var i = 0; i < value.Length; i++)
            {
                if (char.IsLower(value[i])) // if current char is already lowercase
                {
                    builder.Append(value[i]);
                }
                else if (i == 0) // if current char is the first char
                {
                    builder.Append(char.ToLower(value[i]));
                }
                else if (char.IsLower(value[i - 1])) // if current char is upper and previous char is lower
                {
                    builder.Append("-");
                    builder.Append(char.ToLower(value[i]));
                }
                else if (i + 1 == value.Length || char.IsUpper(value[i + 1])) // if current char is upper and next char doesn't exist or is upper
                {
                    builder.Append(char.ToLower(value[i]));
                }
                else // if current char is upper and next char is lower
                {
                    builder.Append("-");
                    builder.Append(char.ToLower(value[i]));
                }
            }
            return builder.ToString();
        }
    }
}
