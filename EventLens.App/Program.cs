using EventLens.App.Components;
using EventLens.App.Services;
using Microsoft.AspNetCore.DataProtection;

namespace EventLens.App
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(options => options.TimestampFormat = "HH:mm:ss ");
            builder.Logging.AddDebug();
            var configuredKeyPath = builder.Configuration["EventLens:DataProtectionPath"];
            var keyPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredKeyPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EventLens", "DataProtectionKeys")
                : Environment.ExpandEnvironmentVariables(configuredKeyPath));
            Directory.CreateDirectory(keyPath);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            var eventLensSettings = builder.Configuration
                .GetSection(EventLensUiOptions.SectionName)
                .Get<EventLensUiOptions>() ?? new EventLensUiOptions();
            builder.Services.Configure<EventLensUiOptions>(
                builder.Configuration.GetSection(EventLensUiOptions.SectionName));
            builder.Services.AddDataProtection()
                .SetApplicationName("EventLens.App")
                .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
                .ProtectKeysWithDpapi();
            builder.Services.AddHttpClient<EventLensApiClient>(client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["EventLens:WorkerApiBaseUrl"] ?? "http://127.0.0.1:5178");
                client.Timeout = eventLensSettings.WorkerApiTimeout;
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<Components.App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
