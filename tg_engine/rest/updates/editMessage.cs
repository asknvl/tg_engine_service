using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.rest.updates
{
    public class editMessage : UpdateBase
    {        
        public int telegram_message_id { get; set; }
        public string? operator_id { get; set; }
        public string? operator_letters { get; set; }        
        public string? text { get; set; } = null;
        public string? screen_text { get; set; } = null;
        public string? type { get; set; } = null;
        public string? file_name { get; set; } = null;
        public string? file_extension { get; set; } = null;
        public byte[]? file { get; set; } = null;
    }
}
