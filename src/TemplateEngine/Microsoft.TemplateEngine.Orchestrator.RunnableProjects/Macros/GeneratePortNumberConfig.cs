// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class GeneratePortNumberConfig : BaseMacroConfig<GeneratePortNumberMacro, GeneratePortNumberConfig>
    {
        internal const int LowPortDefault = 1024;
        internal const int HighPortDefault = 65535;

        // sources of unsafe ports:
        //   * chrome:  https://chromium.googlesource.com/chromium/src.git/+/refs/heads/master/net/base/port_util.cc#27
        //   * firefox: https://www-archive.mozilla.org/projects/netlib/portbanning#portlist
        //   * safari:  https://github.com/WebKit/WebKit/blob/42f5a93823a7f087a800cd65c6bc0551dbeb55d3/Source/WTF/wtf/URL.cpp#L969
        private static readonly HashSet<int> UnsafePorts = new HashSet<int>()
        {
            1719, // H323 (RAS)
            1720, // H323 (Q931)
            1723, // H323 (H245)
            2049, // NFS
            3659, // apple-sasl / PasswordServer [Apple addition]
            4045, // lockd
            4190, // ManageSieve [Apple addition]
            5060, // SIP
            5061, // SIPS
            6000, // X11
            6566, // SANE
            6665, // Alternate IRC [Apple addition]
            6666, // Alternate IRC [Apple addition]
            6667, // Standard IRC [Apple addition]
            6668, // Alternate IRC [Apple addition]
            6669, // Alternate IRC [Apple addition]
            6679, // Alternate IRC SSL [Apple addition]
            6697, // IRC+SSL [Apple addition]
            10080, // amanda
        };

        internal GeneratePortNumberConfig(GeneratePortNumberMacro macro, string variableName, string? dataType, int fallback, int low, int high)
             : base(macro, variableName, dataType)
        {
            if (low < LowPortDefault)
            {
                throw new ArgumentException($"{nameof(low)} should be greater than {LowPortDefault}.", nameof(low));
            }

            if (high > HighPortDefault)
            {
                throw new ArgumentException($"{nameof(high)} should be less than {HighPortDefault}.", nameof(high));
            }

            if (low > high)
            {
                throw new ArgumentException($"{nameof(low)} should be greater than {nameof(high)}.", nameof(low));
            }

            Fallback = fallback;
            Low = low;
            High = high;
            Port = AllocatePort(low, high, fallback);
        }

        internal GeneratePortNumberConfig(ILogger logger, GeneratePortNumberMacro macro, IGeneratedSymbolConfig generatedSymbolConfig)
            : base(macro, generatedSymbolConfig.VariableName, generatedSymbolConfig.DataType)
        {
            int low = GetOptionalParameterValue(generatedSymbolConfig, "low", ConvertJTokenToInt, LowPortDefault);
            int high = GetOptionalParameterValue(generatedSymbolConfig, "high", ConvertJTokenToInt, HighPortDefault);
            if (low < LowPortDefault)
            {
                logger.LogWarning(LocalizableStrings.GeneratePortNumberConfig_Warning_InvalidLowBound, low, LowPortDefault);
                low = LowPortDefault;
            }

            if (high > HighPortDefault)
            {
                logger.LogWarning(LocalizableStrings.GeneratePortNumberConfig_Warning_InvalidHighBound, high, HighPortDefault);
                high = HighPortDefault;
            }

            if (low > high)
            {
                logger.LogWarning(LocalizableStrings.GeneratePortNumberConfig_Warning_InvalidLowHighBound, low, high, LowPortDefault, HighPortDefault);
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

        private static ConcurrentDictionary<int, int> UnavailablePorts { get; } = new(UnsafePorts.ToDictionary(p => p));

        private static int AllocatePort(int low, int high, int fallback = 0)
        {
            int startPort = CryptoRandom.NextInt(low, high);

            for (int testPort = startPort; testPort <= high; testPort++)
            {
                if (TryAllocatePort(testPort))
                {
                    return testPort;
                }
            }

            for (int testPort = low; testPort < startPort; testPort++)
            {
                if (TryAllocatePort(testPort))
                {
                    return testPort;
                }
            }
            return fallback;
        }

        private static bool TryAllocatePort(int testPort)
        {
            Socket? testSocket = null;
            if (!UnavailablePorts.TryAdd(testPort, testPort))
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
