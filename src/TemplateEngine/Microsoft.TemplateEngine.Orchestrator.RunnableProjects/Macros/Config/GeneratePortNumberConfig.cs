// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class GeneratePortNumberConfig : IMacroConfig
    {
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

        internal GeneratePortNumberConfig(string variableName, string? dataType, int fallback, int low, int high)
        {
            DataType = dataType;
            VariableName = variableName;
            int startPort = CryptoRandom.NextInt(low, high);

            for (int testPort = startPort; testPort <= high; testPort++)
            {
                if (TryAllocatePort(testPort, out Socket? testSocket))
                {
                    Socket = testSocket;
                    Port = ((IPEndPoint)Socket!.LocalEndPoint).Port;
                    return;
                }
            }

            for (int testPort = low; testPort < startPort; testPort++)
            {
                if (TryAllocatePort(testPort, out Socket? testSocket))
                {
                    Socket = testSocket;
                    Port = ((IPEndPoint)Socket!.LocalEndPoint).Port;
                    return;
                }
            }

            Port = fallback;
        }

        public string VariableName { get; }

        public string Type => "port";

        internal string? DataType { get; }

        internal Socket? Socket { get; }

        internal int Port { get; }

        internal int Low { get; }

        internal int High { get; }

        private bool TryAllocatePort(int testPort, out Socket? testSocket)
        {
            testSocket = null;

            if (UnsafePorts.Contains(testPort))
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
            }
            catch
            {
                testSocket?.Dispose();
                return false;
            }

            if (testSocket != null)
            {
                IPEndPoint endPoint = new IPEndPoint(testSocket.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, testPort);

                try
                {
                    testSocket.Bind(endPoint);
                    return true;
                }
                catch
                {
                    testSocket?.Dispose();
                    return false;
                }
            }

            testSocket = null;
            return false;
        }
    }
}
