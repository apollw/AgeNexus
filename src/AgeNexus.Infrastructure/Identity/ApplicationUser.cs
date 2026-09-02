using Microsoft.AspNetCore.Identity;

namespace AgeNexus.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    private ApplicationUser()
    {
    }

    public ApplicationUser(string email)
    {
        Id = Guid.NewGuid();
        UserName = email.Trim();
        Email = email.Trim();
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public DateTimeOffset CreatedAtUtc { get; private set; }
}

