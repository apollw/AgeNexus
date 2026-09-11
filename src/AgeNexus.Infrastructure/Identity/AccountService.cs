using System.Security.Claims;
using System.Security.Cryptography;
using AgeNexus.Application.Evidence;
using AgeNexus.Domain.Common;
using AgeNexus.Domain.GameCatalog;
using AgeNexus.Domain.Players;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgeNexus.Infrastructure.Identity;

public sealed class AccountService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    AgeNexusDbContext database,
    IEvidenceObjectStorage storage,
    IConfiguration configuration,
    ILogger<AccountService> logger)
{
    public const string GoogleProvider = "Google";
    public const string AdministratorRole = "Administrator";
    public const string GoogleEmailVerifiedClaim = "urn:agenexus:google:email_verified";
    public const string GoogleHostedDomainClaim = "urn:agenexus:google:hosted_domain";

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
            var linkedAccount = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            var hasProfile = linkedAccount is not null && await database.PlayerProfiles.AsNoTracking()
                .AnyAsync(x => x.ApplicationUserId == linkedAccount.Id, cancellationToken);
            return AccountOperationResult.Success(requiresProfileSetup: !hasProfile);
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
            if (isNewUser && !AllowGooglePlayerLogin)
            {
                return AccountOperationResult.Failure(["RegistrationClosed"]);
            }

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

            var hasProfile = await database.PlayerProfiles.AsNoTracking()
                .AnyAsync(x => x.ApplicationUserId == user.Id, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            account = user;
            return AccountOperationResult.Success(requiresProfileSetup: !hasProfile);
        });

        if (operation.Succeeded)
        {
            await EnsureAdministratorRoleAsync(cancellationToken);
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

        var profile = await database.PlayerProfiles
            .AsNoTracking()
            .Where(x => x.ApplicationUserId == userId)
            .Select(x => new PublicProfile(x.Id, x.DisplayName, x.Bio, x.Location, x.AvatarUrl, null, null, null))
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var favorite = await (
            from selected in database.PlayerFavoriteFactions.AsNoTracking()
            join faction in database.Factions.AsNoTracking() on selected.FactionId equals faction.Id
            where selected.PlayerProfileId == profile.Id && selected.Priority == 1
            select new { faction.Id, faction.Name, faction.ImageUrl })
            .SingleOrDefaultAsync(cancellationToken);
        return favorite is null
            ? profile
            : profile with
            {
                FavoriteFactionId = favorite.Id,
                FavoriteFactionName = favorite.Name,
                FavoriteFactionImageUrl = favorite.ImageUrl
            };
    }

    public async Task<AccountOperationResult> UpdateProfileAsync(
        ClaimsPrincipal principal,
        string displayName,
        string? bio,
        string? location,
        Guid? favoriteFactionId,
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

        var normalizedName = displayName?.Trim() ?? string.Empty;
        if (await database.PlayerProfiles.AsNoTracking().AnyAsync(
                x => x.Id != profile.Id && x.DisplayName.ToLower() == normalizedName.ToLower(), cancellationToken))
        {
            return AccountOperationResult.Failure(["DuplicateDisplayName"]);
        }

        if (favoriteFactionId.HasValue && !await database.Factions.AsNoTracking()
                .AnyAsync(x => x.Id == favoriteFactionId.Value, cancellationToken))
        {
            return AccountOperationResult.Failure(["InvalidFaction"]);
        }

        try
        {
            profile.UpdatePublicProfile(normalizedName, bio, location, profile.AvatarUrl);
            var favorites = await database.PlayerFavoriteFactions
                .Where(x => x.PlayerProfileId == profile.Id).ToArrayAsync(cancellationToken);
            database.PlayerFavoriteFactions.RemoveRange(favorites);
            if (favoriteFactionId.HasValue)
            {
                database.PlayerFavoriteFactions.Add(new PlayerFavoriteFaction(
                    Guid.NewGuid(), profile.Id, favoriteFactionId.Value, 1));
            }
            await database.SaveChangesAsync(cancellationToken);
            return AccountOperationResult.Success();
        }
        catch (DomainRuleException)
        {
            return AccountOperationResult.Failure(["InvalidProfile"]);
        }
    }

    public bool IsGooglePlayerLoginOpen => AllowGooglePlayerLogin;

    public async Task<IReadOnlyCollection<ProfileFactionOption>> GetFactionOptionsAsync(
        CancellationToken cancellationToken = default) =>
        await database.Factions.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new ProfileFactionOption(x.Id, x.Name, x.ImageUrl))
            .ToArrayAsync(cancellationToken);

    public async Task<ProfileOnboardingView?> GetProfileOnboardingAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue)
        {
            return null;
        }

        var email = await userManager.Users.AsNoTracking().Where(x => x.Id == userId.Value)
            .Select(x => x.Email ?? x.UserName ?? "Conta Google")
            .SingleOrDefaultAsync(cancellationToken);
        if (email is null)
        {
            return null;
        }

        var linkedProfile = await database.PlayerProfiles.AsNoTracking()
            .Where(x => x.ApplicationUserId == userId.Value)
            .Select(x => new ClaimablePlayerProfile(x.Id, x.DisplayName, x.AvatarUrl))
            .SingleOrDefaultAsync(cancellationToken);
        var pending = await (
            from claim in database.PlayerProfileClaims.AsNoTracking()
            join profile in database.PlayerProfiles.AsNoTracking() on claim.PlayerProfileId equals profile.Id
            where claim.ApplicationUserId == userId.Value && claim.Status == PlayerProfileClaimStatus.Pending
            select new PendingProfileClaim(claim.Id, profile.Id, profile.DisplayName, claim.RequestedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
        var unavailableProfileIds = database.PlayerProfileClaims.AsNoTracking()
            .Where(x => x.Status == PlayerProfileClaimStatus.Pending)
            .Select(x => x.PlayerProfileId);
        IReadOnlyCollection<ClaimablePlayerProfile> available = linkedProfile is not null || pending is not null
            ? []
            : await database.PlayerProfiles.AsNoTracking()
                .Where(x => !x.ApplicationUserId.HasValue && !unavailableProfileIds.Contains(x.Id))
                .OrderBy(x => x.DisplayName)
                .Select(x => new ClaimablePlayerProfile(x.Id, x.DisplayName, x.AvatarUrl))
                .ToArrayAsync(cancellationToken);

        return new ProfileOnboardingView(userId.Value, email, linkedProfile, pending, available);
    }

    public async Task<AccountOperationResult> RequestProfileClaimAsync(
        ClaimsPrincipal principal,
        Guid playerProfileId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue)
        {
            return AccountOperationResult.Failure(["NotAuthenticated"]);
        }

        if (await database.PlayerProfiles.AsNoTracking()
            .AnyAsync(x => x.ApplicationUserId == userId.Value, cancellationToken))
        {
            return AccountOperationResult.Failure(["ProfileAlreadyLinked"]);
        }

        var existing = await database.PlayerProfileClaims.AsNoTracking().SingleOrDefaultAsync(
            x => x.ApplicationUserId == userId.Value && x.Status == PlayerProfileClaimStatus.Pending,
            cancellationToken);
        if (existing is not null)
        {
            return existing.PlayerProfileId == playerProfileId
                ? AccountOperationResult.Success()
                : AccountOperationResult.Failure(["PendingClaimExists"]);
        }

        var profileAvailable = await database.PlayerProfiles.AsNoTracking().AnyAsync(
            x => x.Id == playerProfileId && !x.ApplicationUserId.HasValue,
            cancellationToken);
        var alreadyRequested = await database.PlayerProfileClaims.AsNoTracking().AnyAsync(
            x => x.PlayerProfileId == playerProfileId && x.Status == PlayerProfileClaimStatus.Pending,
            cancellationToken);
        if (!profileAvailable || alreadyRequested)
        {
            return AccountOperationResult.Failure(["ProfileUnavailable"]);
        }

        try
        {
            database.PlayerProfileClaims.Add(new PlayerProfileClaim(
                Guid.NewGuid(), userId.Value, playerProfileId, DateTimeOffset.UtcNow));
            await database.SaveChangesAsync(cancellationToken);
            return AccountOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return AccountOperationResult.Failure(["ProfileUnavailable"]);
        }
    }

    public async Task<AccountOperationResult> CancelProfileClaimAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue)
        {
            return AccountOperationResult.Failure(["NotAuthenticated"]);
        }

        var claim = await database.PlayerProfileClaims.SingleOrDefaultAsync(
            x => x.ApplicationUserId == userId.Value && x.Status == PlayerProfileClaimStatus.Pending,
            cancellationToken);
        if (claim is null)
        {
            return AccountOperationResult.Success();
        }

        database.PlayerProfileClaims.Remove(claim);
        await database.SaveChangesAsync(cancellationToken);
        return AccountOperationResult.Success();
    }

    public async Task<AccountOperationResult> CreateOwnProfileAsync(
        ClaimsPrincipal principal,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue)
        {
            return AccountOperationResult.Failure(["NotAuthenticated"]);
        }

        if (await database.PlayerProfiles.AsNoTracking()
            .AnyAsync(x => x.ApplicationUserId == userId.Value, cancellationToken))
        {
            return AccountOperationResult.Failure(["ProfileAlreadyLinked"]);
        }

        if (await database.PlayerProfileClaims.AsNoTracking().AnyAsync(
            x => x.ApplicationUserId == userId.Value && x.Status == PlayerProfileClaimStatus.Pending,
            cancellationToken))
        {
            return AccountOperationResult.Failure(["PendingClaimExists"]);
        }

        var normalizedName = displayName?.Trim() ?? string.Empty;
        if (await database.PlayerProfiles.AsNoTracking().AnyAsync(
            x => x.DisplayName.ToLower() == normalizedName.ToLower(), cancellationToken))
        {
            return AccountOperationResult.Failure(["DuplicateDisplayName"]);
        }

        try
        {
            database.PlayerProfiles.Add(new PlayerProfile(Guid.NewGuid(), normalizedName, userId.Value));
            await database.SaveChangesAsync(cancellationToken);
            return AccountOperationResult.Success();
        }
        catch (DomainRuleException)
        {
            return AccountOperationResult.Failure(["InvalidProfile"]);
        }
    }

    public async Task<IReadOnlyCollection<ManagedProfileClaim>> GetPendingProfileClaimsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAdministratorAsync(principal, cancellationToken))
        {
            return [];
        }

        return await (
            from claim in database.PlayerProfileClaims.AsNoTracking()
            join profile in database.PlayerProfiles.AsNoTracking() on claim.PlayerProfileId equals profile.Id
            join user in userManager.Users.AsNoTracking() on claim.ApplicationUserId equals user.Id
            where claim.Status == PlayerProfileClaimStatus.Pending
            orderby claim.RequestedAtUtc
            select new ManagedProfileClaim(
                claim.Id, profile.Id, profile.DisplayName, user.Email ?? user.UserName ?? "Conta Google",
                claim.RequestedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<AccountOperationResult> DecideProfileClaimAsync(
        ClaimsPrincipal principal,
        Guid claimId,
        bool approve,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAdministratorAsync(principal, cancellationToken))
        {
            return AccountOperationResult.Failure(["NotAuthorized"]);
        }

        var administratorId = GetUserId(principal)!.Value;
        var claim = await database.PlayerProfileClaims.SingleOrDefaultAsync(
            x => x.Id == claimId && x.Status == PlayerProfileClaimStatus.Pending,
            cancellationToken);
        if (claim is null)
        {
            return AccountOperationResult.Failure(["ClaimNotFound"]);
        }

        try
        {
            if (approve)
            {
                var profile = await database.PlayerProfiles.SingleOrDefaultAsync(
                    x => x.Id == claim.PlayerProfileId, cancellationToken);
                if (profile is null || profile.ApplicationUserId.HasValue ||
                    await database.PlayerProfiles.AsNoTracking().AnyAsync(
                        x => x.ApplicationUserId == claim.ApplicationUserId, cancellationToken))
                {
                    return AccountOperationResult.Failure(["ProfileUnavailable"]);
                }

                profile.LinkToUser(claim.ApplicationUserId);
                claim.Approve(administratorId, DateTimeOffset.UtcNow);
            }
            else
            {
                claim.Reject(administratorId, DateTimeOffset.UtcNow);
            }

            await database.SaveChangesAsync(cancellationToken);
            return AccountOperationResult.Success();
        }
        catch (DomainRuleException)
        {
            return AccountOperationResult.Failure(["ClaimCannotBeDecided"]);
        }
    }

    public async Task<AccountOperationResult> UploadProfileAvatarAsync(
        ClaimsPrincipal principal,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue)
        {
            return AccountOperationResult.Failure(["NotAuthenticated"]);
        }

        var profile = await database.PlayerProfiles.SingleOrDefaultAsync(
            x => x.ApplicationUserId == userId.Value, cancellationToken);
        if (profile is null)
        {
            return AccountOperationResult.Failure(["ProfileNotFound"]);
        }

        if (!storage.IsConfigured)
        {
            return await SaveDatabaseAvatarAsync(profile, null, fileName, content, cancellationToken);
        }

        if (content.Length == 0 || content.Length > 2 * 1024 * 1024 ||
            !ProfileImage.TryIdentify(fileName, content, out var image))
        {
            return AccountOperationResult.Failure(["InvalidAvatar"]);
        }

        var objectKey = $"profiles/{profile.Id:N}/avatar-{Guid.NewGuid():N}.{image.Extension}";
        var previousObjectKey = profile.AvatarUrl is null ? null : storage.TryGetObjectKey(profile.AvatarUrl);
        try
        {
            await storage.UploadAsync(objectKey, image.ContentType, content, cancellationToken);
            profile.UpdateAvatar(storage.GetPublicUrl(objectKey));
            var databaseAvatar = await database.PlayerProfileAvatars.SingleOrDefaultAsync(
                x => x.PlayerProfileId == profile.Id, cancellationToken);
            if (databaseAvatar is not null)
            {
                database.PlayerProfileAvatars.Remove(databaseAvatar);
            }
            await database.SaveChangesAsync(cancellationToken);
            if (previousObjectKey is not null && previousObjectKey.StartsWith($"profiles/{profile.Id:N}/", StringComparison.Ordinal))
            {
                await TryDeleteStorageObjectAsync(previousObjectKey, cancellationToken);
            }
            return AccountOperationResult.Success();
        }
        catch (Exception exception) when (exception is HttpRequestException or DbUpdateException)
        {
            logger.LogError(exception, "Falha ao atualizar avatar do perfil {ProfileId}.", profile.Id);
            await TryDeleteStorageObjectAsync(objectKey, CancellationToken.None);
            return AccountOperationResult.Failure(["AvatarUploadFailed"]);
        }
    }

    public async Task<AccountOperationResult> RemoveProfileAvatarAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue)
        {
            return AccountOperationResult.Failure(["NotAuthenticated"]);
        }

        var profile = await database.PlayerProfiles.SingleOrDefaultAsync(
            x => x.ApplicationUserId == userId.Value, cancellationToken);
        if (profile is null)
        {
            return AccountOperationResult.Failure(["ProfileNotFound"]);
        }

        var objectKey = profile.AvatarUrl is null || !storage.IsConfigured
            ? null
            : storage.TryGetObjectKey(profile.AvatarUrl);
        var databaseAvatar = await database.PlayerProfileAvatars.SingleOrDefaultAsync(
            x => x.PlayerProfileId == profile.Id, cancellationToken);
        if (databaseAvatar is not null)
        {
            database.PlayerProfileAvatars.Remove(databaseAvatar);
        }
        profile.UpdateAvatar(null);
        await database.SaveChangesAsync(cancellationToken);
        if (objectKey is not null && objectKey.StartsWith($"profiles/{profile.Id:N}/", StringComparison.Ordinal))
        {
            await TryDeleteStorageObjectAsync(objectKey, cancellationToken);
        }
        return AccountOperationResult.Success();
    }

    public async Task<IReadOnlyCollection<ManagedPlayerProfile>> GetManagedProfilesAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAdministratorAsync(principal, cancellationToken))
        {
            return [];
        }

        return await database.PlayerProfiles.AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(x => new ManagedPlayerProfile(
                x.Id, x.DisplayName, x.Bio, x.Location, x.AvatarUrl, x.ApplicationUserId.HasValue))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<AccountOperationResult> CreateManagedProfileAsync(
        ClaimsPrincipal principal,
        string displayName,
        string? bio,
        string? location,
        string? avatarUrl,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAdministratorAsync(principal, cancellationToken))
        {
            return AccountOperationResult.Failure(["NotAuthorized"]);
        }

        var normalizedName = displayName?.Trim() ?? string.Empty;
        if (await database.PlayerProfiles.AnyAsync(
                x => x.DisplayName.ToLower() == normalizedName.ToLower(), cancellationToken))
        {
            return AccountOperationResult.Failure(["DuplicateDisplayName"]);
        }

        try
        {
            var profile = new PlayerProfile(Guid.NewGuid(), normalizedName);
            profile.UpdatePublicProfile(normalizedName, bio, location, avatarUrl);
            database.PlayerProfiles.Add(profile);
            await database.SaveChangesAsync(cancellationToken);
            return AccountOperationResult.Success();
        }
        catch (DomainRuleException)
        {
            return AccountOperationResult.Failure(["InvalidProfile"]);
        }
    }

    public async Task<AccountOperationResult> UpdateManagedProfileAsync(
        ClaimsPrincipal principal,
        Guid profileId,
        string displayName,
        string? bio,
        string? location,
        string? avatarUrl,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAdministratorAsync(principal, cancellationToken))
        {
            return AccountOperationResult.Failure(["NotAuthorized"]);
        }

        var profile = await database.PlayerProfiles.SingleOrDefaultAsync(
            x => x.Id == profileId && !x.ApplicationUserId.HasValue, cancellationToken);
        if (profile is null)
        {
            return AccountOperationResult.Failure(["ManagedProfileNotFound"]);
        }

        var normalizedName = displayName?.Trim() ?? string.Empty;
        if (await database.PlayerProfiles.AnyAsync(
                x => x.Id != profileId && x.DisplayName.ToLower() == normalizedName.ToLower(), cancellationToken))
        {
            return AccountOperationResult.Failure(["DuplicateDisplayName"]);
        }

        try
        {
            profile.UpdatePublicProfile(normalizedName, bio, location, avatarUrl);
            await database.SaveChangesAsync(cancellationToken);
            return AccountOperationResult.Success();
        }
        catch (DomainRuleException)
        {
            return AccountOperationResult.Failure(["InvalidProfile"]);
        }
    }

    private bool AllowGooglePlayerLogin =>
        configuration.GetValue("OperatingMode:AllowGooglePlayerLogin", true);

    public async Task EnsureAdministratorRoleAsync(CancellationToken cancellationToken = default)
    {
        if (!await roleManager.RoleExistsAsync(AdministratorRole))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(AdministratorRole)
            {
                Id = Guid.NewGuid()
            });
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException("The administrator role could not be created.");
            }
        }

        if ((await userManager.GetUsersInRoleAsync(AdministratorRole)).Count > 0)
        {
            return;
        }

        // Bootstrap is opt-in. Registration order must never grant administrative access.
        var bootstrapEmail = configuration["Security:BootstrapAdministratorEmail"]?.Trim();
        if (string.IsNullOrWhiteSpace(bootstrapEmail))
        {
            return;
        }

        var bootstrapUser = await userManager.FindByEmailAsync(bootstrapEmail);
        if (bootstrapUser is null || !bootstrapUser.EmailConfirmed)
        {
            return;
        }

        var assignment = await userManager.AddToRoleAsync(bootstrapUser, AdministratorRole);
        if (!assignment.Succeeded)
        {
            throw new InvalidOperationException("The configured account could not be assigned as administrator.");
        }
    }

    public async Task<bool> IsAdministratorAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue)
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        return user is not null && !await userManager.IsLockedOutAsync(user) &&
               await userManager.IsInRoleAsync(user, AdministratorRole);
    }

    public async Task<bool> IsAdministratorProfileAsync(
        Guid playerProfileId,
        CancellationToken cancellationToken = default)
    {
        var userId = await database.PlayerProfiles.AsNoTracking()
            .Where(x => x.Id == playerProfileId)
            .Select(x => x.ApplicationUserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!userId.HasValue)
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        return user is not null && !await userManager.IsLockedOutAsync(user) &&
               await userManager.IsInRoleAsync(user, AdministratorRole);
    }

    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true &&
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    private async Task TryDeleteStorageObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteAsync(objectKey, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Falha ao remover o objeto de avatar {ObjectKey}.", objectKey);
        }
    }

    private async Task<AccountOperationResult> SaveDatabaseAvatarAsync(
        PlayerProfile profile,
        ProfileImage? image,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        if (content.Length == 0 || content.Length > 2 * 1024 * 1024 ||
            image is null && !ProfileImage.TryIdentify(fileName, content, out image))
        {
            return AccountOperationResult.Failure(["InvalidAvatar"]);
        }

        var detectedImage = image!;
        try
        {
            var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            var avatar = await database.PlayerProfileAvatars.SingleOrDefaultAsync(
                x => x.PlayerProfileId == profile.Id, cancellationToken);
            if (avatar is null)
            {
                database.PlayerProfileAvatars.Add(new PlayerProfileAvatar(
                    profile.Id, detectedImage.ContentType, content, hash, DateTimeOffset.UtcNow));
            }
            else
            {
                avatar.Replace(detectedImage.ContentType, content, hash, DateTimeOffset.UtcNow);
            }

            profile.UpdateAvatar($"/profile-images/{profile.Id:D}?v={hash[..12]}");
            await database.SaveChangesAsync(cancellationToken);
            return AccountOperationResult.Success();
        }
        catch (Exception exception) when (exception is DomainRuleException or DbUpdateException)
        {
            logger.LogError(exception, "Falha ao salvar avatar no banco para o perfil {ProfileId}.", profile.Id);
            return AccountOperationResult.Failure(["AvatarUploadFailed"]);
        }
    }

    private sealed record ProfileImage(string Extension, string ContentType)
    {
        public static bool TryIdentify(string fileName, byte[] content, out ProfileImage image)
        {
            image = null!;
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (content.Length >= 8 && extension == ".png" &&
                content.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            {
                image = new ProfileImage("png", "image/png");
                return true;
            }

            if (content.Length >= 3 && extension is ".jpg" or ".jpeg" &&
                content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff)
            {
                image = new ProfileImage("jpg", "image/jpeg");
                return true;
            }

            if (content.Length >= 12 && extension == ".webp" &&
                content.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                content.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            {
                image = new ProfileImage("webp", "image/webp");
                return true;
            }

            return false;
        }
    }
}

public sealed record PublicProfile(
    Guid Id,
    string DisplayName,
    string? Bio,
    string? Location,
    string? AvatarUrl,
    Guid? FavoriteFactionId,
    string? FavoriteFactionName,
    string? FavoriteFactionImageUrl);

public sealed record ProfileFactionOption(Guid Id, string Name, string? ImageUrl);

public sealed record ClaimablePlayerProfile(Guid Id, string DisplayName, string? AvatarUrl);

public sealed record PendingProfileClaim(
    Guid ClaimId,
    Guid PlayerProfileId,
    string DisplayName,
    DateTimeOffset RequestedAtUtc);

public sealed record ProfileOnboardingView(
    Guid ApplicationUserId,
    string Email,
    ClaimablePlayerProfile? LinkedProfile,
    PendingProfileClaim? PendingClaim,
    IReadOnlyCollection<ClaimablePlayerProfile> AvailableProfiles);

public sealed record ManagedProfileClaim(
    Guid ClaimId,
    Guid PlayerProfileId,
    string DisplayName,
    string AccountEmail,
    DateTimeOffset RequestedAtUtc);

public sealed record ManagedPlayerProfile(
    Guid Id,
    string DisplayName,
    string? Bio,
    string? Location,
    string? AvatarUrl,
    bool HasUserAccount);

public sealed record AccountOperationResult(
    bool Succeeded,
    IReadOnlyCollection<string> ErrorCodes,
    bool RequiresProfileSetup = false)
{
    public static AccountOperationResult Success(bool requiresProfileSetup = false) =>
        new(true, [], requiresProfileSetup);
    public static AccountOperationResult Failure(IEnumerable<string> errors) => new(false, errors.ToArray());
}
