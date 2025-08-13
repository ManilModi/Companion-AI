using DotnetMVCApp.Data;
using Microsoft.EntityFrameworkCore;
using Clerk.Net.AspNetCore.Security;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Clerk settings from appsettings.json
var clerkAuthority = builder.Configuration["Clerk:Authority"];
var clerkAuthorizedParty = builder.Configuration["Clerk:AuthorizedParty"];

// Add MVC and DB
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Clerk authentication
builder.Services
    .AddAuthentication(ClerkAuthenticationDefaults.AuthenticationScheme)
    .AddClerkAuthentication(options =>
    {
        options.Authority = clerkAuthority ?? throw new InvalidOperationException("Clerk Authority not configured.");
        options.AuthorizedParty = clerkAuthorizedParty ?? throw new InvalidOperationException("Clerk AuthorizedParty not configured.");
    });

// Transform Clerk's public_metadata to Role claims
builder.Services.AddScoped<IClaimsTransformation, ClerkRoleClaimsTransformation>();

// Authorization policies (optional)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CandidateOnly", policy => policy.RequireRole("Candidate"));
    options.AddPolicy("HROnly", policy => policy.RequireRole("HR"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

