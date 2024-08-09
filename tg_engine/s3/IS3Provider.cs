using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.s3
{
    public interface IS3Provider
    {
        Task<S3ItemInfo> Upload(byte[] bytes, string extension);
        Task<(byte[], S3ItemInfo)> Download(string storage_id);
        Task<S3ItemInfo> GetInfo(string storage_id);
    }

    public class S3ItemInfo
    {
        public string storage_id { get; set; }
        public string? extension { get; set; }
        public string? url { get; set; }
    }
}
