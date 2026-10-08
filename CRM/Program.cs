var builder = WebApplication.CreateBuilder(args);

// Note: CRM.api isn't actually run as a web app in this project - Desktop
// references its Controllers directly, in-process. This Program.cs is left
// minimal/unused; remove AddControllers/OpenApi too if this project is ever
// trimmed down to a pure class library.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
