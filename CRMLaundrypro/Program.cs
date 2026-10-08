using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CRM.infrastructure.data;
using CRM.domain.Entities.Tenant;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews(); // lets .cshtml Views work
builder.Services.AddOpenApi();

// Connect to the same database CleanWaveCRM.Desktop uses.
builder.Services.AddDbContext<LaundryShopDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity for this web app, if it's ever actually run standalone.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<LaundryShopDbContext>()
    .AddDefaultTokenProviders();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // needed to serve CSS/JS/images for Views

app.UseAuthentication(); // must come BEFORE UseAuthorization
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();