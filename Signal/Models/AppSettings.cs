namespace Signal.Models
{
    public class AppSettings
    {
        public int KeepAliveInterval { get; set; } = 120;

        public string[] AllowedOrigins { get; set; } = new string[] { };
    }
}
