namespace SipLoadTester
{
    public class SipSettings
    {
        public string SipDomain { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ExternalDomain { get; set; }
        public int CallCount { get; set; } = 100;
        public int CallDelayMs { get; set; } = 5000;
    }
}
