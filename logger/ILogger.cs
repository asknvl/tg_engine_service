namespace logger
{
    public interface ILogger
    {
        void dbg(string tag, string text);
        void warn(string tag, string text);
        void err(string tag, string text);
        void inf(string tag, string text);
        void inf_urgent(string tag, string text);
        void user_input(string tag, string text);
    }
    
}
