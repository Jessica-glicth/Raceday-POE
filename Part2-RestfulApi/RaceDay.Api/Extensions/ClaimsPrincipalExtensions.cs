using System.Security.Claims;

namespace RaceDay.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The ID (OrganiserID or ParticipantID) of the logged-in user, taken from the session cookie.</summary>
    public static int GetUserId(this ClaimsPrincipal user)
        => int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? throw new InvalidOperationException("No user id claim."));
}
