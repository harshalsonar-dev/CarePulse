using CarePulse.Data;
using CarePulse.Models.Entities;
using CarePulse.Services;
using CarePulse.Services.Background;
using CarePulse.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration (SQL Server with resilient SQLite fallback)
var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer";
var sqlServerConn = builder.Configuration.GetConnectionString("DefaultConnection") 
                    ?? "Server=localhost;Database=CarePulseDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";
var sqliteConn = builder.Configuration.GetConnectionString("SqliteConnection") 
                 ?? "Data Source=CarePulse.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(sqliteConn);
    }
    else
    {
        options.UseSqlServer(sqlServerConn, sqlServerOptions =>
        {
            sqlServerOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        });
    }
});

// 2. Identity Configuration with strict RBAC
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "CarePulse.Auth";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// 3. Application Services
builder.Services.AddScoped<ISchedulingService, SchedulingService>();
builder.Services.AddScoped<IPrescriptionPdfService, PrescriptionPdfService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAzureBlobStorageService, AzureBlobStorageService>();

// 4. Background Services (Appointment confirmation, reminders, cancellation alerts)
builder.Services.AddHostedService<AppointmentNotificationBackgroundService>();

// 5. MVC Controllers & Views
builder.Services.AddControllersWithViews();

// 6. RESTful Web API & Swagger Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CarePulse Healthcare & Clinical Appointment API",
        Version = "v1",
        Description = "Automated smart doctor scheduling, real-time availability calendars, Electronic Health Records (EHR), and clinical workflow APIs.",
        Contact = new OpenApiContact
        {
            Name = "CarePulse Clinical Engineering Team",
            Email = "support@carepulse.com"
        }
    });
});

var app = builder.Build();

// Seed Database automatically on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        await DbInitializer.SeedAsync(services);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database seeding failed on primary provider. If SQL Server is inaccessible, switch 'DatabaseProvider' to 'Sqlite' in appsettings.json.");
    }
}

// HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CarePulse Clinical API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
