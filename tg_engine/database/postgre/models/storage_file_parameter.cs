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
        required public byte[] hash { get; set; }
        required public int file_length { get; set; }
        required public string file_type { get; set; }   
        public string? file_name { get; set; }
        required public string file_extension { get; set; }
        public bool is_uploaded { get; set; } = false;
        public string? storage_id { get; set; } = null;
        public string? link { get; set; } = null;
        public bool is_removed { get; set; } = false;
        public DateTime? uploaded_at { get; set; } = null;
        public DateTime? removed_at { get; set; } = null;
        public DateTime created_at { get; set; } = DateTime.Now;
        public DateTime? updated_at { get; set; } = DateTime.Now;
    }
}
