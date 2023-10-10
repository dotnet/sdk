// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Sockets;

namespace Microsoft.DotNet.Cli.Package.Tests
{
    public class PortFinder
    {
        private readonly object _lock = new object();
        private readonly HashSet<int> _allocatedPorts = new HashSet<int>();
        private const int DynamicPortStart = 49152;
        private const int DynamicPortEnd = 65535;

        /// <summary>
        /// Allocate a free port.
        /// </summary>
        public int AllocateFreePort()
        {
            lock (_lock)
            {
                for (int port = DynamicPortStart; port <= DynamicPortEnd; port++)
                {
                    if (_allocatedPorts.Contains(port))
                    {
                        continue; // Skip already allocated ports
                    }

                    if (IsPortAvailable(port))
                    {
                        _allocatedPorts.Add(port);
                        return port;
                    }
                }

                throw new Exception("No free ports available!");
            }
        }

        /// <summary>
        /// Release an allocated port.
        /// </summary>
        public void ReleasePort(int port)
        {
            lock (_lock)
            {
                _allocatedPorts.Remove(port);
            }
        }

        /// <summary>
        /// Check if a port is available.
        /// </summary>
        private static bool IsPortAvailable(int port)
        {
            using (var tcpListener = new TcpListener(IPAddress.Loopback, port))
            {
                try
                {
                    tcpListener.Start();
                    return true;
                }
                catch (SocketException)
                {
                    return false; // Port is already in use
                }
                finally
                {
                    tcpListener.Stop();
                }
            }
        }
    }

}
