using DotnetMVCApp.Data;
using DotnetMVCApp.Models;
using DotnetMVCApp.Repositories;
using Microsoft.EntityFrameworkCore;
using AutoMapper;

var builder = WebApplication.CreateBuilder(args);


// Add MVC and DB
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient();

// Add User repository
builder.Services.AddScoped<IUserRepo, UserRepo>();
builder.Services.AddScoped<IJobRepo, JobRepo>();
builder.Services.AddScoped<IInterviewrepo, InterviewRepo>();
builder.Services.AddScoped<IFeedbackrepo,FeedbackRepo>();
builder.Services.AddScoped<IUserJobRepo, UserJobRepo>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();


builder.Services.AddAutoMapper(typeof(MappingProfile));


builder.Services.AddSession();


builder.Services.AddSingleton(x =>
{
    var config = builder.Configuration.GetSection("Cloudinary");
    return new CloudinaryDotNet.Cloudinary(
        new CloudinaryDotNet.Account(
            config["CloudName"],
            config["ApiKey"],
            config["ApiSecret"]
        )
    );
});



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
    app.UseDeveloperExceptionPage();
    // app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();
app.UseSession();
// Enable Cookie Auth
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
