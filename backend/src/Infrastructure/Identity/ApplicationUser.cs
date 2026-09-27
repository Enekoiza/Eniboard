using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

/// <summary>
/// The single seeded user for this personal, single-user Kanban app. No additional
/// profile fields are needed for v1.
/// </summary>
public sealed class ApplicationUser : IdentityUser;
