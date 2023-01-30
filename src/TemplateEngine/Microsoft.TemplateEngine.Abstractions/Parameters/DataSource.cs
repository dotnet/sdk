// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Abstractions.Parameters
{
    public enum DataSource
    {
        /// <summary>
        /// Value was not set.
        /// </summary>
        NoSource,

        /// <summary>
        /// Those are values supplied by the host. This usually means value(s) was/were set by user.
        /// </summary>
        User,

        /// <summary>
        /// Value obtained via <see cref="ITemplateEngineHost.TryGetHostParamDefault"/>.
        /// </summary>
        HostDefault,

        /// <summary>
        /// Value obtained via ITemplateEngineHost.OnParameterError.
        /// </summary>
        [Obsolete("The value is not used anymore due to ITemplateEngineHost.OnParameterError was removed.")]
        HostOnError,

        /// <summary>
        /// Value from template - <see cref="ITemplateParameter.DefaultValue"/>.
        /// </summary>
        Default,

        /// <summary>
        /// Value from template - <see cref="ITemplateParameter.DefaultIfOptionWithoutValue"/>.
        /// </summary>
        DefaultIfNoValue,

        /// <summary>
        /// This corresponds to Name implicit parameter value.
        /// </summary>
        NameParameter,

        /// <summary>
        /// To be used in case host uses advanced object model to supply values to TemplateCreator or Generator and
        ///  wants to indicate that it used some custom logic of inferring value for parameter
        ///  (e.g. custom Host calculated value of parameter based on current context and supplied the value to template engine).
        /// </summary>
        HostOther,
    }
}
