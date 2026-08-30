using Microsoft.AspNetCore.DataProtection;
using AgeNexus.Infrastructure;
using AgeNexus.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.TimestampFormat = "HH:mm:ss ");

builder.Services.AddAgeNexusInfrastructure();
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

app.MapRazorComponents<App>();

app.Run();

public partial class Program;
