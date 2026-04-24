using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace SecurityRestApi.IdentityServer;

public class CustomProfileService : IProfileService
{
    public Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var subjectId = context.Subject.FindFirst("sub")?.Value;
        var user = subjectId is not null ? UserStore.FindById(subjectId) : null;

        if (user is null) return Task.CompletedTask;

        var claims = new List<Claim>
        {
            new("preferred_username", user.Username),
        };

        if (user.Email is not null)
            claims.Add(new("email", user.Email));

        if (user.FirstName is not null)
            claims.Add(new("given_name", user.FirstName));

        if (user.LastName is not null)
            claims.Add(new("family_name", user.LastName));

        if (user.FirstName is not null || user.LastName is not null)
            claims.Add(new("name", $"{user.FirstName} {user.LastName}".Trim()));

        context.IssuedClaims.AddRange(claims);
        return Task.CompletedTask;
    }

    public Task IsActiveAsync(IsActiveContext context)
    {
        var subjectId = context.Subject.FindFirst("sub")?.Value;
        if (subjectId is null) { context.IsActive = false; return Task.CompletedTask; }

        var localUser = UserStore.FindById(subjectId);
        // Local store IDs are integers ("1","2","3"). External subjects from Keycloak token exchange
        // are UUIDs (contain '-') — not in the local store but already authenticated by the grant validator.
        context.IsActive = localUser is not null || subjectId.Contains('-');
        return Task.CompletedTask;
    }
}
