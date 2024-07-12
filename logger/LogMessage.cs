namespace logger
{
    public enum LogMessageType
    {
        dbg,
        warn,
        err,
        inf,
        inf_urgent,
        user_input
    }
    public class LogMessage
    {        
        public LogMessageType Type { get; set; }     
        public string TAG { get; set; }        
        public string Text { get; set; }        
        public string Date { get; set; }
    

        public LogMessage(LogMessageType type, string tag, string text) { 
            TAG = tag;
            this.Type = type;
            Text = text;
            Date = DateTime.Now.ToString();
        }

        public override string ToString()
        {
            return $"{Date} {Type} {TAG} > {Text}";
        }

        public string ToFiltered()
        {
            return $"{Type}{TAG}{Text}".ToLower();
        }
    }

}
