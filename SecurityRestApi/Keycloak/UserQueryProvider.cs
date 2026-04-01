namespace SecurityRestApi.Keycloak;

/// <summary>
/// Odpowiednik Keycloak UserQueryProvider SPI — zawiera logikę wyszukiwania,
/// filtrowania i paginacji użytkowników wywoływaną przez User Storage Federation.
///
/// Obsługiwane metody:
///   - SearchForUsers(search, first, max)   — pełnotekstowe po username/email/imię/nazwisko
///   - SearchForUsers(params, first, max)   — filtrowanie po poszczególnych atrybutach
///   - GetUsersCount(search)                — liczba dopasowanych użytkowników
/// </summary>
public static class UserQueryProvider
{
    private static readonly StringComparison Cmp = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Wyszukiwanie pełnotekstowe: dopasowuje <paramref name="search"/> do
    /// username, email, firstName lub lastName.
    /// Paginacja: <paramref name="first"/> (offset, 0-based), <paramref name="max"/> (limit; -1 = wszystkie).
    /// </summary>
    public static IReadOnlyList<KeycloakUserDto> SearchForUsers(
        string? search,
        int first = 0,
        int max = -1)
    {
        var query = UserStore.Users.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => MatchesSearch(u, search));

        query = query.Skip(first);

        if (max >= 0)
            query = query.Take(max);

        return query.Select(KeycloakUserDto.FromUser).ToList();
    }

    /// <summary>
    /// Filtrowanie po atrybutach (odpowiednik Keycloak <c>searchForUser(Map params)</c>).
    /// Obsługiwane klucze: <c>username</c>, <c>email</c>, <c>firstName</c>, <c>lastName</c>.
    /// Parametry są łączone warunkiem AND. Pominięty/pusty parametr nie ogranicza wyników.
    /// </summary>
    public static IReadOnlyList<KeycloakUserDto> SearchForUsers(
        string? username,
        string? email,
        string? firstName,
        string? lastName,
        int first = 0,
        int max = -1)
    {
        var query = UserStore.Users.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(username))
            query = query.Where(u => u.Username.Contains(username, Cmp));

        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(u => u.Email != null && u.Email.Contains(email, Cmp));

        if (!string.IsNullOrWhiteSpace(firstName))
            query = query.Where(u => u.FirstName != null && u.FirstName.Contains(firstName, Cmp));

        if (!string.IsNullOrWhiteSpace(lastName))
            query = query.Where(u => u.LastName != null && u.LastName.Contains(lastName, Cmp));

        query = query.Skip(first);

        if (max >= 0)
            query = query.Take(max);

        return query.Select(KeycloakUserDto.FromUser).ToList();
    }

    /// <summary>
    /// Zwraca liczbę użytkowników pasujących do <paramref name="search"/>
    /// (pełnotekstowo). Gdy <paramref name="search"/> jest puste — zwraca
    /// całkowitą liczbę użytkowników.
    /// </summary>
    public static int GetUsersCount(string? search = null)
    {
        if (string.IsNullOrWhiteSpace(search))
            return UserStore.Users.Count;

        return UserStore.Users.Count(u => MatchesSearch(u, search));
    }

    // -------------------------------------------------------------------------

    private static bool MatchesSearch(User u, string search) =>
        u.Username.Contains(search, Cmp)
        || (u.Email      is not null && u.Email.Contains(search, Cmp))
        || (u.FirstName  is not null && u.FirstName.Contains(search, Cmp))
        || (u.LastName   is not null && u.LastName.Contains(search, Cmp));
}
