namespace SecurityRestApi.Keycloak;

/// <summary>
/// Reprezentacja użytkownika zgodna z formatem oczekiwanym przez
/// Keycloak User Storage REST Provider.
/// </summary>
public record KeycloakUserDto(
    string Id,
    string Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled = true)
{
    public static KeycloakUserDto FromUser(User user) =>
        new(user.Id, user.Username, user.Email, user.FirstName, user.LastName);
}
