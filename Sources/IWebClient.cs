using System;
using System.IO;

namespace Misho.Cloud.MegaNz
{
    public interface IWebClient
    {
        int BufferSize { get; set; }

        string PostRequestJson(Uri url, string jsonData);

        string PostRequestRaw(Uri url, Stream dataStream);

        Stream GetRequestRaw(Uri url);
    }
}
