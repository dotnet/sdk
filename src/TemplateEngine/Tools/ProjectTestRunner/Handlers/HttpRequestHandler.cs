using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using ProjectTestRunner.HandlerResults;

namespace ProjectTestRunner.Handlers
{
    public class HttpRequestHandler : IHandler
    {
        public static string Handler => "httpRequest";

        public string HandlerName => Handler;

        public IHandlerResult Execute(IReadOnlyDictionary<string, string> tokens, IReadOnlyList<IHandlerResult> results, JObject json)
        {
            Stopwatch watch = Stopwatch.StartNew();

            try
            {
                string url = json["url"].ToString();
                int status = json["statusCode"].Value<int>();
                string verb = json["verb"].ToString();
                string body = json["body"]?.ToString();
                string requestMediaType = json["requestMediaType"]?.ToString();
                string requestEncoding = json["requestEncoding"]?.ToString();

                HttpClient client = new HttpClient();
                HttpRequestMessage message = new HttpRequestMessage(new HttpMethod(verb), url);

                if (body != null)
                {
                    if (!string.IsNullOrEmpty(requestEncoding))
                    {
                        if (!string.IsNullOrEmpty(requestMediaType))
                        {
                            message.Content = new StringContent(body, Encoding.GetEncoding(requestEncoding), requestMediaType);
                        }
                        else
                        {
                            message.Content = new StringContent(body, Encoding.GetEncoding(requestEncoding));
                        }
                    }
                    else
                    {
                        message.Content = new StringContent(body);
                    }
                }

                try
                {
                    HttpResponseMessage response = client.SendAsync(message).Result;
                    bool success = status == (int)response.StatusCode;
                    return new GenericHandlerResult(watch.Elapsed, success, success ? null : $"Expected {status} but got {response.StatusCode}");
                }
                catch (Exception ex)
                {
                    return new GenericHandlerResult(watch.Elapsed, false, ex.Message);
                }
            }
            finally
            {
                watch.Stop();
            }
        }

        public string Summarize(IReadOnlyDictionary<string, string> tokens, JObject json)
        {
            string url = json["url"].ToString();
            int status = json["statusCode"].Value<int>();
            string verb = json["verb"].ToString();
            string body = json["body"]?.ToString();
            string requestMediaType = json["requestMediaType"]?.ToString();
            string requestEncoding = json["requestEncoding"]?.ToString();

            return $"Web Request - {verb} {url} (Body? {body != null}, Encoding? {requestEncoding}, MediaType? {requestMediaType}) -> Expect {status}";
        }
    }
}
