using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace SharpServerLib
{
    public static class HttpMethod
    {
        public const string GET = "GET";
        public const string POST = "POST";
        //public static string PUT
    }

    public static class HttpStatus
    {
        public const int OKAY = 200;
        public const int FILENOTFOUND = 404;
        public const int INTERNAL_ERROR = 500;
    }

    public class HttpRequestInfo
    {
        public string method;
        public int status;
        public string file;
        public string mimeType;

        public string Headers;
        public bool skipBuiltinServe = false;
        //NetworkStream clientStream = null;
        public byte[] rawFile = null;
        public byte[] finalizedFile = null;
        public Dictionary<string, string> directives = new Dictionary<string, string>();
    }
}
