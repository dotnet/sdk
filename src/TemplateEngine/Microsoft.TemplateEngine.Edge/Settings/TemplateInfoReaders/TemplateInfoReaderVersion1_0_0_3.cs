// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings.TemplateInfoReaders
{
    public class TemplateInfoReaderVersion1_0_0_3 : TemplateInfoReaderVersion1_0_0_2
    {
        public static new TemplateInfo FromJObject(JObject jObject)
        {
            TemplateInfoReaderVersion1_0_0_3 reader = new TemplateInfoReaderVersion1_0_0_3();
            return reader.Read(jObject);
        }

        public override TemplateInfo Read(JObject jObject)
        {
            TemplateInfo info = base.Read(jObject);
            return info;
        }
    }
}
