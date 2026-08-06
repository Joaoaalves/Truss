namespace Truss.Email.Resend
{
    /// <summary>
    /// Options for the Resend sender.
    /// Bindable from configuration, for example the "Truss:Email:Resend" section or
    /// environment variables such as Truss__Email__Resend__ApiKey.
    /// </summary>
    public sealed class TrussResendOptions
    {
        /// <summary>
        /// Gets or sets the Resend API key. Required.
        /// Keep it out of source control; bind it from the environment.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the sender address stamped on every message. Required,
        /// and the domain must be verified in the Resend dashboard.
        /// </summary>
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the sender.
        /// </summary>
        public string? FromName { get; set; }
    }
}
