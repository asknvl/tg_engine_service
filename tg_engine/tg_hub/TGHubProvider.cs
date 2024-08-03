using Amazon.Runtime;
using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using tg_engine.config;
using tg_engine.tg_hub.events;

namespace tg_engine.tg_hub
{
    public class TGHubProvider : ITGHubProvider
    {
        #region vars
        string url;
        ServiceCollection serviceCollection;
        IHttpClientFactory httpClientFactory;
        HttpClient httpClient;
        #endregion

        public TGHubProvider(settings_hub settings) {
            url = settings.host;
            serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            var services = serviceCollection.BuildServiceProvider();
            httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
            httpClient = httpClientFactory.CreateClient();
            //httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.token);
            httpClient.DefaultRequestHeaders.Add("Cookie", $"access_token={settings.token}");
        }

        #region private
        private static void ConfigureServices(ServiceCollection services)
        {
            services.AddHttpClient();
        }
        #endregion

        #region public
        public async Task SendEvent(EventBase hevent) {

            var addr = $"{url}/{hevent.path}";
            var json = hevent.GetSerialized();
            var data = new StringContent(json, Encoding.UTF8, "application/json");
            string res = "";

            try
            {
                var response = await httpClient.PostAsync(addr, data);
                res = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
            } catch (Exception ex)
            {
                throw new Exception($"SendEvent error {ex.Message} {res}");
            }
        }
        #endregion

    }
}
