namespace ProjectManager.API.Common.Options
{
    public class EmailOptions
    {
        public string? ResendApiKey { get; set; }
        public string EmailFrom { get; set; } = "noreply@trunkpeter.com";
        public string FrontendUrl { get; set; } = "http://localhost:5173";
    }
}
