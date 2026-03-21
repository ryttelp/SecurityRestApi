using Duende.IdentityServer.Models;

namespace SecurityRestApi.IdentityServer;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email(),
    ];

    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new ApiScope("api", "SecurityRestApi"),
    ];

    public static IEnumerable<Client> Clients =>
    [
        // Resource Owner Password Credentials — do integracji maszyna-maszyna / testów
        new Client
        {
            ClientId     = "ropc-client",
            ClientName   = "Resource Owner Password Client",
            AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedScopes = { "openid", "profile", "email", "api" },
            AccessTokenLifetime = 3600,
        },

        // Kod autoryzacji z PKCE — do aplikacji przeglądarkowych / Swagger UI
        new Client
        {
            ClientId     = "interactive",
            ClientName   = "Interactive Client (PKCE)",
            AllowedGrantTypes   = GrantTypes.Code,
            RequirePkce         = true,
            RequireClientSecret = false,
            RedirectUris         = { "http://localhost:5236/signin-oidc" },
            PostLogoutRedirectUris = { "http://localhost:5236/signout-callback-oidc" },
            AllowedScopes = { "openid", "profile", "email", "api" },
            AllowOfflineAccess = true,
        },
    ];
}
