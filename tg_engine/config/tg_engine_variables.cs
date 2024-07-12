using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace tg_engine.config
{
    public class variables
    {        
        #region singletone
        private static variables instance;
        public variables() {
            tg_engine_variables = load();
        }

        public static variables getInstance()
        {
            if (instance == null)
                instance = new variables();
            return instance;
        }
        #endregion

        #region const        
        #endregion

        #region properties        
        [JsonProperty]
        public tg_engine_variables tg_engine_variables { get; private set; } = new();
        #endregion

        #region public
        public tg_engine_variables load()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "tg_engine_variables.json");
            tg_engine_variables vars = new tg_engine_variables();
            string sjson = "";


            if (!File.Exists(path))
            {
                sjson = JsonConvert.SerializeObject(vars, Formatting.Indented);
                File.WriteAllText(path, sjson);
                
            } else
            {
                sjson = File.ReadAllText(path);
                vars = JsonConvert.DeserializeObject<tg_engine_variables>(sjson);
            }

            return vars;
        }
        #endregion
    }

    public class settings_db
    {
        public string host { get; set; } = "host";
        public string db_name { get; set; } = "db_name";
        public string user { get; set; } = "login";
        public string password { get; set; } = "password";
    }

    public class settings_rest
    {
        public int control_port { get; set; } = 7070;
    }

    public class tg_engine_variables
    {
        public settings_db accounts_settings_db { get; set; } = new();
        public settings_db messaging_settings_db { get; set; } = new();
        public settings_rest settings_rest { get; set; } = new();

    }
    
}
