using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using AgeNexus.Infrastructure;
using AgeNexus.Infrastructure.Persistence;
using AgeNexus.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.TimestampFormat = "HH:mm:ss ");

builder.Services.AddAgeNexusInfrastructure(builder.Configuration);
builder.Services.AddRazorComponents();

if (builder.Environment.IsDevelopment())
{
    var keyDirectory = new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".keys"));
    builder.Services
        .AddDataProtection()
        .SetApplicationName("AgeNexus")
        .PersistKeysToFileSystem(keyDirectory);
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/health/database", async (AgeNexusDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "healthy", database = "postgresql" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.MapRazorComponents<App>();

app.Run();

public partial class Program;
