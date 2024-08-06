using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.interlayer.cache
{
    public class MediaCache : IMediaCache
    {
        #region vars
        Dictionary<string, MediaCaheItem> cache = new();
        #endregion

        public void Add(string storage_id, MediaCaheItem imtem)
        {            
        }

        public MediaCaheItem Get(string storage_id)
        {
            throw new NotImplementedException();
        }

        public void Update(string storage_id, MediaCaheItem item)
        {
            throw new NotImplementedException();
        }

        public void Load()
        {
            throw new NotImplementedException();
        }

        public void Save()
        {
            throw new NotImplementedException();
        }       
    }
}
