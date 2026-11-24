using Logistics.Web;
using Logistics.Web.Components;
using Logistics.Web.Models;
using Logistics.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------- Интеграция с API ATI.SU ----------
var atiOptions = builder.Configuration.GetSection("Ati").Get<AtiOptions>() ?? new AtiOptions();
builder.Services.AddSingleton(atiOptions);

// Диагностика: откуда реально читаются настройки ATI (значение токена не выводится)
Console.WriteLine("[ATI] Папка приложения: " + builder.Environment.ContentRootPath);
Console.WriteLine("[ATI] Окружение: " + builder.Environment.EnvironmentName);
Console.WriteLine("[ATI] ExpiresAt = " + builder.Configuration["Ati:ExpiresAt"]);
foreach (var key in new[] { "Ati:ExpiresAt", "Ati:Token" })
{
    var source = ((IConfigurationRoot)builder.Configuration).Providers
        .Reverse()
        .FirstOrDefault(p => p.TryGet(key, out _));
    Console.WriteLine("[ATI] " + key + " взят из: " + (source?.ToString() ?? "нигде не задан"));
}

builder.Services.AddSingleton<AtiService>(sp =>
{
    var opt = sp.GetRequiredService<AtiOptions>();
    return new AtiService(new HttpClient(), opt);
});

builder.Services.AddHostedService<AtiPoller>();
// ---------------------------------------------

var app = builder.Build();

SqliteStore.Initialize();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();