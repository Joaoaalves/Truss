namespace Truss.Support
{
    /// <summary>
    /// Where the deck lives and how this application proves itself to it.
    /// The key is a secret: bind it from the environment
    /// (Truss__Support__Deck__ApiKey), never from appsettings.json.
    /// </summary>
    public sealed class TrussSupportDeckOptions
    {
        /// <summary>Gets or sets the deck's base address.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Gets or sets the service credential issued at registration.</summary>
        public string ApiKey { get; set; } = string.Empty;

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Url) || !Uri.TryCreate(Url, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException(
                    "The deck needs an absolute Url. Set it in AddTrussSupportDeck or bind the Truss:Support:Deck configuration section.");
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                throw new InvalidOperationException(
                    "The deck needs the ApiKey issued when the application was registered. Set it per environment with Truss__Support__Deck__ApiKey.");
            }
        }
    }
}
