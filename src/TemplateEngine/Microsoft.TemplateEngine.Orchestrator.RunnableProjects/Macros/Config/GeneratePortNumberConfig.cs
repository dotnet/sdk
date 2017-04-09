using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class GeneratePortNumberConfig : IMacroConfig
    {
        public string VariableName { get; }

        public string Type => "port";

        public Socket Socket { get; }

        public int Port { get; }

        public GeneratePortNumberConfig(string variableName, int fallback)
        {
            VariableName = variableName;

            try
            {
                if (Socket.OSSupportsIPv4)
                {
                    Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                }
                else if (Socket.OSSupportsIPv6)
                {
                    Socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                }
            }
            catch
            {
            }

            if (Socket != null)
            {
                IPEndPoint endPoint = new IPEndPoint(Socket.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
                Socket.Bind(endPoint);
                Port = ((IPEndPoint)Socket.LocalEndPoint).Port;
            }
            else
            {
                Port = fallback;
            }
        }
    }
}
