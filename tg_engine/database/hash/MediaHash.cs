using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.database.hash
{
    public static class MediaHash
    {
        public static string Get(byte[] input)
        {
            using (SHA256 sha = SHA256.Create())
            {
                var _32bytes = sha.ComputeHash(input);
                StringBuilder hex = new StringBuilder();
                foreach (byte b in _32bytes) {
                    hex.Append(b.ToString("X2"));
                }
                return hex.ToString();
            }            
        }
    }
}
