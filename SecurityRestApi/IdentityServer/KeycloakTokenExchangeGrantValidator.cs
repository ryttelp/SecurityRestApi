using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace SecurityRestApi.IdentityServer;

public class KeycloakTokenExchangeGrantValidator : IExtensionGrantValidator
{
    public string GrantType => "urn:ietf:params:oauth:grant-type:token-exchange";

    private const string SubjectTokenType = "urn:ietf:params:oauth:token-type:access_token";

    private readonly string _keycloakAuthority;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;
    private readonly ILogger<KeycloakTokenExchangeGrantValidator> _logger;

    public KeycloakTokenExchangeGrantValidator(
        IConfiguration config,
        ConfigurationManager<OpenIdConnectConfiguration> configManager,
        ILogger<KeycloakTokenExchangeGrantValidator> logger)
    {
        _keycloakAuthority = config["Keycloak:Authority"]!;
        _configManager     = configManager;
        _logger            = logger;
    }

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        var subjectToken     = context.Request.Raw.Get("subject_token");
        var subjectTokenType = context.Request.Raw.Get("subject_token_type");

        if (string.IsNullOrEmpty(subjectToken))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "subject_token is required");
            return;
        }

        if (!string.IsNullOrEmpty(subjectTokenType) && subjectTokenType != SubjectTokenType)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest,
                $"subject_token_type must be {SubjectTokenType}");
            return;
        }

        try
        {
            var oidcConfig = await _configManager.GetConfigurationAsync(CancellationToken.None);

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidIssuer              = _keycloakAuthority,
                ValidateAudience         = false, // Keycloak access_tokens have aud="account", not "internalkg"
                ValidateLifetime         = true,
                IssuerSigningKeys        = oidcConfig.SigningKeys,
                ValidateIssuerSigningKey = true,
                ClockSkew                = TimeSpan.FromSeconds(30),
            };

            var handler = new JsonWebTokenHandler();
            var result  = await handler.ValidateTokenAsync(subjectToken, validationParams);

            if (!result.IsValid)
            {
                _logger.LogWarning("Keycloak token validation failed: {Reason}", result.Exception?.Message);
                context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Invalid subject_token");
                return;
            }

            var sub = result.ClaimsIdentity.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(sub))
            {
                context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "subject_token missing sub claim");
                return;
            }

            context.Result = new GrantValidationResult(subject: sub, authenticationMethod: GrantType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Keycloak token exchange validation");
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Token validation error");
        }
    }
}