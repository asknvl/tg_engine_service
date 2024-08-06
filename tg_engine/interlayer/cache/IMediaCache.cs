using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.interlayer.cache
{
    public interface IMediaCache
    {
        void Add(string storage_id, MediaCaheItem imtem);
        MediaCaheItem Get(string storage_id);
        void Update(string storage_id, MediaCaheItem item);

        void Load();
        void Save();
    }

    public class MediaCaheItem
    {
        public long file_id { get; set; }
        public byte[] file_reference { get; set; }
        public long acess_hash { get; set; }
    }
}
