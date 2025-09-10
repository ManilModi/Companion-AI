using DotnetMVCApp.Data;
using DotnetMVCApp.Models;
using DotnetMVCApp.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add MVC and DB
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add User repository
builder.Services.AddScoped<IUserRepo, UserRepo>();
builder.Services.AddScoped<IJobRepo, JobRepo>();
builder.Services.AddScoped<IInterviewrepo, InterviewRepo>();
builder.Services.AddScoped<IFeedbackrepo,FeedbackRepo>();
builder.Services.AddScoped<IUserJobRepo, UserJobRepo>();



// Cookie Authentication (for MVC)
builder.Services.AddAuthentication("MyCookieAuth")
    .AddCookie("MyCookieAuth", options =>
    {
        options.LoginPath = "/Account/Login"; // Redirect here if not logged in
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

// Enable Cookie Auth
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
