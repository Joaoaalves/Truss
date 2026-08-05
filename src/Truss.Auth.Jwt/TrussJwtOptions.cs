namespace Truss.Auth
{
    /// <summary>
    /// Options for JWT authentication.
    /// Bindable from configuration, for example the "Truss:Auth:Jwt" section or
    /// environment variables such as Truss__Auth__Jwt__SigningKey.
    /// </summary>
    public sealed class TrussJwtOptions
    {
        /// <summary>
        /// Gets or sets the token issuer. Required.
        /// </summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the token audience. Required.
        /// </summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the symmetric signing key. Required, at least 32 characters.
        /// Keep it out of source control; bind it from configuration or environment variables.
        /// </summary>
        public string SigningKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the access token lifetime. Defaults to 15 minutes.
        /// </summary>
        public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Gets or sets the refresh token lifetime. Defaults to 7 days.
        /// </summary>
        public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
            {
                throw new InvalidOperationException(
                    "JWT authentication requires Issuer and Audience. Set them in AddTrussJwtAuth or bind the Truss:Auth:Jwt configuration section."
                );
            }

            if (SigningKey.Length < 32)
            {
                throw new InvalidOperationException(
                    "JWT authentication requires a SigningKey of at least 32 characters. Set it in AddTrussJwtAuth or bind the Truss:Auth:Jwt configuration section."
                );
            }
        }
    }
}
