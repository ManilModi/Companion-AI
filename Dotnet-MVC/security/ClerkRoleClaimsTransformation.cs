// Role claim transformation service
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Json;

public class ClerkRoleClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity;

        var metadataClaim = identity.FindFirst("public_metadata");
        if (metadataClaim != null)
        {
            var metadata = JsonDocument.Parse(metadataClaim.Value).RootElement;
            if (metadata.TryGetProperty("role", out var roleProp))
            {
                var role = roleProp.GetString();
                if (!string.IsNullOrEmpty(role) && !identity.HasClaim(ClaimTypes.Role, role))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }
        }

        return Task.FromResult(principal);
    }
}
