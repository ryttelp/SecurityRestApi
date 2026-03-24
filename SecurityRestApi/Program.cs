using SecurityRestApi;
using SecurityRestApi.IdentityServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
    o.CombineLogs = true;
});

builder.Services.AddIdentityServer()
    .AddDeveloperSigningCredential()                          // tylko development; w produkcji zastąp certyfikatem
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryClients(Config.Clients)
    .AddResourceOwnerValidator<CustomResourceOwnerPasswordValidator>()
    .AddProfileService<CustomProfileService>();

var app = builder.Build();

app.UseHttpLogging();
app.UseIdentityServer();

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

app.Run();
