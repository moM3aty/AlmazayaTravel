using Microsoft.EntityFrameworkCore; // Required for DbContext
using AlmazayaTravel.Data;          // Namespace for ApplicationDbContext
using Microsoft.AspNetCore.Authentication.Cookies; // For basic cookie authentication

var builder = WebApplication.CreateBuilder(args);

// *** 1. Add services to the container. ***

// Add MVC services
builder.Services.AddControllersWithViews();

// Add DbContext service
// Get the connection string from appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Register ApplicationDbContext and configure it to use SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add Session services (needed for TempData which uses session by default)
builder.Services.AddDistributedMemoryCache(); // Use memory cache for session storage (simple setup)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Set session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add basic Cookie Authentication services (placeholder for Identity)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Login"; // Redirect path if user is not logged in
        options.AccessDeniedPath = "/Home/Index"; // Optional: Redirect path if access is denied
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });

// *** Add HttpClientFactory service ***
builder.Services.AddHttpClient(); // <--- This line is added to register IHttpClientFactory

var app = builder.Build();

// *** 2. Configure the HTTP request pipeline. ***

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    // Optional: Add developer exception page for easier debugging in development
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Serve files from wwwroot

app.UseRouting();

app.UseSession(); // Enable session middleware (must be before Authentication/Authorization)

app.UseAuthentication(); // Enable authentication middleware
app.UseAuthorization(); // Enable authorization middleware


// Define the default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

