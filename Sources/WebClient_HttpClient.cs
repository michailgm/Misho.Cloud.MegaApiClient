using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Misho.Cloud.MegaNz
{
    public class WebClient : IWebClient
    {
        private const int DefaultResponseTimeout = Timeout.Infinite;

        private readonly HttpClient httpClient = new HttpClient();

        public WebClient(int responseTimeout = DefaultResponseTimeout, ProductInfoHeaderValue userAgent = null)
        {
            BufferSize = Options.DefaultBufferSize;
            httpClient.Timeout = TimeSpan.FromMilliseconds(responseTimeout);
            httpClient.DefaultRequestHeaders.UserAgent.Add(userAgent ?? GenerateUserAgent());
        }

        public int BufferSize { get; set; }

        public string PostRequestJson(Uri url, string jsonData)
        {
            using (MemoryStream jsonStream = new MemoryStream(jsonData.ToBytes()))
            {
                return PostRequest(url, jsonStream, "application/json");
            }
        }

        public string PostRequestRaw(Uri url, Stream dataStream)
        {
            return PostRequest(url, dataStream, "application/octet-stream");
        }

        public Stream GetRequestRaw(Uri url)
        {
            return httpClient.GetStreamAsync(url).Result;
        }

        private string PostRequest(Uri url, Stream dataStream, string contentType)
        {
            using (StreamContent content = new StreamContent(dataStream, BufferSize))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                using (HttpResponseMessage response = httpClient.PostAsync(url, content).Result)
                {
                    using (Stream stream = response.Content.ReadAsStreamAsync().Result)
                    {
                        using (StreamReader streamReader = new StreamReader(stream, Encoding.UTF8))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }

        private ProductInfoHeaderValue GenerateUserAgent()
        {
            AssemblyName assemblyName = Assembly.GetExecutingAssembly().GetName();
            return new ProductInfoHeaderValue(assemblyName.Name, assemblyName.Version.ToString(2));
        }
    }
}
