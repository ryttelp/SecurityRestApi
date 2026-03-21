using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace SecurityRestApi.IdentityServer;

public class CustomResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
{
    public Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
    {
        if (UserStore.ValidateCredentials(context.UserName, context.Password))
        {
            var user = UserStore.FindByUsername(context.UserName)!;
            context.Result = new GrantValidationResult(subject: user.Id, authenticationMethod: "password");
        }
        else
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Nieprawidłowe dane logowania");
        }

        return Task.CompletedTask;
    }
}
