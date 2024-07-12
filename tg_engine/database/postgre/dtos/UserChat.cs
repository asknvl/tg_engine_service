using tg_engine.database.postgre.models;

namespace tg_engine.database.postgre.dtos
{
    public class UserChat
    {
        public telegram_chat chat { get; set; } = new();
        public telegram_user user { get; set; } = new();
    }
}