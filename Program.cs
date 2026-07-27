using fypSystem.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
if (builder.Environment.IsDevelopment())
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString,
            sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null
            )));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("Productiondb")
        ?? throw new InvalidOperationException("Connection string 'Productiondb' not found.");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString,
            sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null
            )));
}

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Auto migrate and seed
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;

    // Auto-apply migrations on startup
    var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();

    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

    // 1. Ensure Roles Exist
    string[] roleNames = { "Admin", "Student", "Supervisor", "Evaluator", "Committee" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // Helper to log errors
    void LogErrors(IdentityResult result, string email)
    {
        foreach (var error in result.Errors)
        {
            Console.WriteLine($"Error seeding user '{email}': {error.Description}");
        }
    }

    // Admin User
    string adminEmail = "admin@fyp.com";
    string adminPassword = "AdminPassword2!";
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var adminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded) await userManager.AddToRoleAsync(adminUser, "Admin");
        else LogErrors(result, adminEmail);
        Console.WriteLine($"Seeded Admin User: {adminEmail}");
    }
    else
    {
        Console.WriteLine($"Admin user '{adminEmail}' already exists.");
    }

    // Supervisor Users
    var supervisorData = new List<(string Email, string Password)>
    {
        ("supervisor@fyp.com", "SupervisorPassword!"),
        ("supervisor1@fyp.com", "Supervisor1Password!"),
        ("supervisor2@fyp.com", "Supervisor2Password!"),
        ("supervisor3@fyp.com", "Supervisor3Password!")
    };

    foreach (var (email, password) in supervisorData)
    {
        if (await userManager.FindByEmailAsync(email) == null)
        {
            var supervisorUser = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(supervisorUser, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(supervisorUser, "Supervisor");
                Console.WriteLine($"Seeded Supervisor User: {email}");
            }
            else LogErrors(result, email);
        }
        else Console.WriteLine($"Supervisor user '{email}' already exists.");
    }

    // Evaluator Users
    var evaluatorData = new List<(string Email, string Password)>
    {
        ("evaluator1@fyp.com", "Evaluator1Password!"),
        ("evaluator2@fyp.com", "Evaluator2Password!"),
        ("evaluator3@fyp.com", "Evaluator3Password!")
    };

    foreach (var (email, password) in evaluatorData)
    {
        if (await userManager.FindByEmailAsync(email) == null)
        {
            var evaluatorUser = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(evaluatorUser, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(evaluatorUser, "Evaluator");
                Console.WriteLine($"Seeded Evaluator User: {email}");
            }
            else LogErrors(result, email);
        }
        else Console.WriteLine($"Evaluator user '{email}' already exists.");
    }

    // Committee Users
    var committeeData = new List<(string Email, string Password)>
    {
        ("committee1@fyp.com", "Committee1Password!"),
        ("committee2@fyp.com", "Committee2Password!"),
        ("committee3@fyp.com", "Committee3Password!")
    };

    foreach (var (email, password) in committeeData)
    {
        if (await userManager.FindByEmailAsync(email) == null)
        {
            var committeeUser = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(committeeUser, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(committeeUser, "Committee");
                Console.WriteLine($"Seeded Committee User: {email}");
            }
            else LogErrors(result, email);
        }
        else Console.WriteLine($"Committee user '{email}' already exists.");
    }

    // Student Users
    var studentData = new List<(string Email, string Password)>
    {
        ("student@fyp.com", "StudentPassword!"),
        ("student1@fyp.com", "Student1Password!"),
        ("student2@fyp.com", "Student2Password!"),
        ("student3@fyp.com", "Student3Password!"),
        ("student4@fyp.com", "Student4Password!")
    };

    foreach (var (email, password) in studentData)
    {
        if (await userManager.FindByEmailAsync(email) == null)
        {
            var studentUser = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(studentUser, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(studentUser, "Student");
                Console.WriteLine($"Seeded Student: {email}");
            }
            else LogErrors(result, email);
        }
        else Console.WriteLine($"Student user '{email}' already exists.");
    }
}

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.Run();
