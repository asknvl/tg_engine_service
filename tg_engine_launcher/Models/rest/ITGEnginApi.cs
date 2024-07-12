using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static tg_engine_launcher.Models.rest.TGEngineApi;

namespace tg_engine_launcher.Models.rest
{
    public interface ITGEnginApi
    {
        Task<List<dmHandlerDto>> GetAllDMHandlers();
        Task SetVerificationCode(Guid id, string code);
        Task StartDMHandlers(Guid[] ids);
        Task StopDMHandlers(Guid[] ids);
    }
}
