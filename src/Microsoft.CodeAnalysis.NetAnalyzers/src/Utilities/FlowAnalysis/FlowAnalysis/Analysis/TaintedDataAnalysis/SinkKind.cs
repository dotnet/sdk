// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Analyzer.Utilities.FlowAnalysis.Analysis.TaintedDataAnalysis
{
    public enum SinkKind
    {
        Sql,
        Dll,
        InformationDisclosure,
        Xss,
        FilePathInjection,
        ProcessCommand,
        Regex,
        Ldap,
        Redirect,
        XPath,
        Xml,
        Xaml,
        ZipSlip,
        HardcodedEncryptionKey,
        HardcodedCertificate,
    }
}
