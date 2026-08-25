namespace Truss.Email
{
    /// <summary>
    /// Options for the SMTP sender.
    /// Bindable from configuration, for example the "Truss:Email:Smtp" section or
    /// environment variables such as Truss__Email__Smtp__Host.
    /// </summary>
    public sealed class TrussSmtpOptions
    {
        /// <summary>
        /// Gets or sets the SMTP host. Required.
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SMTP port. Defaults to 587.
        /// </summary>
        public int Port { get; set; } = 587;

        /// <summary>
        /// Gets or sets the user name, when the server requires authentication.
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// Gets or sets the password, when the server requires authentication.
        /// Keep it out of source control; bind it from the environment.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Gets or sets the sender address stamped on every message. Required.
        /// </summary>
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the sender.
        /// </summary>
        public string? FromName { get; set; }

        /// <summary>
        /// Gets or sets whether the connection upgrades to TLS with STARTTLS.
        /// Defaults to true; disable only against local development servers.
        /// </summary>
        public bool UseStartTls { get; set; } = true;
    }
}
