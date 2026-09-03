// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;
using Collections = System.Collections;
using Diagnostics = System.Diagnostics;
using Utilities = Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    /// <summary>
    /// WrapperClass for Microsoft.Web.Deployment
    /// </summary>
    internal class MSWebDeploymentAssembly : DynamicAssembly
    {
        public MSWebDeploymentAssembly(Version verToLoad) :
            base(AssemblyName, verToLoad, "31bf3856ad364e35")
        {
        }

        public static string AssemblyName { get { return "Microsoft.Web.Deployment"; } }
        public static MSWebDeploymentAssembly? DynamicAssembly { get; set; }
        public static void SetVersion(Version version)
        {
            if (DynamicAssembly == null || DynamicAssembly.Version != version)
            {
                DynamicAssembly = new MSWebDeploymentAssembly(version);
            }
        }

        /// <summary>
        /// Utility function to help out on getting Deployment collection's tryGetMethod
        /// </summary>
        /// <param name="deploymentCollection"></param>
        /// <param name="name"></param>
        /// <param name="foundObject"></param>
        /// <returns></returns>
        public static bool DeploymentTryGetValueForEach(dynamic deploymentCollection, string name, out dynamic? foundObject)
        {
            foundObject = null;
            if (deploymentCollection != null)
            {
                foreach (dynamic item in deploymentCollection)
                {
                    if (string.Compare(name, item.Name.ToString(), StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        foundObject = item;
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool DeploymentTryGetValueContains(dynamic deploymentCollection, string name, out dynamic? foundObject)
        {
            foundObject = null;
            if (deploymentCollection != null)
            {
                if (deploymentCollection.Contains(name))
                {
                    foundObject = deploymentCollection[name];
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// WrapperClass for Microsoft.Web.Delegation
    /// </summary>
    internal class MSWebDelegationAssembly : DynamicAssembly
    {
        public MSWebDelegationAssembly(Version verToLoad) :
            base(AssemblyName, verToLoad, "31bf3856ad364e35")
        {
        }

        public static string AssemblyName { get { return "Microsoft.Web.Delegation"; } }

        public static MSWebDelegationAssembly? DynamicAssembly { get; set; }
        public static void SetVersion(Version version)
        {
            if (DynamicAssembly == null || DynamicAssembly.Version != version)
            {
                DynamicAssembly = new MSWebDelegationAssembly(version);
            }
        }
    }

    // Microsoft.Web.Delegation

    ///--------------------------------------------------------------------
    enum DeployStatus
    {
        ReadyToDeploy,
        Deploying,
        DeployFinished,
        DeployAbandoned,
        DeployFailed
    }

    /// <summary>
    /// Encapsulate the process of interacting with MSDeploy
    /// </summary>
    abstract class BaseMSDeployDriver
    {
        protected VSMSDeployObject _dest;
        protected VSMSDeployObject _src;
        protected IVSMSDeployHost _host;

        protected /*VSMSDeploySyncOption*/ dynamic? _option;
        protected bool _isCancelOperation = false;
        protected string? _cancelMessage;

        public string TaskName
        {
            get
            {
                return (_host != null) ? _host.TaskName : string.Empty;
            }
        }

        public string? HighImportanceEventTypes
        {
            get;
            set;
        }

        /// <summary>
        /// Boolean to cancel the operation
        /// (TODO: in RTM, use thread synchronization to protect the entry(though not absolutely necessary.
        /// Need consider perf hit incurred though as msdeploy's callback will reference the value frequently)
        /// </summary>
        public bool IsCancelOperation
        {
            get { return _isCancelOperation; }
            set
            {
                _isCancelOperation = value;
                if (!_isCancelOperation)
                    CancelMessage = null; // reset error age
            }
        }

        public string? CancelMessage
        {
            get { return _cancelMessage; }
            set { _cancelMessage = value; }
        }

        /// <summary>
        /// called by the msdeploy to cancel the operation
        /// </summary>
        /// <returns></returns>
        private bool CancelCallback()
        {
            return IsCancelOperation;
        }

        protected /*VSMSDeploySyncOption*/ dynamic? CreateOptionIfNeeded()
        {
            if (_option == null)
            {
                object? option = MSWebDeploymentAssembly.DynamicAssembly?.CreateObject("Microsoft.Web.Deployment.DeploymentSyncOptions");
#if NET472
                Type? deploymentCancelCallbackType = MSWebDeploymentAssembly.DynamicAssembly?.GetType("Microsoft.Web.Deployment.DeploymentCancelCallback");
                object cancelCallbackDelegate = Delegate.CreateDelegate(deploymentCancelCallbackType, this, "CancelCallback");

                Utility.SetDynamicProperty(option, "CancelCallback", cancelCallbackDelegate);

                // dynamic doesn't work with delegate. it complain on explicit cast needed from object -> DelegateType :(
                // _option.CancelCallback = cancelCallbackDelegate;
#endif
                _option = option;
            }
            return _option;
        }

#if NET472
        private Dictionary<string, MessageImportance>? _highImportanceEventTypes = null;
        private Dictionary<string, MessageImportance> GetHighImportanceEventTypes()
        {
            if (_highImportanceEventTypes == null)
            {
                _highImportanceEventTypes = new Dictionary<string, MessageImportance>(StringComparer.InvariantCultureIgnoreCase); ;
                if (HighImportanceEventTypes is not null && HighImportanceEventTypes.Length != 0)
                {
                    string[] typeNames = HighImportanceEventTypes.Split(new char[] { ';' }); // follow msbuild convention
                    foreach (string typeName in typeNames)
                    {
                        _highImportanceEventTypes.Add(typeName, MessageImportance.High);
                    }
                }
            }
            return _highImportanceEventTypes;
        }
#endif
        void TraceEventHandlerDynamic(object sender, dynamic e)
        {
            // throw new System.NotImplementedException();
            string msg = e.Message;
            Diagnostics.Trace.WriteLine("MSDeploy TraceEvent Handler is called with " + msg);
#if NET472
            LogTrace(e, GetHighImportanceEventTypes());
#endif
            //try
            //{
            //    LogTrace(e);
            //}
            //catch (Framework.LoggerException loggerException)
            //{
            //    System.OperationCanceledException operationCanceledException
            //        = loggerException.InnerException as System.OperationCanceledException;
            //    if (operationCanceledException != null)
            //    {
            //        // eat this exception and set the args
            //        // Logger is the one throw this exception. we should not log again.
            //        // _option.CancelCallback();
            //        IsCancelOperation = true;
            //        CancelMessage = operationCanceledException.Message;
            //    }
            //    else
            //    {
            //        throw; // rethrow if this is not a OperationCancelException
            //    }
            //}
        }

        /// <summary>
        /// Using MSDeploy API to invoke MSDeploy
        /// </summary>
        protected void InvokeMSdeploySync()
        {
            /*VSMSDeploySyncOption*/
            dynamic? option = CreateOptionIfNeeded();
            IsCancelOperation = false;

            _host.PopulateOptions(option);

            // you can reuse traceEventHandler if you know the function signature is the same
            Delegate traceEventHandler = DynamicAssembly.AddEventDeferHandler(
                _src.BaseOptions,
                "Trace",
                new DynamicAssembly.EventHandlerDynamicDelegate(TraceEventHandlerDynamic));
            DynamicAssembly.AddEventHandler(_dest.BaseOptions, "Trace", traceEventHandler);

            _host.UpdateDeploymentBaseOptions(_src, _dest);

            _src.SyncTo(_dest, option, _host);

            _host.ClearDeploymentBaseOptions(_src, _dest);

            DynamicAssembly.RemoveEventHandler(_src.BaseOptions, "Trace", traceEventHandler);
            DynamicAssembly.RemoveEventHandler(_dest.BaseOptions, "Trace", traceEventHandler);

            _src.ResetBaseOptions();
            _dest.ResetBaseOptions();

        }

        /// <summary>
        /// The end to end process to invoke MSDeploy
        /// </summary>
        /// <returns></returns>
        public void SyncThruMSDeploy()
        {
            BeforeSync();
            StartSync();
            WaitForDone();
            AfterSync();
        }

        /// <summary>
        /// Encapsulate the things be done before invoke MSDeploy
        /// </summary>
        protected abstract void BeforeSync();

        /// <summary>
        /// Encapsulate the approach to invoke the MSDeploy (same thread or in a separate thread; ui or without ui)
        /// </summary>
        protected abstract void StartSync();

        /// <summary>
        /// Encapsulate the approach to wait for the MSDeploy done
        /// </summary>
        protected abstract void WaitForDone();

        /// <summary>
        /// Encapsulate how to report the Trace information
        /// </summary>
        /// <param name="e"></param>
        // abstract protected void LogTrace(Deployment.DeploymentTraceEventArgs e);

        protected abstract void LogTrace(dynamic e, IDictionary<string, MessageImportance> customTypeLoging);

        /// <summary>
        /// Encapsulate the things to be done after the deploy is done
        /// </summary>
        protected abstract void AfterSync();

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dest"></param>
        protected BaseMSDeployDriver(VSMSDeployObject src, VSMSDeployObject dest, IVSMSDeployHost host)
        {
            _src = src;
            _dest = dest;
            _host = host;
        }

        public static BaseMSDeployDriver CreateBaseMSDeployDriver(
            VSMSDeployObject src,
            VSMSDeployObject dest,
            IVSMSDeployHost host)
        {
            BaseMSDeployDriver bmd;
            bmd = new VSMSDeployDriverInCmd(src, dest, host);
            return bmd;
        }
    }

    /// <summary>
    /// We create CustomBuildWithPropertiesEventArgs is for the purpose of logging verious information
    /// in a IDictionary such that the MBuild handler can handle generically.
    /// </summary>
#if NET472
    [Serializable]
#endif
    public class CustomBuildWithPropertiesEventArgs : CustomBuildEventArgs, Collections.IDictionary
    {
        public CustomBuildWithPropertiesEventArgs() : base() { }
        public CustomBuildWithPropertiesEventArgs(string msg, string keyword, string senderName)
            : base(msg, keyword, senderName)
        {
        }

        Collections.Specialized.HybridDictionary m_hybridDictionary = new(10);
        #region IDictionary Members 
        // Delegate everything to m_hybridDictionary

        public void Add(object? key, object? value)
        {
            if (key is not null)
            {
                m_hybridDictionary.Add(key, value);
            }
        }

        public void Clear()
        {
            m_hybridDictionary.Clear();
        }

        public bool Contains(object key)
        {
            return m_hybridDictionary.Contains(key);
        }

        public Collections.IDictionaryEnumerator GetEnumerator()
        {
            return m_hybridDictionary.GetEnumerator();
        }

        public bool IsFixedSize
        {
            get { return m_hybridDictionary.IsFixedSize; }
        }

        public bool IsReadOnly
        {
            get { return m_hybridDictionary.IsReadOnly; }
        }

        public Collections.ICollection Keys
        {
            get { return m_hybridDictionary.Keys; }
        }

        public void Remove(object key)
        {
            m_hybridDictionary.Remove(key);
        }

        public Collections.ICollection Values
        {
            get { return m_hybridDictionary.Values; }
        }

        public object? this[object key]
        {
            get { return m_hybridDictionary[key]; }
            set { m_hybridDictionary[key] = value; }
        }

        #endregion

        #region ICollection Members

        public void CopyTo(Array array, int index)
        {
            m_hybridDictionary.CopyTo(array, index);
        }

        public int Count
        {
            get { return m_hybridDictionary.Count; }
        }

        public bool IsSynchronized
        {
            get { return m_hybridDictionary.IsSynchronized; }
        }

        public object SyncRoot
        {
            get { return m_hybridDictionary.SyncRoot; }
        }

        #endregion

        #region IEnumerable Members

        Collections.IEnumerator Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }

    /// <summary>
    /// Deploy through msbuild in command line
    /// </summary>
    class VSMSDeployDriverInCmd : BaseMSDeployDriver
    {
        protected override void BeforeSync()
        {
            string strMsg = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_Start, _src.ToString(), _dest.ToString());
            _host.Log.LogMessage(strMsg);
        }

        // Utility function to log all public instance property to CustomerBuildEventArgs 
        private static void AddAllPropertiesToCustomBuildWithPropertyEventArgs(ExtendedCustomBuildEventArgs cbpEventArg, object obj)
        {
#if NET472
            if (obj != null)
            {
                Type thisType = obj.GetType();
                if (cbpEventArg.ExtendedMetadata is not null)
                {
                    cbpEventArg.ExtendedMetadata["ArgumentType"] = thisType.ToString();
                }
                System.Reflection.MemberInfo[] arrayMemberInfo = thisType.FindMembers(System.Reflection.MemberTypes.Property, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, null);
                if (arrayMemberInfo != null)
                {
                    foreach (System.Reflection.MemberInfo memberInfo in arrayMemberInfo)
                    {
                        object val = thisType.InvokeMember(memberInfo.Name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.GetProperty, null, obj, null, System.Globalization.CultureInfo.InvariantCulture);
                        if (val is not null && cbpEventArg.ExtendedMetadata is not null)
                        {
                            cbpEventArg.ExtendedMetadata[memberInfo.Name] = val.ToString();
                        }
                    }
                }
            }
#endif
        }

        protected override void LogTrace(dynamic args, IDictionary<string, MessageImportance> customTypeLoging)
        {
            string strMsg = args.Message;
            string strEventType = "Trace";
            MessageImportance messageImportance = MessageImportance.Low;

            Type argsT = args.GetType();
            if (Utility.IsType(argsT, MSWebDeploymentAssembly.DynamicAssembly?.GetType("Microsoft.Web.Deployment.DeploymentFileSerializationEventArgs")) ||
                Utility.IsType(argsT, MSWebDeploymentAssembly.DynamicAssembly?.GetType("Microsoft.Web.Deployment.DeploymentPackageSerializationEventArgs")) ||
                Utility.IsType(argsT, MSWebDeploymentAssembly.DynamicAssembly?.GetType("Microsoft.Web.Deployment.DeploymentObjectChangedEventArgs")) ||
                Utility.IsType(argsT, MSWebDeploymentAssembly.DynamicAssembly?.GetType("Microsoft.Web.Deployment.DeploymentSyncParameterEventArgs")))
            {
                //promote those message only for those event
                strEventType = "Action";
                messageImportance = MessageImportance.High;
            }
            else if (customTypeLoging != null && customTypeLoging.ContainsKey(argsT.Name))
            {
                strEventType = "Trace";
                messageImportance = customTypeLoging[argsT.Name];
            }

            if (!string.IsNullOrEmpty(strMsg))
            {
                Diagnostics.TraceLevel level = (Diagnostics.TraceLevel)Enum.ToObject(typeof(Diagnostics.TraceLevel), args.EventLevel);
                switch (level)
                {
                    case Diagnostics.TraceLevel.Off:
                        break;
                    case Diagnostics.TraceLevel.Error:
                        _host.Log.LogError(strMsg);
                        break;
                    case Diagnostics.TraceLevel.Warning:
                        _host.Log.LogWarning(strMsg);
                        break;
                    default: // Is Warning is a Normal message
                        _host.Log.LogMessageFromText(strMsg, messageImportance);
                        break;

                }
            }

            // additionally we fire the Custom event for the detail information
            var customBuildWithPropertiesEventArg = new ExtendedCustomBuildEventArgs(args.GetType().ToString(), args.Message, null, TaskName)
            {
                ExtendedMetadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    { "TaskName", TaskName },
                    { "EventType", strEventType }
                }
            };

            AddAllPropertiesToCustomBuildWithPropertyEventArgs(customBuildWithPropertiesEventArg, args);
            _host.BuildEngine.LogCustomEvent(customBuildWithPropertiesEventArg);
        }

        /// <summary>
        /// Invoke MSDeploy
        /// </summary>
        protected override void StartSync()
        {
            InvokeMSdeploySync();
        }

        /// <summary>
        /// Wait forever if we are in the command line
        /// </summary>
        protected override void WaitForDone() { }

        /// <summary>
        /// Log status after the deploy is done
        /// </summary>
        protected override void AfterSync()
        {
            string strMsg = Resources.VSMSDEPLOY_Succeeded;
            _host.Log.LogMessage(strMsg);
        }

        /// <summary>
        /// construct
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dest"></param>
        /// <param name="log"></param>
        internal VSMSDeployDriverInCmd(VSMSDeployObject src, VSMSDeployObject dest, IVSMSDeployHost host)
            : base(src, dest, host)
        {
            if (host.GetProperty("HighImportanceEventTypes") != null)
                HighImportanceEventTypes = host.GetProperty("HighImportanceEventTypes")?.ToString();
        }
    }

    /// <summary>
    /// MSBuild Task VSMSDeploy to call the object through UI or not
    /// </summary>
    public class VSMSDeploy : Task, IVSMSDeployHost, ICancelableTask
    {
        string? _disableLink;
        string? _enableLink;
        private string? _disableSkipDirective;
        private string? _enableSkipDirective;

        bool _result = false;
        bool _whatIf = false;
        string? _deploymentTraceLevel;
        bool _useCheckSum = false;
        private int m_retryAttempts = -1;
        private int m_retryInterval = -1;

        bool _allowUntrustedCert;
        bool _skipExtraFilesOnServer = false;

        private ITaskItem[]? m_sourceITaskItem = null;
        private ITaskItem[]? m_destITaskItem = null;
        private ITaskItem[]? m_replaceRuleItemsITaskItem = null;
        private ITaskItem[]? m_skipRuleItemsITaskItem = null;
        private ITaskItem[]? m_declareParameterItems = null;
        private ITaskItem[]? m_importDeclareParametersItems = null;
        private ITaskItem[]? m_simpleSetParameterItems = null;
        private ITaskItem[]? m_importSetParametersItems = null;
        private ITaskItem[]? m_setParameterItems = null;

        private BaseMSDeployDriver? m_msdeployDriver = null;

        [Required]
        public ITaskItem[]? Source
        {
            get { return m_sourceITaskItem; }
            set { m_sourceITaskItem = value; }
        }

        public string? HighImportanceEventTypes
        {
            get;
            set;
        }

        public ITaskItem[]? Destination
        {
            get { return m_destITaskItem; }
            set { m_destITaskItem = value; }
        }

        public bool AllowUntrustedCertificate
        {
            get { return _allowUntrustedCert; }
            set { _allowUntrustedCert = value; }
        }

        public bool SkipExtraFilesOnServer
        {
            get { return _skipExtraFilesOnServer; }
            set { _skipExtraFilesOnServer = value; }
        }

        public bool WhatIf
        {
            get { return _whatIf; }
            set { _whatIf = value; }
        }

        public string? DeploymentTraceLevel
        {
            get { return _deploymentTraceLevel; }
            set { _deploymentTraceLevel = value; }
        }

        public bool UseChecksum
        {
            get { return _useCheckSum; }
            set { _useCheckSum = value; }
        }

        //Sync result: Succeed or Fail
        [Output]
        public bool Result
        {
            get { return _result; }
            set { _result = value; }
        }

        /// <summary>
        /// Disable Link is a list of disable provider
        /// </summary>
        public string? DisableLink
        {
            get { return _disableLink; }
            set { _disableLink = value; }
        }

        public string? EnableLink
        {
            get { return _enableLink; }
            set { _enableLink = value; }
        }

        public string? DisableSkipDirective
        {
            get { return _disableSkipDirective; }
            set { _disableSkipDirective = value; }
        }

        public string? EnableSkipDirective
        {
            get { return _enableSkipDirective; }
            set { _enableSkipDirective = value; }
        }

        public int RetryAttempts
        {
            get { return m_retryAttempts; }
            set { m_retryAttempts = value; }
        }

        public int RetryInterval
        {
            get { return m_retryInterval; }
            set { m_retryInterval = value; }
        }

        public ITaskItem[]? ReplaceRuleItems
        {
            get { return m_replaceRuleItemsITaskItem; }
            set { m_replaceRuleItemsITaskItem = value; }
        }

        public ITaskItem[]? SkipRuleItems
        {
            get { return m_skipRuleItemsITaskItem; }
            set { m_skipRuleItemsITaskItem = value; }
        }

        public ITaskItem[]? DeclareParameterItems
        {
            get { return m_declareParameterItems; }
            set { m_declareParameterItems = value; }
        }

        public bool OptimisticParameterDefaultValue { get; set; }

        public ITaskItem[]? ImportDeclareParametersItems
        {
            get { return m_importDeclareParametersItems; }
            set { m_importDeclareParametersItems = value; }
        }

        public ITaskItem[]? SimpleSetParameterItems
        {
            get { return m_simpleSetParameterItems; }
            set { m_simpleSetParameterItems = value; }
        }

        public ITaskItem[]? ImportSetParametersItems
        {
            get { return m_importSetParametersItems; }
            set { m_importSetParametersItems = value; }
        }

        public ITaskItem[]? SetParameterItems
        {
            get { return m_setParameterItems; }
            set { m_setParameterItems = value; }
        }

        public bool EnableMSDeployBackup { get; set; }

        public bool EnableMSDeployAppOffline { get; set; }

        public bool EnableMSDeployWebConfigEncryptRule { get; set; }

        private string? _userAgent;
        public string? UserAgent
        {
            get { return _userAgent; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _userAgent = Utility.GetFullUserAgentString(value);
                }
            }
        }

        public ITaskItem[]? AdditionalDestinationProviderOptions { get; set; }

        public string? MSDeployVersionsToTry
        {
            get;
            set;
        }

        private bool AllowUntrustedCertCallback(object sp,
                System.Security.Cryptography.X509Certificates.X509Certificate cert,
                System.Security.Cryptography.X509Certificates.X509Chain chain,
                System.Net.Security.SslPolicyErrors problem)
        {
            if (AllowUntrustedCertificate)
            {
                return true;
            }

            return false;
        }
        private void SetupPublishRelatedProperties(ref VSMSDeployObject dest)
        {
#if NET472
            if (AllowUntrustedCertificate) 
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback
                         += new System.Net.Security.RemoteCertificateValidationCallback(AllowUntrustedCertCallback);
            }
#endif
        }

        public override bool Execute()
        {
            Result = false;

            try
            {
                Utility.SetupMSWebDeployDynamicAssemblies(MSDeployVersionsToTry, this);
            }
            catch (Exception exception)
            {
                Log.LogErrorFromException(exception);
                return false; // failed the task
            }

            string? errorMessage = null;
            if (!Utility.CheckMSDeploymentVersion(Log, out errorMessage))
                return false;

            VSMSDeployObject? src = null;
            VSMSDeployObject? dest = null;

            if (Source == null || Source.GetLength(0) != 1)
            {
                Log.LogError("Source must be 1 item");
                return false;
            }
            else
            {
                src = VSMSDeployObjectFactory.CreateVSMSDeployObject(Source[0]);
            }

            if (Destination == null || Destination.GetLength(0) != 1)
            {
                Log.LogError("Destination must be 1 item");
                return false;
            }
            else
            {
                dest = VSMSDeployObjectFactory.CreateVSMSDeployObject(Destination[0]);
                VSHostObject hostObj = new(HostObject as IEnumerable<ITaskItem>);
                string username, password;
                if (hostObj.ExtractCredentials(out username, out password))
                {
                    dest.UserName = username;
                    dest.Password = password;
                }
            }

            //$Todo, Should we split the Disable Link to two set of setting, one for source, one for destination
            src.DisableLinks = DisableLink ?? string.Empty;
            dest.DisableLinks = DisableLink ?? string.Empty;
            src.EnableLinks = EnableLink ?? string.Empty;
            dest.EnableLinks = EnableLink ?? string.Empty;
            if (RetryAttempts >= 0)
            {
                src.RetryAttempts = RetryAttempts;
                dest.RetryAttempts = RetryAttempts;
            }
            if (RetryInterval >= 0)
            {
                src.RetryInterval = RetryInterval;
                dest.RetryInterval = RetryInterval;
            }
            dest.UserAgent = UserAgent;

            SetupPublishRelatedProperties(ref dest);

            // change to use when we have MSDeploy implement the dispose method 
            BaseMSDeployDriver driver = BaseMSDeployDriver.CreateBaseMSDeployDriver(src, dest, this);
            m_msdeployDriver = driver;
            try
            {
                driver.SyncThruMSDeploy();
                Result = !driver.IsCancelOperation;
            }
            catch (Exception e)
            {
                if (e is System.Reflection.TargetInvocationException)
                {
                    if (e.InnerException != null)
                        e = e.InnerException;
                }

                Type eType = e.GetType();
                if (Utility.IsType(eType, MSWebDeploymentAssembly.DynamicAssembly?.GetType("Microsoft.Web.Deployment.DeploymentCanceledException")))
                {
                    Log.LogMessageFromText(Resources.VSMSDEPLOY_Canceled, MessageImportance.High);
                }
                else if (Utility.IsType(eType, MSWebDelegationAssembly.DynamicAssembly?.GetType("Microsoft.Web.Deployment.DeploymentException"))
                    || Utility.IsType(eType, MSWebDeploymentAssembly.DynamicAssembly?.GetType("Microsoft.Web.Deployment.DeploymentFatalException")))
                {
                    Utility.LogVsMsDeployException(Log, e);
                }
                else
                {
                    if (!driver.IsCancelOperation)
                        Log.LogError(string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_FailedWithException, e.Message));
                }
            }
            finally
            {
#if NET472

                if (AllowUntrustedCertificate)
                    System.Net.ServicePointManager.ServerCertificateValidationCallback
                        -= new System.Net.Security.RemoteCertificateValidationCallback(AllowUntrustedCertCallback);
#endif
            }

            Utility.MsDeployEndOfExecuteMessage(Result, dest.Provider, dest.Root, Log);
            return Result;
        }

        #region IVSMSDeployHost Members

        string IVsPublishMsBuildTaskHost.TaskName
        {
            get
            {
                return GetType().Name;
            }
        }

        Utilities.TaskLoggingHelper IVsPublishMsBuildTaskHost.Log
        {
            get
            {
                return Log;
            }
        }

        IBuildEngine IVsPublishMsBuildTaskHost.BuildEngine
        {
            get
            {
                return BuildEngine;
            }
        }

        /// <summary>
        /// Sample for skipping directories
        //   <ItemGroup>
        //        <MsDeploySkipRules Include = "SkippingWWWRoot" >
        //            <ObjectName>dirPath</ ObjectName >
        //            <AbsolutePath>wwwroot</ AbsolutePath >
        //        </MsDeploySkipRules>
        //    </ ItemGroup >
        /// </summary>
        void IVSMSDeployHost.UpdateDeploymentBaseOptions(VSMSDeployObject srcVsMsDeployobject, VSMSDeployObject destVsMsDeployobject)
        {
            List<string> enableSkipDirectiveList = MSDeployUtility.ConvertStringIntoList(EnableSkipDirective);
            List<string> disableSkipDirectiveList = MSDeployUtility.ConvertStringIntoList(DisableSkipDirective);

            VSHostObject hostObject = new(HostObject as IEnumerable<ITaskItem>);
            ITaskItem[]? srcSkipItems, destSkipsItems;

            // Add FileSkip rules from Host Object
            hostObject.GetFileSkips(out srcSkipItems, out destSkipsItems);
            Utility.AddSkipDirectiveToBaseOptions(srcVsMsDeployobject.BaseOptions, srcSkipItems, enableSkipDirectiveList, disableSkipDirectiveList, Log);
            Utility.AddSkipDirectiveToBaseOptions(destVsMsDeployobject.BaseOptions, destSkipsItems, enableSkipDirectiveList, disableSkipDirectiveList, Log);

            //Add CustomSkip Rules + AppDataSkipRules
            GetCustomAndAppDataSkips(out srcSkipItems, out destSkipsItems);
            Utility.AddSkipDirectiveToBaseOptions(srcVsMsDeployobject.BaseOptions, srcSkipItems, enableSkipDirectiveList, disableSkipDirectiveList, Log);
            Utility.AddSkipDirectiveToBaseOptions(destVsMsDeployobject.BaseOptions, destSkipsItems, enableSkipDirectiveList, disableSkipDirectiveList, Log);

            if (!string.IsNullOrEmpty(DeploymentTraceLevel))
            {
                Diagnostics.TraceLevel deploymentTraceEventLevel =
                    (Diagnostics.TraceLevel)Enum.Parse(typeof(Diagnostics.TraceLevel), DeploymentTraceLevel, true);
                if (srcVsMsDeployobject.BaseOptions is not null)
                {
                    srcVsMsDeployobject.BaseOptions.TraceLevel = deploymentTraceEventLevel;
                }
                if (destVsMsDeployobject.BaseOptions is not null)
                {
                    destVsMsDeployobject.BaseOptions.TraceLevel = deploymentTraceEventLevel;
                }
            }

            Utility.AddSetParametersFilesVsMsDeployObject(srcVsMsDeployobject, ImportSetParametersItems);
            Utility.AddSimpleSetParametersVsMsDeployObject(srcVsMsDeployobject, SimpleSetParameterItems, OptimisticParameterDefaultValue);
            Utility.AddSetParametersVsMsDeployObject(srcVsMsDeployobject, SetParameterItems, OptimisticParameterDefaultValue);

            AddAdditionalProviderOptions(destVsMsDeployobject);
        }
        private void GetCustomAndAppDataSkips(out ITaskItem[]? srcSkips, out ITaskItem[]? destSkips)
        {
            srcSkips = null;
            destSkips = null;

            if (SkipRuleItems != null)
            {
                IEnumerable<ITaskItem> items;

                items = from item in SkipRuleItems
                        where (string.IsNullOrEmpty(item.GetMetadata(VSMsDeployTaskHostObject.SkipApplyMetadataName)) ||
                               item.GetMetadata(VSMsDeployTaskHostObject.SkipApplyMetadataName) == VSMsDeployTaskHostObject.SourceDeployObject)
                        select item;
                srcSkips = items.ToArray();

                items = from item in SkipRuleItems
                        where (string.IsNullOrEmpty(item.GetMetadata(VSMsDeployTaskHostObject.SkipApplyMetadataName)) ||
                               item.GetMetadata(VSMsDeployTaskHostObject.SkipApplyMetadataName) == VSMsDeployTaskHostObject.DestinationDeployObject)
                        select item;

                destSkips = items.ToArray();
            }
        }

        private void AddAdditionalProviderOptions(VSMSDeployObject destVsMsDeployobject)
        {
            if (AdditionalDestinationProviderOptions != null)
            {
                foreach (ITaskItem item in AdditionalDestinationProviderOptions)
                {
                    if (!string.IsNullOrEmpty(item.ItemSpec))
                    {
                        string settingName = item.GetMetadata("Name");
                        string settingValue = item.GetMetadata("Value");
                        if (!string.IsNullOrEmpty(settingName) && !string.IsNullOrEmpty(settingValue))
                            destVsMsDeployobject.BaseOptions?.AddDefaultProviderSetting(item.ItemSpec, settingName, settingValue);
                    }
                }
            }
        }

        void IVSMSDeployHost.ClearDeploymentBaseOptions(VSMSDeployObject srcVsMsDeployobject, VSMSDeployObject destVsMsDeployobject)
        {
            // Nothing to do here
        }

        void IVSMSDeployHost.PopulateOptions(/*Microsoft.Web.Deployment.DeploymentSyncOptions*/ dynamic option)
        {
            option.WhatIf = WhatIf;
            // Add the replace rules, we should consider doing the same thing for the skip rule
            Utility.AddReplaceRulesToOptions(option.Rules, ReplaceRuleItems);
            Utility.AddImportDeclareParametersFileOptions(option, ImportDeclareParametersItems);
            Utility.AddDeclareParametersToOptions(option, DeclareParameterItems, OptimisticParameterDefaultValue);

            option.UseChecksum = UseChecksum;
            option.DoNotDelete = SkipExtraFilesOnServer;
            if (EnableMSDeployBackup == false)
            {
                // We need to remove the BackupRule to work around bug DevDiv: 478647. We try catch in case
                // the rule isn't there and webdeploy throws. The documentation doesn't say what the exceptions are and the function
                // is void.
                try
                {
                    option.Rules.Remove("BackupRule");
                }
                catch
                {
                }
            }

            if (EnableMSDeployAppOffline)
            {
                AddOptionRule(option, "AppOffline", "Microsoft.Web.Deployment.AppOfflineRuleHandler");
            }

            if (EnableMSDeployWebConfigEncryptRule)
            {
                AddOptionRule(option, "EncryptWebConfig", "Microsoft.Web.Deployment.EncryptWebConfigRuleHandler");
            }
        }

        #endregion

        #region ICancelableTask Members

        public void Cancel()
        {
            try
            {
                if (m_msdeployDriver != null)
                {
                    //[TODO: in RTM make sure we can cancel even "m_msdeployDriver" can be null, meaning vsmsdeploy task has not initialized the deploy driver to sync]
                    //Currently there is a very slim chance that users can't cancel it if the cancel action falls into this time frame 
                    m_msdeployDriver.IsCancelOperation = true;
                }
            }
            catch (Exception ex)
            {
                Diagnostics.Debug.Fail("Exception on ICancelableTask.Cancel being invoked:" + ex.Message);
            }
        }

        #endregion

        public object? GetProperty(string propertyName)
        {
#if NET472
            string lowerName = propertyName.ToLower(System.Globalization.CultureInfo.InvariantCulture);
#else
            string lowerName = propertyName.ToLower();
#endif
            switch (lowerName)
            {
                case "msdeployversionstotry":
                    return MSDeployVersionsToTry;
                case "highimportanceeventtypes":
                    return HighImportanceEventTypes;
                default:
                    break;
            }
            return null;
        }

        public void AddOptionRule(/*Microsoft.Web.Deployment.DeploymentSyncOptions*/ dynamic option, string ruleName, string handlerType)
        {
            bool ruleExists = false;
            try
            {
                object existingRule = option.Rules[ruleName];
                ruleExists = true;
            }
            catch (KeyNotFoundException) { }

            if (!ruleExists)
            {
                dynamic? appOfflineRuleHandler = MSWebDeploymentAssembly.DynamicAssembly?.CreateObject(handlerType, new object[] { });
                if (appOfflineRuleHandler is not null)
                {
                    dynamic? appOfflineRule = MSWebDeploymentAssembly.DynamicAssembly?.CreateObject("Microsoft.Web.Deployment.DeploymentRule",
                        new object[] { ruleName, appOfflineRuleHandler });
                    option.Rules.Add(appOfflineRule);
                }
            }
        }
    }
}
