using System.Security.Claims;
using AgeNexus.Domain.Common;
using AgeNexus.Domain.Players;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AgeNexus.Infrastructure.Identity;

public sealed class AccountService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AgeNexusDbContext database)
{
    public const string GoogleProvider = "Google";
    public const string GoogleEmailVerifiedClaim = "urn:agenexus:google:email_verified";
    public const string GoogleHostedDomainClaim = "urn:agenexus:google:hosted_domain";

    public async Task<AccountOperationResult> RegisterAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim();
        var strategy = database.Database.CreateExecutionStrategy();
        ApplicationUser? createdUser = null;

        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var user = new ApplicationUser(normalizedEmail);
            var identityResult = await userManager.CreateAsync(user, password);

            if (!identityResult.Succeeded)
            {
                return AccountOperationResult.Failure(identityResult.Errors.Select(x => x.Code));
            }

            try
            {
                database.PlayerProfiles.Add(new PlayerProfile(Guid.NewGuid(), displayName, user.Id));
            }
            catch (DomainRuleException)
            {
                return AccountOperationResult.Failure(["InvalidProfile"]);
            }

            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            createdUser = user;
            return AccountOperationResult.Success();
        });

        if (result.Succeeded)
        {
            await signInManager.SignInAsync(createdUser!, isPersistent: false);
        }

        return result;
    }

    public async Task<AccountOperationResult> LoginAsync(string email, string password, bool rememberMe)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return AccountOperationResult.Failure(["InvalidCredentials"]);
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return AccountOperationResult.Failure(["LockedOut"]);
        }

        if (!result.Succeeded)
        {
            return AccountOperationResult.Failure(["InvalidCredentials"]);
        }

        await signInManager.SignInAsync(user, rememberMe);
        return AccountOperationResult.Success();
    }

    public Task LogoutAsync() => signInManager.SignOutAsync();

    public AuthenticationProperties ConfigureExternalLogin(string redirectUrl) =>
        signInManager.ConfigureExternalAuthenticationProperties(GoogleProvider, redirectUrl);

    public async Task<AccountOperationResult> CompleteGoogleLoginAsync(CancellationToken cancellationToken)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null || info.LoginProvider != GoogleProvider)
        {
            return AccountOperationResult.Failure(["ExternalLoginUnavailable"]);
        }

        var signIn = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);
        if (signIn.Succeeded)
        {
            return AccountOperationResult.Success();
        }

        if (signIn.IsLockedOut)
        {
            return AccountOperationResult.Failure(["LockedOut"]);
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email)?.Trim();
        var verifiedEmail = string.Equals(
            info.Principal.FindFirstValue(GoogleEmailVerifiedClaim),
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(email) || !verifiedEmail)
        {
            return AccountOperationResult.Failure(["ExternalEmailNotVerified"]);
        }

        var strategy = database.Database.CreateExecutionStrategy();
        ApplicationUser? account = null;
        var operation = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var user = await userManager.FindByEmailAsync(email);
            var isNewUser = user is null;
            var googleIsAuthoritative = email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase) ||
                                        !string.IsNullOrWhiteSpace(
                                            info.Principal.FindFirstValue(GoogleHostedDomainClaim));
            if (!isNewUser && !googleIsAuthoritative)
            {
                return AccountOperationResult.Failure(["ExternalAccountLinkRequired"]);
            }

            user ??= new ApplicationUser(email);
            user.ConfirmEmailFromTrustedProvider();

            var identityResult = isNewUser
                ? await userManager.CreateAsync(user)
                : await userManager.UpdateAsync(user);
            if (!identityResult.Succeeded)
            {
                return AccountOperationResult.Failure(identityResult.Errors.Select(x => x.Code));
            }

            identityResult = await userManager.AddLoginAsync(user, info);
            if (!identityResult.Succeeded)
            {
                return AccountOperationResult.Failure(identityResult.Errors.Select(x => x.Code));
            }

            var hasProfile = await database.PlayerProfiles
                .AnyAsync(x => x.ApplicationUserId == user.Id, cancellationToken);
            if (!hasProfile)
            {
                var displayName = NormalizeExternalDisplayName(
                    info.Principal.FindFirstValue(ClaimTypes.Name),
                    email);
                database.PlayerProfiles.Add(new PlayerProfile(Guid.NewGuid(), displayName, user.Id));
                await database.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            account = user;
            return AccountOperationResult.Success();
        });

        if (operation.Succeeded)
        {
            await signInManager.SignInAsync(account!, isPersistent: false);
        }

        return operation;
    }

    public async Task<PublicProfile?> GetProfileAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return null;
        }

        return await database.PlayerProfiles
            .AsNoTracking()
            .Where(x => x.ApplicationUserId == userId)
            .Select(x => new PublicProfile(x.Id, x.DisplayName, x.Bio, x.Location, x.AvatarUrl))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AccountOperationResult> UpdateProfileAsync(
        ClaimsPrincipal principal,
        string displayName,
        string? bio,
        string? location,
        string? avatarUrl,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return AccountOperationResult.Failure(["NotAuthenticated"]);
        }

        var profile = await database.PlayerProfiles
            .SingleOrDefaultAsync(x => x.ApplicationUserId == userId, cancellationToken);
        if (profile is null)
        {
            return AccountOperationResult.Failure(["ProfileNotFound"]);
        }

        try
        {
            profile.UpdatePublicProfile(displayName, bio, location, avatarUrl);
            await database.SaveChangesAsync(cancellationToken);
            return AccountOperationResult.Success();
        }
        catch (DomainRuleException)
        {
            return AccountOperationResult.Failure(["InvalidProfile"]);
        }
    }

    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    private static string NormalizeExternalDisplayName(string? name, string email)
    {
        var fallback = email.Split('@', 2)[0];
        var normalized = string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
        return normalized.Length <= 100 ? normalized : normalized[..100];
    }
}

public sealed record PublicProfile(Guid Id, string DisplayName, string? Bio, string? Location, string? AvatarUrl);

public sealed record AccountOperationResult(bool Succeeded, IReadOnlyCollection<string> ErrorCodes)
{
    public static AccountOperationResult Success() => new(true, []);
    public static AccountOperationResult Failure(IEnumerable<string> errors) => new(false, errors.ToArray());
}
