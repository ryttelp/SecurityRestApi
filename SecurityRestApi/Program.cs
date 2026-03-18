var app = WebApplication.Create(args);

// --- Test data ---
var users = new List<User>
{
    new("1", "jkowalski", "jan.kowalski@example.com", "Jan", "Kowalski"),
    new("2", "anowak",   "anna.nowak@example.com",   "Anna", "Nowak"),
    new("3", "admin",    "admin@example.com",         "Admin", null),
};

var credentials = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["jkowalski"] = "haslo123",
    ["anowak"]    = "tajnehaslo",
    ["admin"]     = "admin",
};

// GET /users/lookup?id=&username=&email=
app.MapGet("/users/lookup", (string? id, string? username, string? email) =>
{
    var user = users.FirstOrDefault(u =>
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

    if (credentials.TryGetValue(username, out var expected) && expected == password)
        return Results.Ok();

    return Results.Unauthorized();
})
.WithName("ValidateCredentials")
.Accepts<IFormCollection>("application/x-www-form-urlencoded");

app.Run();

record User(string Id, string Username, string? Email, string? FirstName, string? LastName);