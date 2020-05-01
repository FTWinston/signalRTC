namespace Signal.Models
{
    public class IdentifierConfiguration
    {
        public int MinLength { get; set; }

        public string[] AllowedCharacters { get; set; } = new string[] { };
    }
}
