// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class GeneratePortNumberConfig : BaseMacroConfig<GeneratePortNumberMacro, GeneratePortNumberConfig>
    {
        internal const int LowPortDefault = 1024;
        internal const int HighPortDefault = 65535;

        private static readonly object LockObj = new();

        private static readonly HashSet<int> UnsafePorts = new()
        {
                    2049, // nfs
                    3659, // apple-sasl / PasswordServer
                    4045, // lockd
                    6000, // X11
                    6665, // Alternate IRC [Apple addition]
                    6666, // Alternate IRC [Apple addition]
                    6667, // Standard IRC [Apple addition]
                    6668, // Alternate IRC [Apple addition]
                    6669, // Alternate IRC [Apple addition]
        };

        private static readonly HashSet<int> AllocatedPorts = new();

        internal GeneratePortNumberConfig(GeneratePortNumberMacro macro, string variableName, string? dataType, int fallback, int low, int high)
             : base(macro, variableName, dataType)
        {
            Fallback = fallback;
            Low = low;
            High = high;
            Port = AllocatePort(low, high, fallback);
        }

        internal GeneratePortNumberConfig(GeneratePortNumberMacro macro, IGeneratedSymbolConfig generatedSymbolConfig)
            : base(macro, generatedSymbolConfig.VariableName, generatedSymbolConfig.DataType)
        {
            int low = GetOptionalParameterValue(generatedSymbolConfig, nameof(Low), ConvertJTokenToInt, LowPortDefault);
            int high = GetOptionalParameterValue(generatedSymbolConfig, nameof(High), ConvertJTokenToInt, HighPortDefault);
            if (low < LowPortDefault)
            {
                low = LowPortDefault;
            }

            if (high > HighPortDefault)
            {
                high = HighPortDefault;
            }

            if (low > high)
            {
                low = LowPortDefault;
                high = HighPortDefault;
            }

            int fallback = GetOptionalParameterValue(generatedSymbolConfig, "fallback", ConvertJTokenToInt, 0);

            Fallback = fallback;
            Low = low;
            High = high;
            Port = AllocatePort(low, high, fallback);
        }

        internal int Port { get; }

        internal int Low { get; }

        internal int High { get; }

        internal int Fallback { get; }

        private static int AllocatePort(int low, int high, int fallback = 0)
        {
            int startPort = CryptoRandom.NextInt(low, high);

            for (int testPort = startPort; testPort <= high; testPort++)
            {
                if (TryAllocatePort(testPort, out int port))
                {
                    return port;
                }
            }

            for (int testPort = low; testPort < startPort; testPort++)
            {
                if (TryAllocatePort(testPort, out int port))
                {
                    return port;
                }
            }
            return fallback;
        }

        private static bool TryAllocatePort(int testPort, out int finalPort)
        {
            Socket? testSocket = null;
            finalPort = 0;

            if (UnsafePorts.Contains(testPort))
            {
                return false;
            }
            lock (LockObj)
            {
                if (AllocatedPorts.Contains(testPort))
                {
                    return false;
                }

                try
                {
                    if (Socket.OSSupportsIPv4)
                    {
                        testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    }
                    else if (Socket.OSSupportsIPv6)
                    {
                        testSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                    }

                    if (testSocket is null)
                    {
                        return false;
                    }
                    IPEndPoint endPoint = new(testSocket.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, testPort);
                    testSocket.Bind(endPoint);
                    finalPort = ((IPEndPoint)testSocket.LocalEndPoint).Port;
                    if (testPort != finalPort)
                    {
                        return false;
                    }
                    AllocatedPorts.Add(testPort);
                    return true;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    testSocket?.Dispose();
                }
            }
        }
    }
}
