using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15);
    options.Cookie.HttpOnly = true;   // Add ".Cookie" here
    options.Cookie.IsEssential = true; // Add ".Cookie" here
});



var app = builder.Build();

// Seed roles and default admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

    string[] roles = new[] { "Admin", "User" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var adminEmail = builder.Configuration["AdminSettings:Email"] ?? "admin@local";
    var adminPassword = builder.Configuration["AdminSettings:Password"] ?? "Admin123!";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
    else
    {
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
        if (!await userManager.CheckPasswordAsync(adminUser, adminPassword))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(adminUser);
            await userManager.ResetPasswordAsync(adminUser, token, adminPassword);
        }
    }

    var customAdminProfile = await services.GetRequiredService<ApplicationDbContext>().UserProfiles.FirstOrDefaultAsync(u => u.Email == adminEmail);
    if (customAdminProfile == null && adminUser != null)
    {
        services.GetRequiredService<ApplicationDbContext>().UserProfiles.Add(new Users
        {
            Full_Name = "Administrator",
            Email = adminEmail,
            PasswordHash = adminUser.PasswordHash ?? string.Empty,
            Role = "Admin",
            IdentityUserId = adminUser.Id
        });
        await services.GetRequiredService<ApplicationDbContext>().SaveChangesAsync();
    }

    // Seed test user
    var testUserEmail = "lemuel@gmail.com";
    var testUserPassword = "Marc123!";
    var testUser = await userManager.FindByEmailAsync(testUserEmail);
    if (testUser == null)
    {
        testUser = new IdentityUser { UserName = testUserEmail, Email = testUserEmail, EmailConfirmed = true };
        var result = await userManager.CreateAsync(testUser, testUserPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(testUser, "User");
        }
    }
    else
    {
        if (!await userManager.IsInRoleAsync(testUser, "User"))
        {
            await userManager.AddToRoleAsync(testUser, "User");
        }
        if (!await userManager.CheckPasswordAsync(testUser, testUserPassword))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(testUser);
            await userManager.ResetPasswordAsync(testUser, token, testUserPassword);
        }
    }

    var customTestProfile = await services.GetRequiredService<ApplicationDbContext>().UserProfiles.FirstOrDefaultAsync(u => u.Email == testUserEmail);
    if (customTestProfile == null && testUser != null)
    {
        services.GetRequiredService<ApplicationDbContext>().UserProfiles.Add(new Users
        {
            Full_Name = "Test User",
            Email = testUserEmail,
            PasswordHash = testUser.PasswordHash ?? string.Empty,
            Role = "User",
            IdentityUserId = testUser.Id
        });
        await services.GetRequiredService<ApplicationDbContext>().SaveChangesAsync();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSession();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
