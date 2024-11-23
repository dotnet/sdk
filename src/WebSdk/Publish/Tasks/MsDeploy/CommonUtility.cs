// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

///--------------------------------------------------------------------------------------------
/// CommonUtility.cs
///
/// Common utility function
///
/// Copyright(c) 2006 Microsoft Corporation
///--------------------------------------------------------------------------------------------

using System.Diagnostics;
using System.Reflection;
using System.Xml;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;
using CultureInfo = System.Globalization.CultureInfo;
using Framework = Microsoft.Build.Framework;
using RegularExpressions = System.Text.RegularExpressions;
using Utilities = Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    internal enum PipelineMetadata
    {
        //<ItemDefinitionGroup>
        //  <FilesForPackagingFromProject>
        //    <DestinationRelativePath></DestinationRelativePath>
        //    <Exclude>False</Exclude>
        //    <FromTarget>Unknown</FromTarget>
        //    <Category>Run</Category>
        //  </FilesForPackagingFromProject>
        //</ItemDefinitionGroup>
        DestinationRelativePath,
        Exclude,
        FromTarget,
        Category,
    };

    internal enum ReplaceRuleMetadata
    {
        //<ItemDefinitionGroup>
        //  <MsDeployReplaceRules>
        //    <ObjectName></ObjectName>
        //    <ScopeAttributeName></ScopeAttributeName>
        //    <ScopeAttributeValue></ScopeAttributeValue>
        //    <TargetAttributeName></TargetAttributeName>
        //    <Match></Match>
        //    <Replace></Replace>
        //  </MsDeployReplaceRules>
        //</ItemDefinitionGroup>
        ObjectName,
        ScopeAttributeName,
        ScopeAttributeValue,
        TargetAttributeName,
        Match,
        Replace,
    };

    internal enum SkipRuleMetadata
    {
        //<ItemDefinitionGroup>
        //  <MsDeploySkipRules>
        //    <SkipAction></SkipAction>
        //    <ObjectName></ObjectName>
        //    <AbsolutePath></AbsolutePath>
        //    <XPath></XPath>
        //    <KeyAttribute></KeyAttribute>
        //  </MsDeploySkipRules>
        //</ItemDefinitionGroup>
        SkipAction,
        ObjectName,
        AbsolutePath,
        XPath,
        KeyAttribute,
    };

    internal enum DeclareParameterMetadata
    {
        //<ItemDefinitionGroup>
        //  <MsDeployDeclareParameters>
        //    <Kind></Kind>
        //    <Scope></Scope>
        //    <Match></Match>
        //    <Description></Description>
        //    <DefaultValue></DefaultValue>
        //  </MsDeployDeclareParameters>
        //</ItemDefinitionGroup>
        Kind,
        Scope,
        Match,
        Description,
        DefaultValue,
        Tags,
    };

    internal enum ExistingDeclareParameterMetadata
    {
        //<ItemDefinitionGroup>
        //  <MsDeployDeclareParameters>
        //    <Kind></Kind>
        //    <Scope></Scope>
        //    <Match></Match>
        //    <Description></Description>
        //    <DefaultValue></DefaultValue>
        //  </MsDeployDeclareParameters>
        //</ItemDefinitionGroup>
        Kind,
        Scope,
        Match,
    };

    internal enum SimpleSyncParameterMetadata
    {
        //<ItemDefinitionGroup>
        //  <MsDeploySimpleSetParameters>
        //    <Value></Value>
        //  </MsDeploySimpleSetParameters>
        //</ItemDefinitionGroup>
        Value,
    }

    internal enum SqlCommandVariableMetaData
    {
        Value,
        IsDeclared,
        SourcePath,
        SourcePath_RegExEscaped,
        DestinationGroup
    }

    internal enum ExistingParameterValidationMetadata
    {
        Element,
        Kind,
        ValidationString,
    }

    internal enum SyncParameterMetadata
    {
        //<ItemDefinitionGroup>
        //  <MsDeploySetParameters>
        //    <Kind></Kind>
        //    <Scope></Scope>
        //    <Match></Match>
        //    <Value></Value>
        //  </MsDeploySetParameters>
        //</ItemDefinitionGroup>
        Kind,
        Scope,
        Match,
        Value,
        Description,
        DefaultValue,
        Tags,
    };

    internal enum ExistingSyncParameterMetadata
    {
        //<ItemDefinitionGroup>
        //  <MsDeploySetParameters>
        //    <Kind></Kind>
        //    <Scope></Scope>
        //    <Match></Match>
        //    <Value></Value>
        //  </MsDeploySetParameters>
        //</ItemDefinitionGroup>
        Kind,
        Scope,
        Match,
        Value,
    };

    internal class ParameterInfo
    {
        public string Name;
        public string Value;
        public ParameterInfo(string parameterName, string parameterStringValue)
        {
            Name = parameterName;
            Value = parameterStringValue;
        }
    }

    internal class ProviderOption : ParameterInfo
    {
        public string FactoryName;
        public ProviderOption(string factorName, string parameterName, string parameterStringValue) :
            base(parameterName, parameterStringValue)
        {
            FactoryName = factorName;
        }
    }

    internal class ParameterInfoWithEntry : ParameterInfo
    {
        //Kind,
        //Scope,
        //Match,
        //Value,
        //Description,
        //DefaultValue,
        public string Kind;
        public string Scope;
        public string Match;
        public string Description;
        public string DefaultValue;
        public string Tags;
        public string Element;
        public string ValidationString;

        public ParameterInfoWithEntry(string name, string value, string kind, string scope, string matchRegularExpression, string description, string defaultValue, string tags, string element, string validationString) :
            base(name, value)
        {
            Kind = kind;
            Scope = scope;
            Match = matchRegularExpression;
            Description = description;
            DefaultValue = defaultValue;
            Tags = tags;
            Element = element;
            ValidationString = validationString;
        }
    }

    internal static class Utility
    {
        static Dictionary<string, string?>? s_wellKnownNamesDict = null;
        static Dictionary<string, string?>? s_wellKnownNamesMsdeployDict = null;

        internal enum IISExpressMetadata
        {
            WebServerDirectory, WebServerManifest, WebServerAppHostConfigDirectory
        }

        public static bool IsInternalMsdeployWellKnownItemMetadata(string name)
        {
            if (Enum.TryParse<IISExpressMetadata>(name, out _))
            {
                return true;
            }
            if (string.Compare(name, "Path", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            return IsMSBuildWellKnownItemMetadata(name);
        }

        // Utility function to filter out known MSBuild Metatdata
        public static bool IsMSBuildWellKnownItemMetadata(string name)
        {
            if (s_wellKnownNamesDict == null)
            {
                string[] wellKnownNames =
                {
                    "FullPath",
                    "RootDir",
                    "Filename",
                    "Extension",
                    "RelativeDir",
                    "Directory",
                    "RecursiveDir",
                    "Identity",
                    "ModifiedTime",
                    "CreatedTime",
                    "AccessedTime",
                    "OriginalItemSpec",
                    "DefiningProjectDirectory",
                    "DefiningProjectDirectoryNoRoot",
                    "DefiningProjectExtension",
                    "DefiningProjectFile",
                    "DefiningProjectFullPath",
                    "DefiningProjectName"
                };
                s_wellKnownNamesDict = new Dictionary<string, string?>(wellKnownNames.GetLength(0), StringComparer.OrdinalIgnoreCase);

                foreach (string wellKnownName in wellKnownNames)
                {
                    s_wellKnownNamesDict.Add(wellKnownName, null);
                }
            }
            return s_wellKnownNamesDict.ContainsKey(name);
        }

        public static bool IsMsDeployWellKnownLocationInfo(string name)
        {
            if (s_wellKnownNamesMsdeployDict == null)
            {
                string[] wellKnownNames =
                {
                   "computerName",
                   "wmsvc",
                   "userName",
                   "password",
                   "includeAcls",
                   "encryptPassword",
                   "authType",
                   "prefetchPayload",
                };
                s_wellKnownNamesMsdeployDict = new Dictionary<string, string?>(wellKnownNames.GetLength(0), StringComparer.OrdinalIgnoreCase);

                foreach (string wellKnownName in wellKnownNames)
                {
                    s_wellKnownNamesMsdeployDict.Add(wellKnownName, null);
                }
            }
            return s_wellKnownNamesMsdeployDict.ContainsKey(name);
        }

        static StringBuilder? s_stringBuilder = null;

        /// <summary>
        /// common utility for Clean share common builder
        /// </summary>
        private static StringBuilder StringBuilder
        {
            get
            {
                if (s_stringBuilder == null)
                {
                    s_stringBuilder = new StringBuilder(1024);
                }
                return s_stringBuilder;
            }
        }

        /// <summary>
        /// This is the simple share clean build. Since this is an share instance
        /// make sure you don't call this on complex operation or it will be zero out unexpectedly
        /// Use this you need to be simple function which doesn't call any function that use this property
        /// Sde dev10 bug 699893
        /// </summary>
        public static StringBuilder CleanStringBuilder
        {
            get
            {
                StringBuilder sb = StringBuilder;
                sb.Remove(0, sb.Length);
                return sb;
            }
        }

#if NET472
        /// <summary>
        /// Return the current machine's IIS version
        /// </summary>
        /// <returns></returns>
        public static uint GetInstalledMajorIisVersion()
        {
            uint iisMajorVersion = 0;
            using (Win32.RegistryKey registryKey = Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Inetstp"))
            {
                if (registryKey != null)
                {
                    iisMajorVersion = Convert.ToUInt16(registryKey.GetValue(@"MajorVersion", 0), CultureInfo.InvariantCulture);
                }
            }
            return iisMajorVersion;
        }
#endif

        /// <summary>
        /// verify it is in IIS6
        /// </summary>
        /// <param name="verFromTarget"></param>
        /// <returns></returns>
        public static bool IsIis6(string verFromTarget)
        {
            return (verFromTarget == "6");
        }

        /// <summary>
        /// Main version of IIS
        /// </summary>
        public enum IisMainVersion
        {
            NonIis = 0,
            Iis6 = 6,
            Iis7 = 7
        }

        /// <summary>
        /// Return true if MSDeploy is installed
        /// </summary>
        private static bool _isMSDeployInstalled = false;
        private static string? _strErrorMessage = null;
        public static bool IsMSDeployInstalled
        {
            get
            {
                if (_isMSDeployInstalled)
                {
                    return true;
                }
                else if (_strErrorMessage != null)
                {
                    return false;
                }
                else
                {
                    try
                    {
                        _isMSDeployInstalled = CheckMSDeploymentVersion();
                    }
                    catch (FileNotFoundException ex)
                    {
                        _strErrorMessage = string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_MSDEPLOYLOADFAIL,
                            Resources.VSMSDEPLOY_MSDEPLOY32bit,
                            Resources.VSMSDEPLOY_MSDEPLOY64bit,
                            ex.Message);
                        _isMSDeployInstalled = false;
                    }

                    Debug.Assert(_isMSDeployInstalled || _strErrorMessage != null);
                    return _isMSDeployInstalled;
                }
            }
        }

        /// <summary>
        /// Return true if MSDeploy is installed, and report an error to task.Log if it's not
        /// </summary>
        public static bool CheckMSDeploymentVersion(Utilities.TaskLoggingHelper log, out string? errorMessage)
        {
            errorMessage = null;
            if (!IsMSDeployInstalled)
            {
                errorMessage = _strErrorMessage;
                log.LogError(_strErrorMessage);
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Utility function to save the given XML document in UTF8 and indented
        /// </summary>
        /// <param name="document"></param>
        public static void SaveDocument(XmlDocument document, string outputFileName, Encoding encode)
        {
#if NET472
            XmlTextWriter textWriter = new(outputFileName, encode)
            {
                Formatting = Formatting.Indented
            };
            document.Save(textWriter);
            textWriter.Close();
#else
            using (FileStream fs = new(outputFileName, FileMode.OpenOrCreate))
            {
                using (StreamWriter writer = new(fs, encode))
                {
                    XmlDeclaration xmldecl;
                    xmldecl = document.CreateXmlDeclaration("1.0", null, null);
                    xmldecl.Encoding = "utf-8";

                    // Add the new node to the document.
                    XmlElement? root = document.DocumentElement;
                    document.InsertBefore(xmldecl, root);

                    document.Save(writer);
                }
            }
#endif
        }

        /// <summary>
        /// Utility to check the MinimumVersion of Msdeploy
        /// </summary>
        static string? s_strMinimumVersion;

        /// <summary>
        /// Helper function to determine installed MSDeploy version
        /// </summary>
        private static bool CheckMSDeploymentVersion()
        {
            // Find the MinimumVersionRequirement
            Version currentMinVersion;
            if (!string.IsNullOrEmpty(s_strMinimumVersion))
            {
                currentMinVersion = new Version(s_strMinimumVersion);
            }
            else
            {
                currentMinVersion = new Version(7, 1, 614); // current drop
                {
                    string strMinimumVersion = string.Empty;
#if NET472
                    using (Win32.RegistryKey registryKeyVs = Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\11.0\WebDeploy"))
                    {
                        if (registryKeyVs != null)
                        {
                            s_strMinimumVersion = registryKeyVs.GetValue(@"MinimumMsDeployVersion", string.Empty).ToString();
                        }
                        else
                        {
                            using (Win32.RegistryKey registryKeyVsLM = Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\11.0\WebDeploy"))
                            {
                                if (registryKeyVsLM != null)
                                {
                                    s_strMinimumVersion = registryKeyVsLM.GetValue(@"MinimumMsDeployVersion", string.Empty).ToString();
                                }
                            }
                        }
                    }
#endif
                    if (!string.IsNullOrEmpty(s_strMinimumVersion))
                    {
                        currentMinVersion = new Version(strMinimumVersion);
                    }
                    else
                    {
                        s_strMinimumVersion = currentMinVersion.ToString();
                    }
                }
            }

            Debug.Assert(MSWebDeploymentAssembly.DynamicAssembly != null && MSWebDeploymentAssembly.DynamicAssembly.Assembly != null);
            if (MSWebDeploymentAssembly.DynamicAssembly != null && MSWebDeploymentAssembly.DynamicAssembly.Assembly != null)
            {
                AssemblyName assemblyName = MSWebDeploymentAssembly.DynamicAssembly.Assembly.GetName();
                Version minVersion = new(currentMinVersion.Major, currentMinVersion.Minor);
                Version? assemblyVersion = assemblyName.Version; // assembly version only accurate to the minor version
                bool fMinVersionNotMeet = false;

                if (assemblyVersion < minVersion)
                {
                    fMinVersionNotMeet = true;
                }

                if (fMinVersionNotMeet)
                {
                    _strErrorMessage = string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_MSDEPLOYLOADFAIL,
                        Resources.VSMSDEPLOY_MSDEPLOY32bit,
                        Resources.VSMSDEPLOY_MSDEPLOY64bit,
                        assemblyVersion,
                        currentMinVersion);
                    return false;
                }

                return true;
            }
            else
            {
#if NET472
                _strErrorMessage = string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_MSDEPLOYLOADFAIL,
                 Resources.VSMSDEPLOY_MSDEPLOY32bit,
                 Resources.VSMSDEPLOY_MSDEPLOY64bit,
                 new Version(),
                 currentMinVersion);
#else
                _strErrorMessage = string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_MSDEPLOYLOADFAIL,
                 Resources.VSMSDEPLOY_MSDEPLOY32bit,
                 Resources.VSMSDEPLOY_MSDEPLOY64bit,
                 new System.Version(3, 6),
                 currentMinVersion);
#endif
                return false;
            }
        }

#if NET472
        /// <summary>
        /// Return a search path for the data
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="xmlPath"></param>
        /// <param name="defaultNamespace"></param>
        /// <returns></returns>
        public static string? GetNodeFromProjectFile(XmlDocument doc, XmlNamespaceManager xmlnsManager,
            string xmlPath, string defaultNamespace)
        {
            if (doc == null)
                return null;

            string searchPath = xmlPath;
            if (!string.IsNullOrEmpty(defaultNamespace))
            {
                RegularExpressions.Regex regex = new(@"([\w]+)");
                searchPath = regex.Replace(xmlPath, defaultNamespace + @":$1");
            }

            XmlNode xmlNode = doc.SelectSingleNode(searchPath, xmlnsManager);
            if (xmlNode != null)
            {
                return xmlNode.InnerText;
            }
            return null;
        }
#endif
        /// <summary>
        /// Utility to help build the argument list from the enum type
        /// </summary>
        /// <param name="item"></param>
        /// <param name="arguments"></param>
        /// <param name="enumType"></param>
        internal static void BuildArgumentsBaseOnEnumTypeName(Framework.ITaskItem item, List<string> arguments, Type enumType, string? valueQuote)
        {
            string[] enumNames = Enum.GetNames(enumType);
            foreach (string enumName in enumNames)
            {
                string data = item.GetMetadata(enumName);
                if (!string.IsNullOrEmpty(data))
                {
                    string valueData = PutValueInQuote(data, valueQuote);
#if NET472
                    arguments.Add(string.Concat(enumName.ToLower(CultureInfo.InvariantCulture), "=", valueData));
#else
                    arguments.Add(string.Concat(enumName.ToLower(), "=", valueData));
#endif
                }
            }
        }

        internal static string AlternativeQuote(string? valueQuote)
        {
            if (string.IsNullOrEmpty(valueQuote) || valueQuote == "\"")
            {
                return "'";
            }
            else
            {
                return "\"";
            }
        }

        public static char[] s_specialCharactersForCmd = @"&()[]{}^=;!'+,`~".ToArray();
        internal static string PutValueInQuote(string value, string? quote)
        {
            if (string.IsNullOrEmpty(quote))
            {
                if (value is not null && value.IndexOfAny(s_specialCharactersForCmd) >= 0)
                {
                    // any command line special characters, we use double quote by default
                    quote = "\"";
                }
                else
                {
                    // otherwise we pick the smart one.
                    quote = AlternativeQuote(quote);
                }
                if (value is not null && value.Length != 0)
                {
                    if (value.Contains(quote))
                    {
                        quote = AlternativeQuote(quote);
                    }
                }

            }
            return string.Concat(quote, value, quote);
        }

        public static bool IsOneOf(string source, string[] listOfItems, StringComparison comparsion)
        {
            if (listOfItems is not null && !string.IsNullOrEmpty(source))
            {
                foreach (string item in listOfItems)
                {
                    if (string.Compare(source, item, comparsion) == 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Utility function to prompt common end of Execution message for msdeploy.exe
        /// 
        /// </summary>
        /// <param name="bSuccess"></param>
        /// <param name="destType"></param>
        /// <param name="destRoot"></param>
        /// <param name="Log"></param>
        public static void MsDeployExeEndOfExecuteMessage(bool bSuccess, string destType, string destRoot, Utilities.TaskLoggingHelper Log)
        {
            bool fNeedUnpackageHelpLink = false;
            string strSucceedFailMsg;
            string[] packageArchivedir = new string[] { MSDeploy.Provider.ArchiveDir, MSDeploy.Provider.Package };
            string[] ArchiveDirOnly = new string[] { MSDeploy.Provider.ArchiveDir };
            if (bSuccess)
            {
#if NET472
                if (IsOneOf(destType, packageArchivedir, StringComparison.InvariantCultureIgnoreCase))
#else
                if (IsOneOf(destType, packageArchivedir, StringComparison.OrdinalIgnoreCase))
#endif
                {
                    //strip off the trailing slash, so IO.Path.GetDirectoryName/GetFileName will return values correctly
                    destRoot = StripOffTrailingSlashes(destRoot);

                    string dir = Path.GetDirectoryName(destRoot) ?? string.Empty;
                    string dirUri = ConvertAbsPhysicalPathToAbsUriPath(dir);
#if NET472
                    if (IsOneOf(destType, ArchiveDirOnly, StringComparison.InvariantCultureIgnoreCase))
#else
                    if (IsOneOf(destType, ArchiveDirOnly, StringComparison.OrdinalIgnoreCase))
#endif
                        strSucceedFailMsg = string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_SucceedArchiveDir, string.IsNullOrEmpty(dirUri) ? destRoot : dirUri);
                    else
                        strSucceedFailMsg = string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_SucceedPackage, Path.GetFileName(destRoot), string.IsNullOrEmpty(dirUri) ? destRoot : dirUri);
                    fNeedUnpackageHelpLink = true;
                }
                else
                {
                    strSucceedFailMsg = Resources.VSMSDEPLOY_SucceedDeploy;
                }
            }
            else
            {
#if NET472
                if (IsOneOf(destType, packageArchivedir, StringComparison.InvariantCultureIgnoreCase))
#else
                if (IsOneOf(destType, packageArchivedir, StringComparison.OrdinalIgnoreCase))
#endif
                {
                    strSucceedFailMsg = Resources.VSMSDEPLOY_FailedPackage;
                }
                else
                {
                    strSucceedFailMsg = Resources.VSMSDEPLOY_FailedDeploy;
                }
            }
            Log.LogMessage(Framework.MessageImportance.High, strSucceedFailMsg);
            if (fNeedUnpackageHelpLink)
            {
                Log.LogMessage(Framework.MessageImportance.High, Resources.VSMSDEPLOY_WebPackageHelpLinkMessage);
                Log.LogMessage(Framework.MessageImportance.High, Resources.VSMSDEPLOY_WebPackageHelpLink);
            }
        }

        /// <summary>
        /// Utility function to prompt common end of Execution message
        /// </summary>
        /// <param name="bSuccess"></param>
        /// <param name="destType"></param>
        /// <param name="destRoot"></param>
        /// <param name="Log"></param>
        public static void MsDeployEndOfExecuteMessage(bool bSuccess, string destType, string destRoot, Utilities.TaskLoggingHelper Log)
        {
            // Deployment.DeploymentWellKnownProvider wellKnownProvider =  Deployment.DeploymentWellKnownProvider.Unknown;
            Type? DeploymentWellKnownProviderType = MSWebDeploymentAssembly.DynamicAssembly?.GetType(MSDeploy.TypeName.DeploymentWellKnownProvider);
            dynamic? wellKnownProvider = MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, "Unknown");
#if NET472
            if (string.Compare(destType, MSDeploy.Provider.DbDacFx, StringComparison.InvariantCultureIgnoreCase) != 0)
#else
            if (string.Compare(destType, MSDeploy.Provider.DbDacFx, StringComparison.OrdinalIgnoreCase) != 0)
#endif
            {
                try
                {
                    if (DeploymentWellKnownProviderType is not null)
                    {
                        wellKnownProvider = Enum.Parse(DeploymentWellKnownProviderType, destType, true);
                    }
                }
                catch
                {
                    // don't cause the failure;
                }
            }
            bool fNeedUnpackageHelpLink = false;
            string strSucceedFailMsg;
            if (bSuccess)
            {
                if (wellKnownProvider?.Equals(MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.ArchiveDir)) ?? false ||
                    wellKnownProvider?.Equals(MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.Package)) ?? false)
                {
                    //strip off the trailing slash, so IO.Path.GetDirectoryName/GetFileName will return values correctly
                    destRoot = StripOffTrailingSlashes(destRoot);

                    string dir = Path.GetDirectoryName(destRoot) ?? string.Empty;
                    string dirUri = ConvertAbsPhysicalPathToAbsUriPath(dir);
                    if (wellKnownProvider?.Equals(MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.ArchiveDir)) ?? false)
                        strSucceedFailMsg = string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_SucceedArchiveDir, string.IsNullOrEmpty(dirUri) ? destRoot : dirUri);
                    else
                        strSucceedFailMsg = string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_SucceedPackage, Path.GetFileName(destRoot), string.IsNullOrEmpty(dirUri) ? destRoot : dirUri);
                    fNeedUnpackageHelpLink = true;
                }
                else
                {
                    strSucceedFailMsg = Resources.VSMSDEPLOY_SucceedDeploy;
                }
            }
            else
            {
                if (wellKnownProvider?.Equals(MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.ArchiveDir)) ?? false ||
                    wellKnownProvider?.Equals(MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.Package)) ?? false)
                {
                    strSucceedFailMsg = Resources.VSMSDEPLOY_FailedPackage;
                }
                else
                {
                    strSucceedFailMsg = Resources.VSMSDEPLOY_FailedDeploy;
                }
            }
            Log.LogMessage(Framework.MessageImportance.High, strSucceedFailMsg);
            if (fNeedUnpackageHelpLink)
            {
                Log.LogMessage(Framework.MessageImportance.High, Resources.VSMSDEPLOY_WebPackageHelpLinkMessage);
                Log.LogMessage(Framework.MessageImportance.High, Resources.VSMSDEPLOY_WebPackageHelpLink);
            }
        }

        public static string ConvertAbsPhysicalPathToAbsUriPath(string physicalPath)
        {
            string absUriPath = string.Empty;
            try
            {
                Uri uri = new(physicalPath);
                if (uri.IsAbsoluteUri)
                    absUriPath = uri.AbsoluteUri;
            }
            catch { }
            return absUriPath;
        }

        // utility function to add the replace rule for the option
        public static void AddReplaceRulesToOptions(/*Deployment.DeploymentRuleCollection*/ dynamic syncConfigRules, Framework.ITaskItem[] replaceRuleItems)
        {
            if (syncConfigRules is not null && replaceRuleItems is not null)// Dev10 bug 496639 foreach will throw the exception if the replaceRuleItem is null
            {
                foreach (Framework.ITaskItem item in replaceRuleItems)
                {
                    string ruleName = item.ItemSpec;
                    string objectName = item.GetMetadata(ReplaceRuleMetadata.ObjectName.ToString());
                    string matchRegularExpression = item.GetMetadata(ReplaceRuleMetadata.Match.ToString());
                    string replaceWith = item.GetMetadata(ReplaceRuleMetadata.Replace.ToString());
                    string scopeAttributeName = item.GetMetadata(ReplaceRuleMetadata.ScopeAttributeName.ToString());
                    string scopeAttributeValue = item.GetMetadata(ReplaceRuleMetadata.ScopeAttributeValue.ToString());
                    string targetAttributeName = item.GetMetadata(ReplaceRuleMetadata.TargetAttributeName.ToString());

                    ///*Deployment.DeploymentReplaceRule*/ dynamic replaceRule =
                    //    new Deployment.DeploymentReplaceRule(ruleName, objectName, scopeAttributeName, 
                    //        scopeAttributeValue, targetAttributeName, matchRegularExpression, replaceWith);

                    /*Deployment.DeploymentReplaceRule*/
                    dynamic? replaceRule = MSWebDeploymentAssembly.DynamicAssembly?.CreateObject("Microsoft.Web.Deployment.DeploymentReplaceRule",
                        new object[]{ruleName, objectName, scopeAttributeName,
                        scopeAttributeValue, targetAttributeName, matchRegularExpression, replaceWith});

                    syncConfigRules.Add(replaceRule);
                }
            }
        }

        /// <summary>
        /// utility function to enable the skip directive's enable state
        /// </summary>
        /// <param name="baseOptions"></param>
        /// <param name="stringList"></param>
        /// <param name="enabled"></param>
        /// <param name="log"></param>
        internal static void AdjustSkipDirectives(/*Deployment.DeploymentBaseOptions*/ dynamic baseOptions, List<string> stringList, bool enabled, Utilities.TaskLoggingHelper log)
        {
            if (stringList is not null && baseOptions is not null)
            {
                foreach (string name in stringList)
                {
                    foreach (/*Deployment.DeploymentSkipDirective*/ dynamic skipDirective in baseOptions.SkipDirectives)
                    {
                        if (string.Compare(skipDirective.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            if (skipDirective.Enabled != enabled)
                            {
                                skipDirective.Enabled = enabled;
                            }
                            log.LogMessage(string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_SkipDirectiveSetEnable, skipDirective.Name, enabled.ToString()));

                        }
                    }
                    log.LogWarning(string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_UnknownSkipDirective, name));
                }
            }
        }

        // utility function to add the skip rule for the option
        public static void AddSkipDirectiveToBaseOptions(/*Deployment.DeploymentBaseOptions*/ dynamic baseOptions,
            Framework.ITaskItem[] skipRuleItems,
            List<string> enableSkipDirectiveList,
            List<string> disableSkipDirectiveList,
            Utilities.TaskLoggingHelper log)
        {
            if (baseOptions is not null && skipRuleItems is not null)
            {
                List<string> arguments = new(6);

                foreach (Framework.ITaskItem item in skipRuleItems)
                {
                    arguments.Clear();
                    BuildArgumentsBaseOnEnumTypeName(item, arguments, typeof(SkipRuleMetadata), "\"");
                    if (arguments.Count > 0)
                    {
                        string name = item.ItemSpec;
                        ///*Deployment.DeploymentSkipDirective*/ dynamic skipDirective = new Microsoft.Web.Deployment.DeploymentSkipDirective(name, string.Join(",", arguments.ToArray()), true);

                        /*Deployment.DeploymentSkipDirective*/
                        dynamic? skipDirective = MSWebDeploymentAssembly.DynamicAssembly?.CreateObject("Microsoft.Web.Deployment.DeploymentSkipDirective", new object[] { name, string.Join(",", arguments.ToArray()), true });
                        baseOptions.SkipDirectives.Add(skipDirective);
                    }
                }
                AdjustSkipDirectives(baseOptions, enableSkipDirectiveList, true, log);
                AdjustSkipDirectives(baseOptions, disableSkipDirectiveList, false, log);
            }
        }

        /// <summary>
        /// Utility to add single DeclareParameter to the list
        /// </summary>
        /// <param name="vSMSDeploySyncOption"></param>
        /// <param name="item"></param>
        public static void AddDeclareParameterToOptions(/*VSMSDeploySyncOption*/ dynamic vSMSDeploySyncOption, Framework.ITaskItem item)
        {
            if (item is not null && vSMSDeploySyncOption is not null)
            {
                string name = item.ItemSpec;
                string element = item.GetMetadata(ExistingParameterValidationMetadata.Element.ToString());
                if (string.IsNullOrEmpty(element))
                    element = "parameterEntry";
                string kind = item.GetMetadata(DeclareParameterMetadata.Kind.ToString());
                string scope = item.GetMetadata(DeclareParameterMetadata.Scope.ToString());
                string matchRegularExpression = item.GetMetadata(DeclareParameterMetadata.Match.ToString());
                string description = item.GetMetadata(DeclareParameterMetadata.Description.ToString());
                string defaultValue = item.GetMetadata(DeclareParameterMetadata.DefaultValue.ToString());
                string tags = item.GetMetadata(DeclareParameterMetadata.Tags.ToString());

                dynamic? deploymentSyncParameter = null;
                // the following have out argument, can't use dynamic on it
                // vSMSDeploySyncOption.DeclaredParameters.TryGetValue(name, out deploymentSyncParameter);
                MSWebDeploymentAssembly.DeploymentTryGetValueContains(vSMSDeploySyncOption.DeclaredParameters, name, out deploymentSyncParameter);

                if (deploymentSyncParameter == null)
                {
                    deploymentSyncParameter =
                       MSWebDeploymentAssembly.DynamicAssembly?.CreateObject("Microsoft.Web.Deployment.DeploymentSyncParameter", new object[] { name, description, defaultValue, tags });
                    vSMSDeploySyncOption.DeclaredParameters.Add(deploymentSyncParameter);
                }
                if (!string.IsNullOrEmpty(kind))
                {
                    if (string.Compare(element, "parameterEntry", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        bool fAddEntry = true;
                        foreach (dynamic entry in deploymentSyncParameter?.Entries ?? Array.Empty<dynamic>())
                        {
                            if (scope.Equals(entry.Scope) &&
                                matchRegularExpression.Equals(entry.Match) &&
                                string.Compare(entry.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                fAddEntry = false;
                            }
                        }
                        if (fAddEntry)
                        {
                            dynamic? parameterEntry = MSWebDeploymentAssembly.DynamicAssembly?.CreateObject("Microsoft.Web.Deployment.DeploymentSyncParameterEntry",
                                new object[] { kind, scope, matchRegularExpression, string.Empty });
                            deploymentSyncParameter?.Add(parameterEntry);
                        }
                    }
                    else if (string.Compare(element, "parameterValidation", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        // this is bogus assertion because by default msdeploy always setup the validation which is never be null
                        // System.Diagnostics.Debug.Assert(deploymentSyncParameter.Validation == null, "deploymentSyncParameter.Validation is already set");
                        string validationString = item.GetMetadata(ExistingParameterValidationMetadata.ValidationString.ToString());

                        object? validationKindNone = MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValue("Microsoft.Web.Deployment.DeploymentSyncParameterValidationKind", "None");
                        dynamic? validationKind = validationKindNone;
                        Type? validationKindType = validationKind?.GetType();
                        string[] validationKinds = kind.Split(new char[] { ',' });

                        foreach (string strValidationKind in validationKinds)
                        {
                            dynamic? currentValidationKind;
                            if (validationKindType is not null && (MSWebDeploymentAssembly.DynamicAssembly?.TryGetEnumValue("Microsoft.Web.Deployment.DeploymentSyncParameterValidationKind", strValidationKind, out currentValidationKind) ?? false))
                            {
                                validationKind = Enum.ToObject(validationKindType, ((int)(validationKind)) | ((int)(currentValidationKind)));
                            }
                        }
                        // dynamic doesn't compare, since this is enum, cast to int to compare
                        if (validationKind is not null && (int)validationKind != (int)(validationKindNone ?? 0))
                        {
                            // due to the reflection the we can't
                            // $exception	{"Cannot implicitly convert type 'object' to 'Microsoft.Web.Deployment.DeploymentSyncParameterValidation'. An explicit conversion exists (are you missing a cast?)"}	System.Exception {Microsoft.CSharp.RuntimeBinder.RuntimeBinderException}
                            object? parameterValidation =
                                MSWebDeploymentAssembly.DynamicAssembly?.CreateObject("Microsoft.Web.Deployment.DeploymentSyncParameterValidation", new object[] { validationKind, validationString });
                            SetDynamicProperty(deploymentSyncParameter, "Validation", parameterValidation);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Utility function to avoid the reflection exception when assign to a strongtype property
        /// // $exception	{"Cannot implicitly convert type 'object' to 'Microsoft.Web.Deployment.DeploymentSyncParameterValidation'. An explicit conversion exists (are you missing a cast?)"}	System.Exception {Microsoft.CSharp.RuntimeBinder.RuntimeBinderException}
        /// </summary>
        /// <param name="thisObj"></param>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        public static void SetDynamicProperty(dynamic? thisObj, string propertyName, object value)
        {
            thisObj?.GetType().GetProperty(propertyName).SetValue(thisObj, value, null);
        }

        /// <summary>
        /// Utility function to add DeclareParameter in line
        /// </summary>
        /// <param name="vSMSDeploySyncOption"></param>
        /// <param name="items"></param>
        public static void AddDeclareParametersToOptions(/*VSMSDeploySyncOption*/ dynamic vSMSDeploySyncOption, Framework.ITaskItem[] originalItems, bool fOptimisticPickNextDefaultValue)
        {
            IList<Framework.ITaskItem> items = SortParametersTaskItems(originalItems, fOptimisticPickNextDefaultValue, DeclareParameterMetadata.DefaultValue.ToString());
            if (vSMSDeploySyncOption is not null && items is not null)
            {
                foreach (Framework.ITaskItem item in items)
                {
                    AddDeclareParameterToOptions(vSMSDeploySyncOption, item);
                }
            }
        }

        // MSDeploy change -- Deprecate
        ///// <summary>
        ///// Utility function to support DeclareParametersFromFile
        ///// </summary>
        ///// <param name="vSMSDeploySyncOption"></param>
        ///// <param name="items"></param>
        public static void AddImportDeclareParametersFileOptions(/*VSMSDeploySyncOption*/ dynamic vSMSDeploySyncOption, Framework.ITaskItem[] items)
        {
            if (vSMSDeploySyncOption is not null && items is not null)
            {
                foreach (Framework.ITaskItem item in items)
                {
                    string fileName = item.ItemSpec;
                    vSMSDeploySyncOption.DeclaredParameters.Load(fileName);
                }
            }
        }

        public static void AddSetParametersFilesToObject(/*Deployment.DeploymentObject*/ dynamic deploymentObject, IList<string> filenames, IVSMSDeployHost host)
        {
            if (deploymentObject is not null && filenames is not null)
            {
                foreach (string filename in filenames)
                {
                    if (!string.IsNullOrEmpty(filename))
                    {
                        try
                        {
                            deploymentObject.SyncParameters.Load(filename);
                        }
                        catch (Exception e)
                        {
                            if (host != null)
                                host.Log.LogErrorFromException(e);
                            else
                                throw;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Utility function to set SimpleSyncParameter Name/Value
        /// </summary>
        /// <param name="vSMSDeploySyncOption"></param>
        /// <param name="items"></param>
        public static void AddSimpleSetParametersVsMsDeployObject(VSMSDeployObject srcVsMsDeployobject, Framework.ITaskItem[]? originalItems, bool fOptimisticPickNextDefaultValue)
        {
            IList<Framework.ITaskItem> items = SortParametersTaskItems(originalItems, fOptimisticPickNextDefaultValue, SimpleSyncParameterMetadata.Value.ToString());
            if (srcVsMsDeployobject is not null && items is not null)
            {
                string lastItemName = string.Empty;
                foreach (Framework.ITaskItem item in items)
                {
                    string name = item.ItemSpec;
                    if (string.CompareOrdinal(name, lastItemName) != 0)
                    {
                        string value = item.GetMetadata(SimpleSyncParameterMetadata.Value.ToString());
                        srcVsMsDeployobject.SyncParameter(name, value);
                        lastItemName = name;
                    }
                }
            }
        }

        public static void AddProviderOptions(/*Deployment.DeploymentProviderOptions*/ dynamic deploymentProviderOptions, IList<ProviderOption> providerOptions, IVSMSDeployHost host)
        {
            if (deploymentProviderOptions is not null && providerOptions is not null)
            {
                foreach (ProviderOption item in providerOptions)
                {
                    string factoryName = item.FactoryName;
                    string name = item.Name;
                    string value = item.Value;
                    // Error handling is not required here if the providerOptions list is different from deploymentProviderOptions.ProviderSettings.
                    // providerOptions list contains metadata from MSBuild and this may be different from deploymentProviderOptions.ProviderSettings.
                    if (string.Compare(factoryName, deploymentProviderOptions.Factory.Name, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        dynamic? setting = null;

                        // deploymentProviderOptions.ProviderSettings.TryGetValue(name, out setting);
                        MSWebDeploymentAssembly.DeploymentTryGetValueForEach(deploymentProviderOptions.ProviderSettings, name, out setting);
                        if (setting != null)
                        {
                            setting.Value = value;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Utility function to set SimpleSyncParameter Name/Value
        /// </summary>
        /// <param name="vSMSDeploySyncOption"></param>
        /// <param name="items"></param>
        public static void AddSimpleSetParametersToObject(/*Deployment.DeploymentObject*/ dynamic deploymentObject, IList<ParameterInfo> parameters, IVSMSDeployHost host)
        {
            if (deploymentObject is not null && parameters is not null)
            {
                Dictionary<string, string> nameValueDictionary = new(parameters.Count, StringComparer.OrdinalIgnoreCase);
                foreach (ParameterInfo item in parameters)
                {
                    string name = item.Name;
                    string? value;
                    if (!nameValueDictionary.TryGetValue(name, out value))
                    {
                        value = item.Value;
                    }

                    dynamic? parameter = null;
                    // deploymentObject.SyncParameters.TryGetValue(name, out parameter);
                    MSWebDeploymentAssembly.DeploymentTryGetValueContains(deploymentObject.SyncParameters, name, out parameter);
                    string msg = string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_AddParameterIntoObject, name, value, deploymentObject.Name);
                    host?.Log.LogMessage(msg);
                    if (parameter != null)
                    {
                        parameter.Value = value;
                    }
                    else
                    {
                        // Try to get error message to show.
                        StringBuilder sb = CleanStringBuilder;
                        foreach (dynamic param in deploymentObject.SyncParameters)
                        {
                            if (sb.Length != 0)
                            {
                                sb.Append(", ");
                            }
                            sb.Append(param.Name);
                        }
                        // To do, change this to resource
                        string errMessage = string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_UnknownParameter, name, sb.ToString());
                        if (host != null)
                        {
                            throw new InvalidOperationException(errMessage);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Utility function to setParameters in type, scope, match, value of SyncParameter
        /// </summary>
        /// <param name="vSMSDeploySyncOption"></param>
        /// <param name="items"></param>
        public static void AddSetParametersToObject(/*Deployment.DeploymentObject*/ dynamic deploymentObject, IList<ParameterInfoWithEntry> parameters, IVSMSDeployHost host)
        {
            if (deploymentObject is not null && parameters is not null)
            {
                Dictionary<string, string> nameValueDictionary = new(parameters.Count, StringComparer.OrdinalIgnoreCase);
                Dictionary<string, string?> entryIdentityDictionary = new(parameters.Count);

                foreach (ParameterInfoWithEntry item in parameters)
                {
                    try
                    {
                        string? data = null;
                        if (!nameValueDictionary.TryGetValue(item.Name, out data))
                        {
                            nameValueDictionary.Add(item.Name, item.Value);
                            data = item.Value;
                        }

                        dynamic? parameter = null;
                        dynamic? parameterEntry = null;
                        dynamic? parameterValidation = null;
                        if (!string.IsNullOrEmpty(item.Kind))
                        {
                            string identityString = string.Join(";", new string[] { item.Name, item.Kind, item.Scope, item.Match, item.Element, item.ValidationString });
                            if (!entryIdentityDictionary.ContainsKey(identityString))
                            {
                                if (string.Compare(item.Element, "parameterEntry", StringComparison.OrdinalIgnoreCase) == 0)
                                {
                                    parameterEntry = MSWebDeploymentAssembly.DynamicAssembly?.CreateObject("Microsoft.Web.Deployment.DeploymentSyncParameterEntry",
                                        new object[] { item.Kind, item.Scope, item.Match, string.Empty });
                                }
                                else if (string.Compare(item.Element, "parameterValidation", StringComparison.OrdinalIgnoreCase) == 0)
                                {
                                    // this is bogus assertion because by default msdeploy always setup the validation which is never be null
                                    // System.Diagnostics.Debug.Assert(deploymentSyncParameter.Validation == null, "deploymentSyncParameter.Validation is already set");
                                    string validationString = item.ValidationString;

                                    object? validationKindNone = MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValue("Microsoft.Web.Deployment.DeploymentSyncParameterValidationKind", "None");
                                    dynamic? validationKind = validationKindNone;
                                    Type? validationKindType = validationKind?.GetType();
                                    dynamic? currentvalidationKind = validationKindNone;

                                    string[] validationKinds = item.Kind.Split(new char[] { ',' });

                                    foreach (string strValidationKind in validationKinds)
                                    {
                                        if (validationKindType is not null && (MSWebDeploymentAssembly.DynamicAssembly?.TryGetEnumValue("Microsoft.Web.Deployment.DeploymentSyncParameterValidationKind", strValidationKind, out currentvalidationKind) ?? false))
                                        {
                                            validationKind = Enum.ToObject(validationKindType, ((int)(validationKind)) | ((int)(currentvalidationKind)));
                                        }
                                    }
                                    // dynamic doesn't compare, since this is enum, cast to int to compare
                                    if (validationKind is not null && (int)validationKind != (int)(validationKindNone ?? 0))
                                    {
                                        parameterValidation =
                                            MSWebDeploymentAssembly.DynamicAssembly?.CreateObject("Microsoft.Web.Deployment.DeploymentSyncParameterValidation", new object[] { validationKind, validationString });
                                    }
                                }
                                entryIdentityDictionary.Add(identityString, null);
                            }
                        }

                        if (!MSWebDeploymentAssembly.DeploymentTryGetValueContains(deploymentObject.SyncParameters, item.Name, out parameter))
                        {
                            parameter = MSWebDeploymentAssembly.DynamicAssembly?.CreateObject("Microsoft.Web.Deployment.DeploymentSyncParameter",
                                new object[] { item.Name, item.Description, item.DefaultValue, item.Tags });
                            deploymentObject.SyncParameters.Add(parameter);
                            if (parameter is not null)
                            {
                                parameter.Value = data;
                            }
                        }
                        if (parameterEntry != null)
                        {
                            parameter?.Add(parameterEntry);
                        }
                        if (parameterValidation != null)
                        {
                            // due to the reflection, compiler complain on assign a object to type without explicit conversion
                            // parameter.Validation = parameterValidation;
                            SetDynamicProperty(parameter, "Validation", parameterValidation);
                        }
                    }
                    catch (Exception e)
                    {
                        if (host != null)
                            host.Log.LogErrorFromException(e);
                        else
                            throw;
                    }
                }
            }
        }

        /// <summary>
        /// Utility function to setParameters in type, scope, match, value of SyncParameter
        /// </summary>
        /// <param name="vSMSDeploySyncOption"></param>
        /// <param name="items"></param>
        public static void AddSetParametersVsMsDeployObject(VSMSDeployObject srcVsMsDeployobject, Framework.ITaskItem[]? originalItems, bool fOptimisticPickNextDefaultValue)
        {
            IList<Framework.ITaskItem> items = SortParametersTaskItems(originalItems, fOptimisticPickNextDefaultValue, SyncParameterMetadata.DefaultValue.ToString());
            if (srcVsMsDeployobject is not null && items is not null)
            {
                foreach (Framework.ITaskItem item in items)
                {
                    string name = item.ItemSpec;
                    string kind = item.GetMetadata(SyncParameterMetadata.Kind.ToString());
                    string scope = item.GetMetadata(SyncParameterMetadata.Scope.ToString());
                    string matchRegularExpression = item.GetMetadata(SyncParameterMetadata.Match.ToString());
                    string value = item.GetMetadata(SyncParameterMetadata.Value.ToString());
                    string description = item.GetMetadata(SyncParameterMetadata.Description.ToString());
                    string defaultValue = item.GetMetadata(SyncParameterMetadata.DefaultValue.ToString());
                    string tags = item.GetMetadata(SyncParameterMetadata.Tags.ToString());
                    string element = item.GetMetadata(ExistingParameterValidationMetadata.Element.ToString());
                    if (string.IsNullOrEmpty(element))
                        element = "parameterEntry";
                    string validationString = item.GetMetadata(ExistingParameterValidationMetadata.ValidationString.ToString());

                    if (string.IsNullOrEmpty(value))
                    {
                        value = defaultValue;
                    }

                    srcVsMsDeployobject.SyncParameter(name, value, kind, scope, matchRegularExpression, description, defaultValue, tags, element, validationString);
                }
            }
        }

        public static void AddSetParametersFilesVsMsDeployObject(VSMSDeployObject srcVsMsDeployobject, Framework.ITaskItem[]? items)
        {
            if (srcVsMsDeployobject is not null && items is not null)
            {
                foreach (Framework.ITaskItem item in items)
                {
                    string filename = item.ItemSpec;
                    srcVsMsDeployobject.SyncParameterFile(filename);
                }
            }
        }

        public static string DumpITaskItem(Framework.ITaskItem iTaskItem)
        {
            StringBuilder sb = CleanStringBuilder;
            string itemSpec = iTaskItem.ItemSpec;
            sb.Append("<Item Name=\"");
            sb.Append(itemSpec);
            sb.Append("\">");

            foreach (string name in iTaskItem.MetadataNames)
            {
                string value = iTaskItem.GetMetadata(name);
                sb.Append(@"<");
                sb.Append(name);
                sb.Append(@">");
                sb.Append(value);
                sb.Append(@"</");
                sb.Append(name);
                sb.Append(@">");
            }
            sb.Append(@"</Item>");

            return sb.ToString();
        }

        public static bool IsDeploymentWellKnownProvider(string strProvider)
        {
#if NET472
            if (string.Compare(strProvider, MSDeploy.Provider.DbDacFx, StringComparison.InvariantCultureIgnoreCase) == 0)
#else
            if (string.Compare(strProvider, MSDeploy.Provider.DbDacFx, StringComparison.OrdinalIgnoreCase) == 0)
#endif
            {
                return true;
            }
            object? DeploymentWellKnownProviderUnknown = MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.Unknown);
            object? deploymentProvider = DeploymentWellKnownProviderUnknown;
            try
            {
                deploymentProvider = MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValueIgnoreCase(MSDeploy.TypeName.DeploymentWellKnownProvider, strProvider);
            }
            catch (Exception)
            {
            }
            return deploymentProvider != DeploymentWellKnownProviderUnknown;

        }

        /// <summary>
        /// Utility function to remove all Empty Directory
        /// </summary>
        /// <param name="dirPath"></param>
        internal static void RemoveAllEmptyDirectories(string dirPath, Utilities.TaskLoggingHelper Log)
        {
            if (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath))
            {
                DirectoryInfo dirInfo = new(dirPath);
                RemoveAllEmptyDirectories(dirInfo, Log);
            }
        }

        internal static void RemoveAllEmptyDirectories(DirectoryInfo dirinfo, Utilities.TaskLoggingHelper log)
        {
            if (dirinfo is not null && dirinfo.Exists)
            {
                //Depth first search.
                foreach (DirectoryInfo subDirInfo in dirinfo.GetDirectories())
                {
                    RemoveAllEmptyDirectories(subDirInfo, log);
                }

                if (dirinfo.GetFileSystemInfos().GetLength(0) == 0)
                {
                    dirinfo.Delete();
                    if (log != null)
                    {
                        log.LogMessage(Framework.MessageImportance.Normal, string.Format(CultureInfo.CurrentCulture, Resources.BUILDTASK_RemoveEmptyDirectories_Deleting, dirinfo.FullName));
                    }
                }
            }
        }

        static PriorityIndexComparer? s_PriorityIndexComparer = null;
        internal static PriorityIndexComparer ParameterTaskComparer
        {
            get
            {
                if (s_PriorityIndexComparer == null)
                {
                    s_PriorityIndexComparer = new PriorityIndexComparer();
                }
                return s_PriorityIndexComparer;
            }
        }

        public static IList<Framework.ITaskItem> SortParametersTaskItems(Framework.ITaskItem[]? taskItems, bool fOptimisticPickNextNonNullDefaultValue, string PropertyName)
        {
            IList<Framework.ITaskItem> sortedList = SortTaskItemsByPriority(taskItems);

            if (!fOptimisticPickNextNonNullDefaultValue || string.IsNullOrEmpty(PropertyName) || taskItems == null || taskItems.GetLength(0) <= 0)
            {
                return sortedList;
            }
            else
            {
                List<Framework.ITaskItem> optimizedValueList = new(sortedList);

                Dictionary<string, bool> FoundDictionary = new(optimizedValueList.Count, StringComparer.OrdinalIgnoreCase);

                int maxCount = sortedList.Count;
                int i = 0;

                while (i < maxCount)
                {
                    int currentItemIndex = i;
                    Framework.ITaskItem item = optimizedValueList[i++];
                    string itemSpec = item.ItemSpec;
                    if (FoundDictionary.ContainsKey(itemSpec))
                    {
                        continue; // already scanned, move on to the next
                    }
                    else
                    {
                        bool fIsCurrentItemEmpty = string.IsNullOrEmpty(item.GetMetadata(PropertyName));
                        if (!fIsCurrentItemEmpty)
                        {
                            FoundDictionary[itemSpec] = true;
                            continue;
                        }
                        else
                        {
                            int next = i;
                            bool found = false;
                            while (next < maxCount)
                            {
                                Framework.ITaskItem nextItem = optimizedValueList[next++];
                                if (string.Compare(itemSpec, nextItem.ItemSpec, StringComparison.OrdinalIgnoreCase) == 0)
                                {
                                    string itemData = nextItem.GetMetadata(PropertyName);
                                    if (!string.IsNullOrEmpty(itemData))
                                    {
                                        // Get the data from the next best data
                                        Utilities.TaskItem newItem = new(item);
                                        newItem.SetMetadata(PropertyName, itemData);
                                        optimizedValueList[currentItemIndex] = newItem;
                                        FoundDictionary[itemSpec] = true; // mark that we already fond teh item;
                                        found = true;
                                        break;
                                    }
                                }
                            }
                            if (!found)
                            {
                                FoundDictionary[itemSpec] = false; // mark that we scan through the item and not found anything
                            }
                        }
                    }
                }
                return optimizedValueList;
            }
        }

        static string strMsdeployFwlink1 = @"http://go.microsoft.com/fwlink/?LinkId=178034";
        static string strMsdeployFwlink2 = @"http://go.microsoft.com/fwlink/?LinkId=178035";
        static string strMsdeployFwlink3 = @"http://go.microsoft.com/fwlink/?LinkId=178036";
        static string strMsdeployFwlink4 = @"http://go.microsoft.com/fwlink/?LinkId=178587";
        static string strMsdeployFwlink5 = @"http://go.microsoft.com/fwlink/?LinkId=178589";
        internal static string strMsdeployInstallationFwdLink = @"http://go.microsoft.com/?linkid=9278654";

        static string[] strMsdeployFwlinks = { strMsdeployFwlink1, strMsdeployFwlink2, strMsdeployFwlink3, strMsdeployFwlink4, strMsdeployFwlink5 };

        static int ContainMsdeployFwlink(string errorMessage, out string? provider)
        {
            int index = -1;
            provider = null;
            string[][] strMsDeployFwlinksArray = { strMsdeployFwlinks };
            foreach (string[] Fwlinks in strMsDeployFwlinksArray)
            {
                for (int i = 0; i < Fwlinks.Length; i++)
                {
                    string fwlink = Fwlinks[i];
                    int lastIndexOfFwLink = -1;
                    if ((lastIndexOfFwLink = errorMessage.LastIndexOf(fwlink, StringComparison.OrdinalIgnoreCase)) >= 0)
                    {
                        index = i;
                        if (i == 0)
                        {
                            string subError = errorMessage.Substring(0, lastIndexOfFwLink);
                            subError = subError.Trim();
                            if ((lastIndexOfFwLink = subError.LastIndexOf(" ", StringComparison.Ordinal)) >= 0)
                            {
                                provider = subError.Substring(lastIndexOfFwLink + 1);
                            }
                        }
                        return index; // break out
                    }
                }
            }
            return index;
        }

        internal static bool IsType(Type type, Type? checkType)
        {
#if NET472
            if (checkType != null && (type == checkType || type.IsSubclassOf(checkType)))
            {
                return true;
            }
#endif
            return false;
        }

        internal static string? EnsureTrailingSlash(string str)
        {
            if (str != null && !str.EndsWith("/", StringComparison.Ordinal))
            {
                str += "/";
            }
            return str;
        }
        internal static string? EnsureTrailingBackSlash(string str)
        {
            if (str != null && !str.EndsWith("\\", StringComparison.Ordinal))
            {
                str += "\\";
            }
            return str;
        }

        // Utility to log VsMsdeploy Exception 
        internal static void LogVsMsDeployException(Utilities.TaskLoggingHelper Log, Exception e)
        {
            if (e is TargetInvocationException)
            {
                if (e.InnerException != null)
                    e = e.InnerException;
            }

            StringBuilder strBuilder = new(e.Message.Length * 4);
            Type t = e.GetType();
            if (IsType(t, MSWebDeploymentAssembly.DynamicAssembly?.GetType(MSDeploy.TypeName.DeploymentEncryptionException)))
            {
                // dev10 695263 OGF: Encryption Error message needs more information for packaging
                strBuilder.Append(Resources.VSMSDEPLOY_EncryptionExceptionMessage);
            }
            else if (IsType(t, MSWebDelegationAssembly.DynamicAssembly?.GetType(MSDeploy.TypeName.DeploymentException)))
            {
                Exception rootException = e;
                dynamic lastDeploymentException = e;
                while (rootException != null && rootException.InnerException != null)
                {
                    rootException = rootException.InnerException;
                    if (IsType(rootException.GetType(), MSWebDelegationAssembly.DynamicAssembly?.GetType(MSDeploy.TypeName.DeploymentException)))
                        lastDeploymentException = rootException;
                }

#if NET472
                bool isWebException = rootException is System.Net.WebException;
                if (isWebException)
                {
                    System.Net.WebException? webException = rootException as System.Net.WebException;

                    // 404 come in as ProtocolError
                    if (webException?.Status == System.Net.WebExceptionStatus.ProtocolError)
                    {
                        if (webException.Message.LastIndexOf("401", StringComparison.Ordinal) >= 0)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_WebException401Message);
                        }
                        else if (webException.Message.LastIndexOf("404", StringComparison.Ordinal) >= 0)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_WebException404Message);
                        }
                        else if (webException.Message.LastIndexOf("502", StringComparison.Ordinal) >= 0)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_WebException502Message);
                        }
                        else if (webException.Message.LastIndexOf("550", StringComparison.Ordinal) >= 0)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_WebException550Message);
                        }
                        else if (webException.Message.LastIndexOf("551", StringComparison.Ordinal) >= 0)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_WebException551Message);
                        }
                    }
                    else if (webException?.Status == System.Net.WebExceptionStatus.ConnectFailure)
                    {
                        strBuilder.Append(Resources.VSMSDEPLOY_WebExceptionConnectFailureMessage);
                    }
                }
                else if (rootException is System.Net.Sockets.SocketException)
                {
                    strBuilder.Append(Resources.VSMSDEPLOY_WebExceptionConnectFailureMessage);
                }
                else
                {
                    string strMsg = lastDeploymentException.Message;
                    string? provider;
                    int index = ContainMsdeployFwlink(strMsg, out provider);
                    if (index >= 0)
                    {
                        object? DeploymentWellKnownProviderUnknown = MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.Unknown);

                        dynamic? wellKnownProvider = DeploymentWellKnownProviderUnknown;
                        // fwdlink1
                        if (index == 0)
                        {
                            string srErrorMessage = Resources.VSMSDEPLOY_MsDeployExceptionFwlink1Message;
                            if (provider?.LastIndexOf("sql", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                srErrorMessage = Resources.VSMSDEPLOY_MsDeployExceptionFwlink1SQLMessage;
                            }
                            else
                            {
                                try
                                {
                                    if (provider is not null)
                                    {
                                        wellKnownProvider = MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValueIgnoreCase(MSDeploy.TypeName.DeploymentWellKnownProvider, provider);
                                    }
                                }
                                catch
                                {
                                    // don't cause the failure;
                                }

                                if (wellKnownProvider?.Equals(MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.MetaKey)) ?? false
                                    || wellKnownProvider?.Equals(MSWebDeploymentAssembly.DynamicAssembly?.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.AppHostConfig)) ?? false)
                                {
                                    srErrorMessage = Resources.VSMSDEPLOY_MsDeployExceptionFwlink1SiteMessage;
                                }
                            }
                            strBuilder.Append(string.Format(CultureInfo.CurrentCulture,srErrorMessage, provider));
                        }
                        else if (index == 1)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_MsDeployExceptionFwlink2Message);
                        }
                        else if (index == 2)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_MsDeployExceptionFwlink3Message);
                        }
                        else if (index == 3)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_MsDeployExceptionFwlink4Message);
                        }
                        else
                        {
                            Debug.Assert(false, "fwdlink5 and above is not implemented");
                        }
                    }
                }
#endif
            }

            if (e.InnerException == null)
            {
                strBuilder.Append(e.Message);
                Log.LogError(string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_FailedWithException, strBuilder.ToString()));
            }
            else
            {
                // Dev10 bug we sometime need better error message to show user what do do
                Exception? currentException = e;
                while (currentException != null)
                {
                    strBuilder.Append(Environment.NewLine);
                    strBuilder.Append(currentException.Message);
                    currentException = currentException.InnerException;
                }
                Log.LogError(string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_FailedWithExceptionWithDetail, e.Message, strBuilder.ToString()));
            }
            strBuilder.Append(Environment.NewLine);
            strBuilder.Append(e.StackTrace);
            Log.LogMessage(Framework.MessageImportance.Low, strBuilder.ToString());
        }

        public static IList<Framework.ITaskItem> SortTaskItemsByPriority(Framework.ITaskItem[]? taskItems)
        {
            int count = taskItems != null ? taskItems.GetLength(0) : 0;
            SortedList<KeyValuePair<int, int>, Framework.ITaskItem> sortedList = new(count, ParameterTaskComparer);

            for (int i = 0; i < count; i++)
            {
                if (taskItems is not null)
                {
                    Framework.ITaskItem iTaskItem = taskItems[i];
                    string priority = iTaskItem.GetMetadata("Priority");
                    int iPriority = string.IsNullOrEmpty(priority) ? 0 : Convert.ToInt32(priority, CultureInfo.InvariantCulture);
                    sortedList.Add(new KeyValuePair<int, int>(iPriority, i), iTaskItem);
                }
            }
            return sortedList.Values;
        }

        internal class PriorityIndexComparer : IComparer<KeyValuePair<int, int>>
        {
            public int Compare(KeyValuePair<int, int> x, KeyValuePair<int, int> y)
            {
                if (x.Key == y.Key)
                {
                    return x.Value - y.Value;
                }
                else
                {
                    return x.Key - y.Key;
                }
            }
        }

        public static string StripOffTrailingSlashes(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                while (str.EndsWith("\\", StringComparison.Ordinal) || str.EndsWith("/", StringComparison.Ordinal))
                    str = str.Substring(0, str.Length - 1);

            }
            return str;
        }

        public static string EnsureTrailingSlashes(string rootPath, char slash)
        {
            string directorySeparator = new(slash, 1);
            string rootPathWithSlash = string.Concat(rootPath, rootPath.EndsWith(directorySeparator, StringComparison.Ordinal) ? string.Empty : directorySeparator);
            return rootPathWithSlash;
        }

        public static string? GetFilePathResolution(string? source, string sourceRootPath)
        {
            if (source is null || Path.IsPathRooted(source) || string.IsNullOrEmpty(sourceRootPath))
                return source;
            else
                return Path.Combine(sourceRootPath, source);
        }

        /// <summary>
        /// Utility to generate the Ipv6 string address to match with the ServerBinding string
        /// Ipv6 need the have 
        /// </summary>
        /// <param name="iPAddress"></param>
        /// <returns></returns>
        internal static string? GetIPAddressString(System.Net.IPAddress iPAddress)
        {
            if (iPAddress != null)
            {
                if (iPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return iPAddress.ToString();
                else if (iPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    StringBuilder stringBuilder = CleanStringBuilder;
                    stringBuilder.Append("[");
                    stringBuilder.Append(iPAddress.ToString());
                    stringBuilder.Append("]");
                    return stringBuilder.ToString();
                }
            }
            return null;
        }

        /// <summary>
        /// Utility function to match the IPAddress with the string in IIS ServerBinding's IPAddress
        /// </summary>
        /// <param name="IISBindingIPString"></param>
        /// <param name="iPAddresses"></param>
        /// <returns></returns>
        internal static bool MatchOneOfIPAddress(string IISBindingIPString, System.Net.IPAddress[] iPAddresses)
        {
            if (!string.IsNullOrEmpty(IISBindingIPString))
            {
                if (IISBindingIPString.Trim() == "*")
                    return true;

                foreach (System.Net.IPAddress iPAddress in iPAddresses)
                {
                    if (string.Compare(GetIPAddressString(iPAddress), IISBindingIPString, StringComparison.OrdinalIgnoreCase) == 0)
                        return true;
                }
            }
            return false;
        }

        internal static void SetupMSWebDeployDynamicAssemblies(string? strVersionsToTry, Task task)
        {
            // Mark the assembly version.
            // System.Version version1_1 = new System.Version("7.1");
            Dictionary<string, string> versionsList = new();
            if (strVersionsToTry is not null && strVersionsToTry.Length != 0)
            {
                foreach (string str in strVersionsToTry.Split(';'))
                {
                    versionsList[str] = str;
                }
            }

            const string MSDeploymentDllFallback = "9.0";
            versionsList[MSDeploymentDllFallback] = MSDeploymentDllFallback;

            Version[] versionArray = versionsList.Values.Select(p => new Version(p)).ToArray();
            Array.Sort(versionArray);

            for (int i = versionArray.GetLength(0) - 1; i >= 0; i--)
            {
                Version version = versionArray[i];
                try
                {
                    MSWebDeploymentAssembly.SetVersion(version);

                    Version webDelegationAssemblyVersion = version;
#if NET472
                    if (MSWebDeploymentAssembly.DynamicAssembly != null && MSWebDeploymentAssembly.DynamicAssembly.Assembly != null)
                    {
                        foreach (AssemblyName assemblyName in MSWebDeploymentAssembly.DynamicAssembly.Assembly.GetReferencedAssemblies())
                        {
                            if (string.Compare(assemblyName.Name, 0 ,  MSWebDelegationAssembly.AssemblyName, 0, MSWebDelegationAssembly.AssemblyName.Length, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                webDelegationAssemblyVersion = assemblyName.Version;
                                break;
                            }
                        }
                    }
#endif
                    MSWebDelegationAssembly.SetVersion(webDelegationAssemblyVersion);
                    task.Log.LogMessage(Framework.MessageImportance.Low, string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_MSDEPLOYVERSIONLOAD, task.ToString(), MSWebDeploymentAssembly.DynamicAssembly?.AssemblyFullName));
                    task.Log.LogMessage(Framework.MessageImportance.Low, string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_MSDEPLOYVERSIONLOAD, task.ToString(), MSWebDelegationAssembly.DynamicAssembly?.AssemblyFullName));
                    return;
                }
                catch (Exception e)
                {
                    task.Log.LogMessage(Framework.MessageImportance.Low, string.Format(CultureInfo.CurrentCulture, Resources.BUILDTASK_FailedToLoadThisVersionMsDeployTryingTheNext, versionArray[i], e.Message));
                }
            }
            // if it not return by now, it is definite a error
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_MSDEPLOYASSEMBLYLOAD_FAIL, task.ToString()));
        }

        public static string? EscapeTextForMSBuildVariable(string? text)
        {
            if (!string.IsNullOrEmpty(text) && text?.IndexOfAny(@"$%".ToArray()) >= 0)
            {
                StringBuilder stringBuilder = new(text.Length * 2);
                char[] chars = text.ToCharArray();
                int i = 0;
                for (i = 0; i < chars.Count() - 2; i++)
                {
                    char ch = chars[i];
                    char nextch1 = chars[i + 1];
                    char nextch2 = chars[i + 2];
                    bool fAlreadyHandled = false;
                    switch (ch)
                    {
                        case '$':
                            if (nextch1 == '(')
                            {
                                stringBuilder.Append("%24");
                                fAlreadyHandled = true;
                            }
                            break;
                        case '%':
                            if (nextch1 == '(' || ("0123456789ABCDEFabcdef".IndexOf(nextch1) >= 0 && "0123456789ABCDEFabcdef".IndexOf(nextch2) >= 0))
                            {
                                stringBuilder.Append("%25");
                                fAlreadyHandled = true;
                            }
                            break;
                    }
                    if (!fAlreadyHandled)
                    {
                        stringBuilder.Append(ch);
                    }
                }
                for (; i < chars.Count(); i++)
                    stringBuilder.Append(chars[i]);
                return stringBuilder.ToString();
            }
            return text;
        }
        /// <summary>
        /// Given a user agent string, it appends :WTE<version> to it if
        /// the string is not null.
        /// </summary>
        public static string? GetFullUserAgentString(string? userAgent)
        {
#if NET472
            if(string.IsNullOrEmpty(userAgent))
                return null;
            try
            {
                object[] o = typeof(Utility).Assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
                if (o.Length > 0 && o[0] is AssemblyFileVersionAttribute)
                {
                    return string.Concat(userAgent, ":WTE", ((AssemblyFileVersionAttribute)o[0]).Version);
                }
            }
            catch
            {
                Debug.Assert(false, "Error getting WTE version");
            }
#endif
            return userAgent;
        }
    }

    internal static class ItemFilter
    {
        public delegate bool ItemMetadataFilter(Framework.ITaskItem iTaskItem);

        public static bool ItemFilterPipelineMetadata(Framework.ITaskItem item, string metadataName, string metadataValue, bool fIgnoreCase)
        {
#if NET472
            return (string.Compare(item.GetMetadata(metadataName), metadataValue, fIgnoreCase, CultureInfo.InvariantCulture) == 0);
#else
            return (string.Compare(item.GetMetadata(metadataName), metadataValue, fIgnoreCase) == 0);
#endif
        }

        public static bool ItemFilterExcludeTrue(Framework.ITaskItem iTaskItem)
        {
            string metadataName = PipelineMetadata.Exclude.ToString();
            return ItemFilterPipelineMetadata(iTaskItem, metadataName, "true", true);
        }
    }
}
