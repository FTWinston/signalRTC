namespace Signal.Models
{
    public class IdentifierSettings
    {
        public int MinLength { get; set; } = 3;

        public string[] AllowedCharacters { get; set; } = "ABCDEFGH".Split();
    }
}
