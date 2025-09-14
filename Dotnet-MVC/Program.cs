using AutoMapper;
using CloudinaryDotNet;
using DotnetMVCApp.Attributes;
using DotnetMVCApp.Data;
using DotnetMVCApp.Models;
using DotnetMVCApp.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ------------------- Add MVC and DB -------------------
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ------------------- HttpClient -------------------
builder.Services.AddHttpClient();

// ------------------- Repositories -------------------
builder.Services.AddScoped<IUserRepo, UserRepo>();
builder.Services.AddScoped<IJobRepo, JobRepo>();
builder.Services.AddScoped<IInterviewrepo, InterviewRepo>();
builder.Services.AddScoped<IFeedbackrepo, FeedbackRepo>();
builder.Services.AddScoped<IUserJobRepo, UserJobRepo>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ------------------- AutoMapper -------------------
builder.Services.AddAutoMapper(typeof(MappingProfile));

// ------------------- Cloudinary -------------------
builder.Services.AddSingleton(x =>
{
    var config = builder.Configuration.GetSection("Cloudinary");
    return new Cloudinary(
        new Account(
            config["CloudName"],
            config["ApiKey"],
            config["ApiSecret"]
        )
    );
});

// ------------------- Cookie Authentication -------------------
builder.Services.AddAuthentication("MyCookieAuth")
    .AddCookie("MyCookieAuth", options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(15);
        options.SlidingExpiration = true;
    });

// ------------------- Session -------------------
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".DotnetMVCApp.Session";
    options.IdleTimeout = TimeSpan.FromDays(15);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ------------------- Email Service (SMTP / Gmail) -------------------
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings")
);
builder.Services.AddTransient<EmailService>();

var app = builder.Build();

// ------------------- Middleware -------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    // app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

// **Session must be before authentication & authorization**
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// ------------------- Routes -------------------
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
