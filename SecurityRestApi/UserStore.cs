namespace SecurityRestApi;

public record User(string Id, string Username, string? Email, string? FirstName, string? LastName);

public static class UserStore
{
    public static readonly IReadOnlyList<User> Users =
    [
        new("1", "jkowalski", "jan.kowalski@example.com", "Jan", "Kowalski"),
        new("2", "anowak",   "anna.nowak@example.com",   "Anna", "Nowak"),
        new("3", "admin",    "admin@example.com",         "Admin", null),
    ];

    private static readonly Dictionary<string, string> Credentials = new(StringComparer.Ordinal)
    {
        ["jkowalski"] = "haslo123",
        ["anowak"]    = "tajnehaslo",
        ["admin"]     = "admin",
    };

    public static bool ValidateCredentials(string username, string password) =>
        Credentials.TryGetValue(username, out var expected) && expected == password;

    public static User? FindById(string id)           => Users.FirstOrDefault(u => u.Id       == id);
    public static User? FindByUsername(string name)   => Users.FirstOrDefault(u => u.Username == name);
    public static User? FindByEmail(string email)     => Users.FirstOrDefault(u => u.Email    == email);
}
