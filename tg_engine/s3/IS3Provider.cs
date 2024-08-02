﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.s3
{
    public interface IS3Provider
    {
        Task Upload(string storage_id, byte[] bytes);
        Task<byte[]> Download(string storage_id);
    }
}
