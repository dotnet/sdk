// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    internal class DynamicAssembly
    {
        public DynamicAssembly(string assemblyName, Version verToLoad, string publicKeyToken)
        {
            AssemblyFullName = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0}, Version={1}.{2}.0.0, Culture=neutral, PublicKeyToken={3}", assemblyName, verToLoad.Major, verToLoad.Minor, publicKeyToken);
#if NET472
            bool isAssemblyLoaded = false;
            try
            {
                Assembly = Assembly.Load(AssemblyFullName);
                isAssemblyLoaded = true;
            }
            catch (FileNotFoundException)
            {
            }

            // if the assembly is not available in the gac, try to load it from the same path as task assembly.
            if (!isAssemblyLoaded)
            {
                Assembly = Assembly.LoadFrom(Path.Combine(TaskAssemblyDirectory, assemblyName+".dll"));
            }
#endif
            Version = verToLoad;
        }

#if NET472
        public static string TaskAssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
#endif
        public DynamicAssembly() { }

        public string? AssemblyFullName { get; set; }
        public Version? Version { get; set; }
        public Assembly? Assembly { get; set; }

        public Type? GetType(string typeName)
        {
            Type? type = Assembly?.GetType(typeName);
            Debug.Assert(type != null);
            return type;
        }

        public virtual Type? TryGetType(string typeName)
        {
            Type? type = Assembly?.GetType(typeName);
            return type;
        }

        public object? GetEnumValue(string enumName, string enumValue)
        {
            Type? enumType = Assembly?.GetType(enumName);
            FieldInfo? enumItem = enumType?.GetField(enumValue);
            object? ret = enumItem?.GetValue(enumType);
            Debug.Assert(ret != null);
            return ret;
        }

        public object? GetEnumValueIgnoreCase(string enumName, string enumValue)
        {
            Type? enumType = Assembly?.GetType(enumName);
            FieldInfo? enumItem = enumType?.GetField(enumValue, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            object? ret = enumItem?.GetValue(enumType);
            Debug.Assert(ret != null);
            return ret;
        }

        public bool TryGetEnumValue(string enumTypeName, string enumStrValue, out object? retValue)
        {
            bool fGetValue = false;
            retValue = null;
            var enumType = GetType(enumTypeName);
            if (enumType is not null)
            {
                retValue = Enum.ToObject(enumType, 0);
            }
            try
            {
                retValue = GetEnumValueIgnoreCase(enumTypeName, enumStrValue);
                fGetValue = true;
            }
            catch
            {
            }
            return fGetValue;
        }

        public object? CreateObject(string typeName)
        {
            return CreateObject(typeName, null);
        }

        public object? CreateObject(string typeName, object[]? arguments)
        {
            object? createdObject = null;
            Type[]? argumentTypes = null;
            if (arguments == null || arguments.GetLength(0) == 0)
            {
                argumentTypes = Type.EmptyTypes;
            }
            else
            {
                argumentTypes = arguments.Select(p => p.GetType()).ToArray();
            }
            Type? typeToConstruct = Assembly?.GetType(typeName);
            ConstructorInfo? constructorInfoObj = typeToConstruct?.GetConstructor(argumentTypes);

            if (constructorInfoObj == null)
            {
                Debug.Assert(false, "DynamicAssembly.CreateObject Failed to get the constructorInfoObject");
            }
            else
            {
                createdObject = constructorInfoObj.Invoke(arguments);
            }
            Debug.Assert(createdObject != null);
            return createdObject;
        }

#if NET472
        public object? CallStaticMethod(string typeName, string methodName, object[] arguments)
        {
            Type? t = GetType(typeName);
            return t?.InvokeMember(methodName, BindingFlags.InvokeMethod, null, t, arguments, System.Globalization.CultureInfo.InvariantCulture);
        }
#endif

        /// <summary>
        /// Support late bind delegate
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public delegate void EventHandlerDynamicDelegate(object sender, dynamic e);
        public delegate void EventHandlerEventArgsDelegate(object sender, EventArgs e);
        internal static Delegate? CreateEventHandlerDelegate<TDelegate>(EventInfo evt, TDelegate d)
        {
            var handlerType = evt.EventHandlerType;
            var eventParams = handlerType?.GetMethod("Invoke")?.GetParameters();

            ParameterExpression[] parameters = eventParams?.Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray() ?? Array.Empty<ParameterExpression>();
            MethodInfo? invokeMethod = d?.GetType().GetMethod("Invoke");
            if (invokeMethod is null)
            {
                return null;
            }

            MethodCallExpression body = Expression.Call(Expression.Constant(d), invokeMethod, parameters);
            var lambda = Expression.Lambda(body, parameters);
            // Diagnostics.Debug.Assert(false, lambda.ToString());
#if NET472
            return Delegate.CreateDelegate(handlerType, lambda.Compile(), "Invoke", false);
#else
            return null;
#endif
        }

        public static Delegate? AddEventDeferHandler(dynamic obj, string eventName, Delegate deferEventHandler)
        {
            EventInfo eventInfo = obj.GetType().GetEvent(eventName);
            Delegate? eventHandler = CreateEventHandlerDelegate(eventInfo, deferEventHandler);
            eventInfo.AddEventHandler(obj, eventHandler);
            return eventHandler;
        }

        public static void AddEventHandler(dynamic obj, string eventName, Delegate eventHandler)
        {
            EventInfo eventInfo = obj.GetType().GetEvent(eventName);
            eventInfo.AddEventHandler(obj, eventHandler);
        }

        public static void RemoveEventHandler(dynamic obj, string eventName, Delegate eventHandler)
        {
            EventInfo eventInfo = obj.GetType().GetEvent(eventName);
            eventInfo.RemoveEventHandler(obj, eventHandler);
        }
    }
}
