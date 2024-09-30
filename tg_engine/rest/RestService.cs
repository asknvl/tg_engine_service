using Amazon.Runtime.Internal;
using HttpMultipartParser;
using logger;
using SharpCompress.Compressors.Xz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using tg_engine.config;
using tg_engine.rest;
using TL;

namespace tg_engine.rest
{
    public class RestService : IRestService
    {
        #region const
        string tag = "rest";
        #endregion

        #region vars
        settings_rest settings;
        ILogger logger;
        #endregion

        #region properties
        public List<IRequestProcessor> RequestProcessors { get; } = new();
        #endregion

        public RestService(ILogger logger, settings_rest settings)
        {
            this.logger = logger;
            this.settings = settings;
        }

        #region private
        async Task<(HttpStatusCode, string)> processGetRequest(HttpListenerContext context)
        {
            HttpStatusCode code = HttpStatusCode.BadRequest;
            string text = code.ToString();

            await Task.Run(async () =>
            {

                var request = context.Request;
                string path = request.Url.AbsolutePath;

                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);                
                var splt = path.Split('/');
                var m = $"RX:\n{path}";
                logger.dbg(tag, m);

                try
                {
                    switch (splt[1])
                    {
                        case "control":
                            var p = RequestProcessors.FirstOrDefault(p => p is EngineControlRequestProcessor);
                            if (p != null)
                            {
                                (code, text) = await p.ProcessGetRequest(splt);
                            }                           
                            break;                        
                    }
                } catch (Exception ex)
                {
                    text = ex.Message;
                }

            });
            return (code, text);
        }

        async Task<(HttpStatusCode, string)> processPostRequest(HttpListenerContext context)
        {            
            HttpStatusCode code = HttpStatusCode.BadRequest;
            string text = code.ToString();

            await Task.Run(async () =>
            {

                var request = context.Request;
                string path = request.Url.AbsolutePath;
                var splt = path.Split('/');

                try
                {
                    IRequestProcessor? processor = null;

                    switch (splt[1])
                    {
                        case "control":
                            var p = RequestProcessors.FirstOrDefault(p => p is EngineControlRequestProcessor);
                            if (p != null)
                            {
                                (code, text) = await p.ProcessPostRequest(splt, request);
                            }
                            break;

                        case "updates":
                            processor = RequestProcessors.FirstOrDefault(p => p is MessageUpdatesRequestProcessor);
                            if (processor != null)
                            {
                                (code, text) = await processor.ProcessPostRequest(splt, request);
                            }
                            break;

                        //case "pushes":

                        //    switch (splt[2])
                        //    {
                        //        case "send":
                        //            var p = RequestProcessors.FirstOrDefault(p => p is PushRequestProcessor);
                        //            if (p != null)
                        //                (code, text) = await p.ProcessRequestData(requestBody);
                        //            break;                                
                        //    }                            
                        //    break;

                        default:
                            break;
                    }
                } catch (Exception ex)
                {
                }

            });
            return (code, text);
        }      

        async Task processRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            HttpStatusCode code = HttpStatusCode.MethodNotAllowed;
            string responseText = code.ToString();

            switch (request.HttpMethod)
            {
                case "GET":
                    (code, responseText) = await processGetRequest(context);
                    break;

                case "POST":
                    (code, responseText) = await processPostRequest(context);
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    responseText = response.StatusCode.ToString();
                    break;
            }

            response.StatusCode = (int)code;
                        

            var buffer = Encoding.UTF8.GetBytes(responseText);
            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
            await output.WriteAsync(buffer, 0, buffer.Length);

            var m = $"TX:\n{code}";
            logger.dbg(tag, m);

        }      
        #endregion

        #region public
        public async void Listen()
        {
            var listener = new HttpListener();

            listener.Prefixes.Add($"http://{settings.host}:{settings.port}/control/");
            listener.Prefixes.Add($"http://{settings.host}:{settings.port}/updates/");            

            try
            {             
                listener.Start();
            }
            catch (Exception ex)
            {
                logger.err(tag, $"Не удалось запустить HTTP сервер {ex.Message}");
                throw new Exception($"Не удалось запустить HTTP сервер {ex.Message}");
            }

            logger.inf_urgent(tag, $"HTTP сервер запущен, порт {settings.port}");

            while (true)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    await processRequest(context);
                } catch (Exception ex)
                {
                    logger.err(tag, ex.Message);
                }
            }
        }
#endregion
    }
}
