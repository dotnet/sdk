// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings.TemplateInfoReaders
{
    public class TemplateInfoReaderVersion1_0_0_1 : TemplateInfoReaderVersion1_0_0_0
    {
        public static new TemplateInfo FromJObject(JObject jObject)
        {
            TemplateInfoReaderVersion1_0_0_1 reader = new TemplateInfoReaderVersion1_0_0_1();
            return reader.Read(jObject);
        }
    }
}
