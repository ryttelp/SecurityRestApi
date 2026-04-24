using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SecurityRestApi;
using SecurityRestApi.IdentityServer;
using SecurityRestApi.Keycloak;

var builder = WebApplication.CreateBuilder(args);

// Named HttpClient for fetching Keycloak JWKS — SSL bypass in dev due to self-signed cert
builder.Services.AddHttpClient("KeycloakBackchannel")
    .ConfigurePrimaryHttpMessageHandler(() =>
        builder.Environment.IsDevelopment()
            ? new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            }
            : new HttpClientHandler());

// Singleton so JWKS is fetched once and cached (ConfigurationManager has built-in refresh)
builder.Services.AddSingleton(sp =>
{
    var http      = sp.GetRequiredService<IHttpClientFactory>().CreateClient("KeycloakBackchannel");
    var authority = sp.GetRequiredService<IConfiguration>()["Keycloak:Authority"]!;
    return new ConfigurationManager<OpenIdConnectConfiguration>(
        $"{authority}/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever(http) { RequireHttps = false });
});

builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
    o.CombineLogs = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "SecurityRestApi", Version = "v1" });
});

builder.Services.AddIdentityServer()
    .AddDeveloperSigningCredential()                          // tylko development; w produkcji zastąp certyfikatem
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryApiResources(Config.ApiResources)
    .AddInMemoryClients(Config.Clients)
    .AddResourceOwnerValidator<CustomResourceOwnerPasswordValidator>()
    .AddProfileService<CustomProfileService>()
    .AddExtensionGrantValidator<KeycloakTokenExchangeGrantValidator>();

var app = builder.Build();

app.UseHttpLogging();
app.UseIdentityServer();

app.UseSwagger();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "SecurityRestApi v1"));

// GET /users/lookup?id=&username=&email=
app.MapGet("/users/lookup", (string? id, string? username, string? email) =>
{
    var user = UserStore.Users.FirstOrDefault(u =>
        (id       != null && u.Id       == id)       ||
        (username != null && u.Username == username) ||
        (email    != null && u.Email    == email));

    return user is not null ? Results.Ok(user) : Results.NotFound();
})
.WithName("LookupUser");

// POST /auth/validate  (application/x-www-form-urlencoded)
app.MapPost("/auth/validate", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var username = form["username"].FirstOrDefault();
    var password = form["password"].FirstOrDefault();

    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        return Results.Unauthorized();

    return UserStore.ValidateCredentials(username, password) ? Results.Ok() : Results.Unauthorized();
})
.WithName("ValidateCredentials")
.Accepts<IFormCollection>("application/x-www-form-urlencoded");

// POST /auth/update-password  (application/x-www-form-urlencoded)
app.MapPost("/auth/update-password", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var username = form["username"].FirstOrDefault();
    var password = form["password"].FirstOrDefault();

    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        return Results.BadRequest("Pole 'username' i 'password' są wymagane.");

    if (UserStore.FindByUsername(username) is null)
        return Results.NotFound();

    UserStore.UpdatePassword(username, password);
    return Results.NoContent();
})
.WithName("UpdatePassword")
.Accepts<IFormCollection>("application/x-www-form-urlencoded");

// ── Keycloak User Storage Federation endpoints ────────────────────────────────

// GET /users?search=&username=&email=&firstName=&lastName=&first=0&max=20
// Obsługuje oba tryby Keycloak UserQueryProvider:
//   - search (pełnotekstowy) — gdy podano parametr `search`
//   - atrybutowy (username/email/firstName/lastName) — gdy search jest puste
app.MapGet("/users", (
    string? search,
    string? username,
    string? email,
    string? firstName,
    string? lastName,
    int first = 0,
    int max = 20) =>
{
    var users = string.IsNullOrWhiteSpace(search)
        ? UserQueryProvider.SearchForUsers(username, email, firstName, lastName, first, max)
        : UserQueryProvider.SearchForUsers(search, first, max);

    return Results.Ok(users);
})
.WithName("KeycloakQueryUsers");

// GET /users/count?search=
app.MapGet("/users/count", (string? search) =>
    Results.Ok(UserQueryProvider.GetUsersCount(search)))
.WithName("KeycloakUsersCount");

app.Run();
