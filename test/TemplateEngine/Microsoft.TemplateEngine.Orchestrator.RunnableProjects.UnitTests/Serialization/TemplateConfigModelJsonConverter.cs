// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Serialization
{
    internal class TemplateConfigModelJsonConverter : JsonConverter<TemplateConfigModel>
    {
        //falls back to default de-serializer if not implemented
        public override TemplateConfigModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotImplementedException();

        public override void Write(Utf8JsonWriter writer, TemplateConfigModel value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                return;
            }
            writer.WriteStartObject();
            writer.WritePropertyName("identity");
            writer.WriteStringValue(value.Identity);
            writer.WritePropertyName("name");
            writer.WriteStringValue(value.Name);
            writer.WritePropertyName("shortName");

            if (value.ShortNameList.Count > 1)
            {
                writer.WriteStartArray();
                foreach (string shortName in value.ShortNameList)
                {
                    if (!string.IsNullOrEmpty(shortName))
                    {
                        writer.WriteStringValue(shortName);
                    }
                }
                writer.WriteEndArray();
            }
            else if (value.ShortNameList.Count == 1)
            {
                writer.WriteStringValue(value.ShortNameList[0]);
            }
            else
            {
                writer.WriteStringValue(string.Empty);
            }

            if (!string.IsNullOrEmpty(value.GroupIdentity))
            {
                writer.WritePropertyName("groupIdentity");
                writer.WriteStringValue(value.GroupIdentity);
            }
            if (value.Precedence != 0)
            {
                writer.WritePropertyName("precedence");
                writer.WriteNumberValue(value.Precedence);
            }
            if (!string.IsNullOrEmpty(value.Author))
            {
                writer.WritePropertyName("author");
                writer.WriteStringValue(value.Author);
            }
            if (!string.IsNullOrEmpty(value.Description))
            {
                writer.WritePropertyName("description");
                writer.WriteStringValue(value.Description);
            }
            if (!string.IsNullOrEmpty(value.ThirdPartyNotices))
            {
                writer.WritePropertyName("thirdPartyNotices");
                writer.WriteStringValue(value.ThirdPartyNotices);
            }
            if (!string.IsNullOrEmpty(value.DefaultName))
            {
                writer.WritePropertyName("defaultName");
                writer.WriteStringValue(value.DefaultName);
            }
            if (!string.IsNullOrEmpty(value.SourceName))
            {
                writer.WritePropertyName("sourceName");
                writer.WriteStringValue(value.SourceName);
            }
            if (!string.IsNullOrEmpty(value.PlaceholderFilename))
            {
                writer.WritePropertyName("placeholderFilename");
                writer.WriteStringValue(value.PlaceholderFilename);
            }
            if (!string.IsNullOrEmpty(value.GeneratorVersions))
            {
                writer.WritePropertyName("generatorVersions");
                writer.WriteStringValue(value.GeneratorVersions);
            }
            if (value.PreferNameDirectory)
            {
                writer.WritePropertyName("preferNameDirectory");
                writer.WriteBooleanValue(value.PreferNameDirectory);
            }
            if (value.PreferDefaultName)
            {
                writer.WritePropertyName("preferDefaultName");
                writer.WriteBooleanValue(value.PreferDefaultName);
            }

            if (value.Classifications.Any())
            {
                writer.WritePropertyName("classifications");
                writer.WriteStartArray();
                foreach (string classification in value.Classifications)
                {
                    if (!string.IsNullOrEmpty(classification))
                    {
                        writer.WriteStringValue(classification);
                    }
                }
                writer.WriteEndArray();
            }

            if (value.Guids.Any())
            {
                writer.WritePropertyName("guids");
                writer.WriteStartArray();
                foreach (Guid guid in value.Guids)
                {
                    writer.WriteStringValue(guid.ToString());
                }
                writer.WriteEndArray();
            }

            if (value.Tags.Any())
            {
                writer.WritePropertyName("tags");
                writer.WriteStartObject();
                foreach (KeyValuePair<string, string> tag in value.Tags)
                {
                    writer.WritePropertyName(tag.Key);
                    writer.WriteStringValue(tag.Value);
                }
                writer.WriteEndObject();
            }
            if (value.Sources.Any())
            {
                writer.WritePropertyName("sources");
                writer.WriteStartArray();
                foreach (ExtendedFileSource source in value.Sources)
                {
                    writer.WriteStartObject();
                    if (!string.IsNullOrEmpty(source.Source))
                    {
                        writer.WritePropertyName("source");
                        writer.WriteStringValue(source.Source);
                    }
                    if (!string.IsNullOrEmpty(source.Target))
                    {
                        writer.WritePropertyName("target");
                        writer.WriteStringValue(source.Target);
                    }
                    if (!string.IsNullOrEmpty(source.Condition))
                    {
                        writer.WritePropertyName("condition");
                        writer.WriteStringValue(source.Condition);
                    }
                    if (source.Include.Any())
                    {
                        writer.WritePropertyName("include");
                        if (source.Include.Count == 1)
                        {
                            writer.WriteStringValue(source.Include[0]);
                        }
                        else
                        {
                            writer.WriteStartArray();
                            foreach (string el in source.Include)
                            {
                                writer.WriteStringValue(el);
                            }
                            writer.WriteEndArray();
                        }
                    }
                    if (source.Exclude.Any())
                    {
                        writer.WritePropertyName("exclude");
                        if (source.Exclude.Count == 1)
                        {
                            writer.WriteStringValue(source.Exclude[0]);
                        }
                        else
                        {
                            writer.WriteStartArray();
                            foreach (string el in source.Exclude)
                            {
                                writer.WriteStringValue(el);
                            }
                            writer.WriteEndArray();
                        }
                    }
                    if (source.CopyOnly.Any())
                    {
                        writer.WritePropertyName("copyOnly");
                        if (source.CopyOnly.Count == 1)
                        {
                            writer.WriteStringValue(source.CopyOnly[0]);
                        }
                        else
                        {
                            writer.WriteStartArray();
                            foreach (string el in source.CopyOnly)
                            {
                                writer.WriteStringValue(el);
                            }
                            writer.WriteEndArray();
                        }
                    }
                    if (source.Rename.Any())
                    {
                        writer.WritePropertyName("rename");
                        foreach (KeyValuePair<string, string> el in source.Rename)
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName(el.Key);
                            writer.WriteStringValue(el.Value);
                            writer.WriteEndObject();
                        }
                    }
                    if (source.Modifiers.Any())
                    {
                        writer.WritePropertyName("modifiers");
                        writer.WriteStartArray();
                        foreach (SourceModifier mod in source.Modifiers)
                        {
                            writer.WriteStartObject();
                            if (!string.IsNullOrEmpty(mod.Condition))
                            {
                                writer.WritePropertyName("condition");
                                writer.WriteStringValue(mod.Condition);
                            }
                            if (mod.Include.Any())
                            {
                                writer.WritePropertyName("include");
                                if (mod.Include.Count == 1)
                                {
                                    writer.WriteStringValue(mod.Include[0]);
                                }
                                else
                                {
                                    writer.WriteStartArray();
                                    foreach (string el in mod.Include)
                                    {
                                        writer.WriteStringValue(el);
                                    }
                                    writer.WriteEndArray();
                                }
                            }
                            if (mod.Exclude.Any())
                            {
                                writer.WritePropertyName("exclude");
                                if (mod.Exclude.Count == 1)
                                {
                                    writer.WriteStringValue(mod.Exclude[0]);
                                }
                                else
                                {
                                    writer.WriteStartArray();
                                    foreach (string el in mod.Exclude)
                                    {
                                        writer.WriteStringValue(el);
                                    }
                                    writer.WriteEndArray();
                                }
                            }
                            if (mod.CopyOnly.Any())
                            {
                                writer.WritePropertyName("copyOnly");
                                if (mod.CopyOnly.Count == 1)
                                {
                                    writer.WriteStringValue(mod.CopyOnly[0]);
                                }
                                else
                                {
                                    writer.WriteStartArray();
                                    foreach (string el in mod.CopyOnly)
                                    {
                                        writer.WriteStringValue(el);
                                    }
                                    writer.WriteEndArray();
                                }
                            }
                            if (mod.Rename.Any())
                            {
                                writer.WritePropertyName("rename");
                                foreach (KeyValuePair<string, string> el in mod.Rename)
                                {
                                    writer.WriteStartObject();
                                    writer.WritePropertyName(el.Key);
                                    writer.WriteStringValue(el.Value);
                                    writer.WriteEndObject();
                                }
                            }
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }

            if (value.PostActionModels.Any())
            {
                writer.WritePropertyName("postActions");
                writer.WriteStartArray();
                foreach (PostActionModel model in value.PostActionModels)
                {
                    writer.WriteStartObject();
                    if (!string.IsNullOrEmpty(model.Id))
                    {
                        writer.WritePropertyName("id");
                        writer.WriteStringValue(model.Id);
                    }
                    writer.WritePropertyName("actionId");
                    writer.WriteStringValue(model.ActionId);
                    if (!string.IsNullOrEmpty(model.Description))
                    {
                        writer.WritePropertyName("description");
                        writer.WriteStringValue(model.Description);
                    }
                    writer.WritePropertyName("continueOnError");
                    writer.WriteBooleanValue(model.ContinueOnError);

                    if (model.Args.Any())
                    {
                        writer.WritePropertyName("args");
                        writer.WriteStartObject();
                        foreach (KeyValuePair<string, string> arg in model.Args)
                        {
                            writer.WritePropertyName(arg.Key);
                            writer.WriteStringValue(arg.Value);
                        }
                        writer.WriteEndObject();
                    }

                    if (model.ManualInstructionInfo.Any())
                    {
                        writer.WritePropertyName("manualInstructions");
                        writer.WriteStartArray();
                        foreach (ManualInstructionModel mi in model.ManualInstructionInfo)
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("text");
                            writer.WriteStringValue(mi.Text);
                            if (!string.IsNullOrEmpty(mi.Condition))
                            {
                                writer.WritePropertyName("condition");
                                writer.WriteStringValue(mi.Condition);
                            }
                            if (!string.IsNullOrEmpty(mi.Id))
                            {
                                writer.WritePropertyName("id");
                                writer.WriteStringValue(mi.Id);
                            }
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                    }
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            //not implemented
            if (value.Forms.Values.Any(f => !f.IsDefault))
            {
                throw new NotSupportedException("Forms are not supported for serialization to JSON.");
            }
            if (value.BaselineInfo.Any())
            {
                throw new NotSupportedException("Baselines are not supported for serialization to JSON.");
            }
            if (value.Symbols.Any(s => !s.IsImplicit))
            {
                writer.WritePropertyName("symbols");
                writer.WriteStartObject();
                foreach (ParameterSymbol p in value.Symbols.OfType<ParameterSymbol>())
                {
                    if (p.IsImplicit)
                    {
                        continue;
                    }
                    JsonSerializer.Serialize(writer, p, options);
                    //writer.WriteRaw(JsonConvert.SerializeObject(p, ParameterSymbolJsonConverter.Instance));
                }
                writer.WriteEndObject();

                if (value.Symbols.Any(s => s is not ParameterSymbol && !s.IsImplicit))
                {
                    throw new NotSupportedException("Symbols are not supported for serialization to JSON.");
                }
            }
            if (value.PrimaryOutputs.Any())
            {
                throw new NotSupportedException("Primary outputs are not supported for serialization to JSON.");
            }
            if (value.GlobalCustomOperations != null)
            {
                throw new NotSupportedException("Global custom operations are not supported for serialization to JSON.");
            }
            if (value.SpecialCustomOperations.Any())
            {
                throw new NotSupportedException("Special custom operations are not supported for serialization to JSON.");
            }
            if (value.Constraints.Any())
            {
                throw new NotSupportedException("Constraints are not supported for serialization to JSON.");
            }

            writer.WriteEndObject();
        }

        internal static TemplateConfigModelJsonConverter Instance { get; } = new TemplateConfigModelJsonConverter();
    }
}
