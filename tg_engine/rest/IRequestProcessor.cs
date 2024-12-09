using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.rest
{
    public interface IRequestProcessor
    {
        Task<(HttpStatusCode, string)> ProcessGetRequest(string[] splt_route, NameValueCollection? query = null);
//        Task<(HttpStatusCode, string)> ProcessPostRequest(string[] splt_route, string data);
        Task<(HttpStatusCode, string)> ProcessPostRequest(string[] splt_route, HttpListenerRequest request);
    }
}
