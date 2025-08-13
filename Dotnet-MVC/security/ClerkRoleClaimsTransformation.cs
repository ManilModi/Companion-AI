using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Json;

public class ClerkRoleClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity)
        {
            return Task.FromResult(principal ?? new ClaimsPrincipal());

        }

        var metadataClaim = identity.FindFirst("public_metadata");
        if (metadataClaim != null && !string.IsNullOrWhiteSpace(metadataClaim.Value))
        {
            try
            {
                using var doc = JsonDocument.Parse(metadataClaim.Value);
                var root = doc.RootElement;

                if (root.TryGetProperty("role", out var roleProp))
                {
                    var role = roleProp.GetString();
                    if (!string.IsNullOrEmpty(role) && !identity.HasClaim(ClaimTypes.Role, role))
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, role));
                    }
                }
            }
            catch (JsonException)
            {
                // Invalid JSON in public_metadata — ignore and continue
            }
        }

        return Task.FromResult(principal);
    }
}
