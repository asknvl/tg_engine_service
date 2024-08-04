using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using tg_engine.interlayer.messaging;
using tg_engine.rest.updates;

namespace tg_engine.rest
{
    public class MessageUpdatesRequestProcessor : IRequestProcessor, IMessageUpdatesObservable
    {
        #region vars
        List<IMessageUpdatesObserver> messageUpdatesObservers = new List<IMessageUpdatesObserver>();
        #endregion

        public MessageUpdatesRequestProcessor() {            
        }

        #region dtos  
        public class messageDto()
        {
            public Guid account_id { get; set; }
            public Guid chat_id { get; set; }
            public Guid telegram_user_id { get; set; }
            public mediaDto? media { get; set; } 
            public string text { get; set; }
        }

        public class mediaDto()
        {
            public string type { get; set; }            
            public string storage_id { get; set; }
        }      
        #endregion

        #region helpers      
        #endregion

        #region public
        public void Add(IMessageUpdatesObserver observer)
        {
            if (observer != null && !messageUpdatesObservers.Contains(observer))
                messageUpdatesObservers.Add(observer);
        }
        public void Remove(IMessageUpdatesObserver observer)
        {
            messageUpdatesObservers.Remove(observer);
        }

        public async Task<(HttpStatusCode, string)> ProcessGetRequest(string[] splt_route)
        {
            var code = HttpStatusCode.NotFound;
            var responseText = code.ToString();
            await Task.CompletedTask;
            return (code, responseText);
        }

        public async Task<(HttpStatusCode, string)> ProcessPostRequest(string[] splt_route, string data)
        {
            var code = HttpStatusCode.NotFound;
            var responseText = code.ToString();

            IMessageUpdatesObserver? observer = null;

            try
            {
                switch (splt_route[2])
                {
                    case "sendMessage":
                        try
                        {
                            var message = JsonConvert.DeserializeObject<messageDto>(data);

                            observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == message.account_id);

                            if (observer != null)
                            {
                                await observer.OnNewMessage(message);       
                                
                                code = HttpStatusCode.OK;
                                responseText = code.ToString();
                            }

                        } catch (Exception ex)
                        {
                            code = HttpStatusCode.BadRequest;
                            responseText = $"{code}:{ex.Message}";
                        }
                        break;

                    case "sendUpdate":
                        try
                        {
                            var updateBase = JsonConvert.DeserializeObject<UpdateBase>(data);

                            observer = messageUpdatesObservers.FirstOrDefault(o => o.account_id == updateBase.account_id);

                            if (observer != null)
                            {
                                switch (updateBase.type)
                                {
                                    case UpdateType.readHistory:
                                        var rh = JsonConvert.DeserializeObject<readHistory>(data);
                                        observer?.OnNewUpdate(rh);
                                        break;

                                    default:
                                        code = HttpStatusCode.BadRequest;
                                        responseText = $"{code}";
                                        break;
                                }
                            }
                        } catch (Exception ex)
                        {
                            code = HttpStatusCode.BadRequest;
                            responseText = $"{code}:{ex.Message}";
                        }
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
            }

            await Task.CompletedTask;
            return (code, responseText);
        }       
        #endregion
    }    
}
