using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.database.postgre.models
{
    //Может быть и канал и юзер
    public class telegram_user 
    {
        [Key]
        public Guid id { get; set; }
        public long telegram_id { get; set; }
        public long? access_hash { get; set; }
        public string? firstname { get; set; }
        public string? lastname { get; set; }
        public string? username { get; set; }

        public telegram_user()
        {
        }

        public telegram_user(TL.User user)
        {
            telegram_id = user.ID;
            access_hash = user.access_hash;
            firstname = user.first_name;
            lastname = user.last_name;
            username = user.username;
        }

        public override string ToString()
        {
            return $"tg_id={telegram_id} hash={access_hash} {firstname} {lastname} {username}";
        }
    }
}
