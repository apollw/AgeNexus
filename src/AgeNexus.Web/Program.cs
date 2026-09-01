using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using AgeNexus.Infrastructure;
using AgeNexus.Application;
using AgeNexus.Infrastructure.Persistence;
using AgeNexus.Web.Components;
using AgeNexus.Web.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.TimestampFormat = "HH:mm:ss ");

builder.Services.AddAgeNexusApplication();
builder.Services.AddAgeNexusInfrastructure(builder.Configuration);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticationStateProvider, HttpContextAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

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
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health/database", async (AgeNexusDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "healthy", database = "postgresql" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.MapAgeNexusAccountEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
