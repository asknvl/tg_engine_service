using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.database.postgre.models
{
    public class storage_file_parameter
    {
        public Guid id { get; set; }
        public byte[] hash { get; set; }    
        public int file_length { get; set; }
        public string file_type { get; set; }   
        public string? file_name { get; set; }
        public string file_extension { get; set; }  
        public bool is_uploaded { get; set; }
        public string? storage_id { get; set; } 
        public string? link { get; set; }   
        public bool is_removed { get; set; }    
        public DateTime uploaded_at { get; set; }
        public DateTime removed_at { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set;}
    }
}
