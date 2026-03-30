namespace LotusCode.Infrastructure.Auth
{
    /// <summary>
    /// Represents JWT configuration settings used for token generation and validation.
    /// Contains issuer, audience, signing key and expiration configuration.
    /// </summary>
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; init; } = string.Empty;

        public string Audience { get; init; } = string.Empty;

        public string SecretKey { get; init; } = string.Empty;

        public int ExpirationMinutes { get; init; }
    }
}
