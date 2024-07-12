using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TL;

namespace tg_engine.rest
{
    public class EngineControlRequestProcessor : IRequestProcessor
    {
        #region vars
        tg_engine_base tg_engine;
        #endregion

        public EngineControlRequestProcessor(tg_engine_base tg_engine) {
            this.tg_engine = tg_engine;
        }

        #region dtos  
        class dmHandlerDto
        {
            public Guid id { get; set; }
            public string source { get; set; }
            public string phone_number { get; set; }
            public int status { get; set; }
        }

        class verifyCodeDto
        {
            public Guid id { get; set; }
            public string code { get; set; }
        }
        #endregion

        #region helpers
        List<dmHandlerDto> getDMHandlers()
        {
            List<dmHandlerDto> res = new();

            foreach (var dm in tg_engine.DMHandlers) {
                res.Add(new dmHandlerDto() {
                    id = dm.settings.account.id,
                    source = dm.settings.source,
                    phone_number = dm.settings.account.phone_number,
                    status = (int)dm.status
                });
            }
            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data">
        /// "[
        ///     "7823e188-6df4-4adf-8021-8f67a496b7a6",
        ///     "7823e188-6df4-4adf-8021-8f67a496b7a6"
        ///  ] или [] для запуска всех DMHandler"</param>
        /// <param name="state">true-запуск, false-стоп</param>
        /// <returns></returns>
        async Task<(HttpStatusCode, string)> toggleDMHandlers(string data, bool state)
        {
            HttpStatusCode code = HttpStatusCode.OK;
            string responseText = code.ToString();
            
            try
            {
                var guids = JsonConvert.DeserializeObject<List<Guid>>(data);
                await tg_engine.ToggleDMHandlers(guids, state); 

            } catch (Exception ex)
            {
                code = HttpStatusCode.InternalServerError;
                responseText = $"{code.ToString()}:{ex.Message}";
            }

            return (code,  responseText);   
        }

        (HttpStatusCode, string) getKeepAlive()
        {
            HttpStatusCode code = HttpStatusCode.ServiceUnavailable;
            string responseText = $"{code}";

            if (tg_engine.IsActive)
            {
                code = HttpStatusCode.OK;
                responseText = $"{code} Service version {tg_engine.Version}";
            }

            return (code, responseText);
        }

        (HttpStatusCode, string) setVerificationCode(string data)
        {
            HttpStatusCode code = HttpStatusCode.OK;
            string responseText = code.ToString();

            try
            {
                var vc = JsonConvert.DeserializeObject<verifyCodeDto>(data);
                var found = tg_engine.DMHandlers.FirstOrDefault(d => d.settings.account.id == vc.id);
                if (found != null)
                {
                    found.SetVerificationCode(vc.code);
                }

            } catch (Exception ex)
            {
                code = HttpStatusCode.BadRequest;
                responseText = $"{code.ToString()}:{ex.Message}";
            }

            return (code, responseText);
        }
        #endregion

        #region public
        public async Task<(HttpStatusCode, string)> ProcessGetRequest(string[] splt_route)
        {
            var code = HttpStatusCode.NotFound;
            var responseText = code.ToString();

            try
            {
                switch (splt_route[2])
                {
                    case "dmhandlers":
                        code = HttpStatusCode.OK;
                        responseText = JsonConvert.SerializeObject(getDMHandlers(), Formatting.Indented);
                        break;

                    case "keepalive":
                        (code, responseText) = getKeepAlive();
                        break;

                    default:
                        break;
                }
            } catch (Exception ex)
            {
            }

            await Task.CompletedTask;
            return (code, responseText);
        }

        public async Task<(HttpStatusCode, string)> ProcessPostRequest(string[] splt_route, string data)
        {
            var code = HttpStatusCode.NotFound;
            var responseText = code.ToString();

            try
            {
                switch (splt_route[2])
                {
                    case "dmhandlers":

                        switch (splt_route[3])
                        {
                            case "start":
                                (code, responseText) = await toggleDMHandlers(data, true);
                                break;
                            case "stop":
                                (code, responseText) = await toggleDMHandlers(data, false);
                                break;
                            case "code":
                                (code, responseText) = setVerificationCode(data);
                                break;
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
