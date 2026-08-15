using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Models;
using RoadSafety.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<OfficerService>();
builder.Services.AddScoped<NumberGenerator>();
builder.Services.AddScoped<RoleService>();

// Lets fetch() send the antiforgery token in a header, so the row actions
// can post without a full form round-trip.
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    // Keyed on the permission rather than the role's name, so a role added
    // later works without touching this. The services re-check the scope
    // themselves; these keep the pages out of reach in the first place.
    options.AddPolicy("ManageOfficers", p => p.RequireClaim("perm", Permissions.ManageOfficers));
    options.AddPolicy("AssignRoles", p => p.RequireClaim("perm", Permissions.AssignRoles));
    options.AddPolicy("ManageRoles", p => p.RequireClaim("perm", Permissions.ManageRoles));
});

var app = builder.Build();

await SeedData.InitialiseAsync(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

// Order matters: authentication establishes who you are,
// authorization then decides what you may reach.
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
