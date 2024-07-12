using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace tg_engine_launcher.Models.rest
{
    public class TGEngineApi : ITGEnginApi
    {

        #region vars
        string url;
        ServiceCollection serviceCollection;
        IHttpClientFactory httpClientFactory;
        HttpClient httpClient;
        #endregion

        public TGEngineApi(string url)
        {
            this.url = url;
            serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            var services = serviceCollection.BuildServiceProvider();
            httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
            httpClient = httpClientFactory.CreateClient();
        }

        #region private
        private static void ConfigureServices(ServiceCollection services)
        {
            services.AddHttpClient();
        }
        #endregion

        #region public
        public class dmHandlerDto
        {
            public Guid id { get; set; }
            public string source { get; set; }
            public string phone_number { get; set; }
            public int status { get; set; }
        }

        public async Task<List<dmHandlerDto>> GetAllDMHandlers()
        {
            List<dmHandlerDto> res = new();

            var addr = $"{url}/control/dmhandlers";

            try
            {
                var response = await httpClient.GetAsync(addr);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();
                res = JsonConvert.DeserializeObject<List<dmHandlerDto>>(result);

            } catch (Exception ex)
            {
                throw new Exception($"GetAllDMHandlers: {ex.Message}");
            }

            return res;
        }

        class verifyCodeDto
        {
            public Guid id { get; set; }
            public string code { get; set; }
        }

        public async Task SetVerificationCode(Guid id, string code)
        {
            verifyCodeDto codeDto = new verifyCodeDto()
            {
                id = id,
                code = code
            };

            var json = JsonConvert.SerializeObject(codeDto);
            var addr = $"{url}/control/dmhandlers/code";

            var data = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync(addr, data);
                response.EnsureSuccessStatusCode();

            } catch (Exception ex)
            {
                throw new Exception($"SetVerificationCode: {ex.Message}");
            }            
        }

        public async Task StartDMHandlers(Guid[] ids)
        {
            try
            {
                var json = JsonConvert.SerializeObject(ids);
                var addr = $"{url}/control/dmhandlers/start";
                var data = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(addr, data);
                response.EnsureSuccessStatusCode();

            } catch (Exception ex)
            {
                throw new Exception($"StartDMHandlers: {ex.Message}");
            }
        }

        public async Task StopDMHandlers(Guid[] ids)
        {
            try
            {
                var json = JsonConvert.SerializeObject(ids);
                var addr = $"{url}/control/dmhandlers/stop";
                var data = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(addr, data);
                response.EnsureSuccessStatusCode();

            }
            catch (Exception ex)
            {
                throw new Exception($"StopDMHandlers: {ex.Message}");
            }
        }        
        #endregion

    }
}
