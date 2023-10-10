// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Cli.Package.Tests
{
    public class MockServer
    {
        private HttpListener _listener;
        private Dictionary<string, string> _mockResponses = new Dictionary<string, string>();
        private bool _running;
        public int Port;
        private static readonly PortFinder PortFinder = new PortFinder();
        public MockServer()
        {
            Port = PortFinder.AllocateFreePort();
        }
        public void Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:" + Port + "/"); // URL prefix
            _listener.Start();
            _running = true;

            // Handle requests in a separate task
            Task.Run(() => HandleRequests());
        }

        private void HandleRequests()
        {
            while (_running)
            {
                var context = _listener.GetContext(); // Block until a request is made
                var request = context.Request;
                var response = context.Response;

                var key = request.Url.AbsolutePath + request.Url.Query;
                if (_mockResponses.TryGetValue(key, out var mockResponse))
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(mockResponse);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "application/json";
                    var output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    output.Close();
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Close();
                }
            }
        }

        public void Stop()
        {
            _running = false;
            _listener?.Stop();
            _listener?.Close();
            PortFinder.ReleasePort(Port);
        }

        public void AddMockResponse(string requestPath, object responseObject)
        {
            _mockResponses[requestPath] = JsonConvert.SerializeObject(responseObject);
        }
    }
}
