// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Net;
using System.Net.Sockets;

namespace Aspire.Tools.Service.UnitTests;

public static class Helpers
{
    public static async Task<bool> CanConnectToPortAsync(Uri url, uint msToWait, CancellationToken cancelToken)
    {
        bool connected = false;
        Socket? ipv4Socket = null;
        Socket? ipv6Socket = null;

        // Create a "client" socket on any available port
        try
        {
            TimeoutSpan timeout = new(msToWait);
            if (Socket.OSSupportsIPv4)
            {
                try
                {
                    ipv4Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    ipv4Socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                }
                catch (SocketException)
                {
                    if (ipv4Socket != null)
                    {
                        ipv4Socket.Close();
                        ipv4Socket = null;
                    }
                }
            }
            if (Socket.OSSupportsIPv6)
            {
                try
                {
                    ipv6Socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                    ipv6Socket.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));
                }
                catch (SocketException)
                {
                    if (ipv6Socket != null)
                    {
                        ipv6Socket.Close();
                        ipv6Socket = null;
                    }
                }
            }

            // No sockets means we aren't connected
            if (ipv6Socket == null && ipv4Socket == null)
            {
                return false;
            }

            // If we have an IP address we use that otherwise assume loopback
            IPEndPoint ipv4ServerEndPoint;
            IPEndPoint ipv6ServerEndPoint;
            if (IPAddress.TryParse(url.Host, out var ipAddress))
            {
                ipv4ServerEndPoint = new IPEndPoint(ipAddress.AddressFamily == AddressFamily.InterNetwork ? ipAddress : IPAddress.Loopback, url.Port);
                ipv6ServerEndPoint = new IPEndPoint(ipAddress.AddressFamily == AddressFamily.InterNetworkV6 ? ipAddress : IPAddress.IPv6Loopback, url.Port);
            }
            else
            {
                ipv4ServerEndPoint = new IPEndPoint(IPAddress.Loopback, url.Port);
                ipv6ServerEndPoint = new IPEndPoint(IPAddress.IPv6Loopback, url.Port);
            }

            // If a process is passed in, we bail if it has exited
            while (!connected && !timeout.Expired)
            {
                cancelToken.ThrowIfCancellationRequested();
                if (ipv4Socket != null)
                {
                    try
                    {
                        // Now use IOControl to set the calls non blocking
                        ipv4Socket.Blocking = false;
                        // Since we are non-blocking, the Connect should throw an error indicating
                        // it needs time to connect
                        ipv4Socket.Connect(ipv4ServerEndPoint);
                    }
                    catch (SocketException)
                    {
                        // Now ping retry and block for a millisecond timeout
                        ArrayList connectList = new ArrayList() {ipv4Socket};
                        Socket.Select(null, connectList, null, 1000 /*microSecond -- in here, 1 milli-second*/);
                        if (connectList.Count == 1)
                        {
                            connected = true;
                            break;
                        }
                    }
                    finally
                    {
                        // TODO: why do we set the sockets back to blocking?
                        ipv4Socket.Blocking = true;
                    }
                }

                // Now try IPV6
                if (ipv6Socket != null)
                {
                    // Couldn't connect with IPV4, so try IPV6
                    try
                    {
                        ipv6Socket.Blocking = false;
                        ipv6Socket.Connect(ipv6ServerEndPoint);
                    }
                    catch (SocketException)
                    {
                        // Ping retry
                        ArrayList connectList = new ArrayList() {ipv6Socket};
                        Socket.Select(null, connectList, null, 1000 /*microSecond -- in here, 1 milli-second*/);
                        if (connectList.Count == 1)
                        {
                            connected = true;
                            break;
                        }
                    }
                    finally
                    {
                        // TODO: why do we set the sockets back to blocking?
                        ipv6Socket.Blocking = true;
                    }
                }

                // Wait a bit and try again
                await Task.Delay(20, cancelToken);
            }

        }
        finally
        {
            if (ipv4Socket != null)
            {
                ipv4Socket.Close();
            }

            if (ipv6Socket != null)
            {
                ipv6Socket.Close();
            }
        }

        return connected;
    }
}

internal class TimeoutSpan
{
    private readonly long _duration;
    private long _startingTickCount;

	    public TimeoutSpan(long durationInMilliseconds)
    {
        // There are 10000 ticks in a millisecond so need to adjust accordingly
        _duration = durationInMilliseconds * 10000;
        Reset();
    }

	    public bool Expired
    {
        get 
        {
            return _duration != 0 && (DateTime.UtcNow.Ticks - _startingTickCount) > _duration;
        }
    }

	    public void Reset() 
    {
        // DateTime.UtcNow is way more efficient than DateTime.Now since it doesn't have to deal with locale, DST, etc
        _startingTickCount = DateTime.UtcNow.Ticks;
    }
}
