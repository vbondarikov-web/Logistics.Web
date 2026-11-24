using System.Text.Json;
using Logistics.Web.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logistics.Web.Services
{
    /// Фоновый опрос ATI: новые встречные предложения попадают в AtiInbox.
    public class AtiPoller : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<AtiPoller> _log;
        private readonly HashSet<string> _seen = new();
        private bool _firstRun = true;

        public AtiPoller(IServiceScopeFactory scopes, ILogger<AtiPoller> log)
        {
            _scopes = scopes;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = 20;

                try
                {
                    using var scope = _scopes.CreateScope();
                    var ati = scope.ServiceProvider.GetRequiredService<AtiService>();
                    var opt = scope.ServiceProvider.GetService<AtiOptions>();

                    if (opt != null) delay = Math.Max(5, opt.PollingSeconds);

                    var routeSet = opt == null ||
                                   !string.IsNullOrWhiteSpace(opt.Endpoints.CounterOffersAll);

                    if (ati.IsConfigured && routeSet)
                    {
                        Process(await ati.GetCounterOffersRawAsync());
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "ATI: не удалось опросить встречные предложения");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void Process(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;

                var id = Text(e, "id") ?? Text(e, "ResponseId");
                if (string.IsNullOrEmpty(id) || !_seen.Add(id)) continue;

                // при первом проходе только запоминаем уже существующие предложения
                if (_firstRun) continue;

                var number = Text(e, "cargo_number") ?? Text(e, "LoadNumber") ?? "?";
                var firm = Text(e, "FirmName") ?? Text(Prop(e, "firm_info"), "full_firm_name") ?? "";

                var prices = new List<string>();
                AddPrice(prices, e, "nds_price", "NdsPrice", "с НДС");
                AddPrice(prices, e, "not_nds_price", "NotNdsPrice", "без НДС");
                AddPrice(prices, e, "price", "Price", "нал");

                AtiInbox.Push(DateTime.Now,
                    "Встречное предложение по грузу № " + number,
                    (firm + " " + string.Join(" / ", prices)).Trim());
            }

            _firstRun = false;
        }

        private static void AddPrice(List<string> list, JsonElement e, string a, string b, string label)
        {
            var v = Prop(e, a);
            if (v.ValueKind != JsonValueKind.Number) v = Prop(e, b);

            if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) && d > 0)
            {
                list.Add(d.ToString("N0") + " " + label);
            }
        }

        private static JsonElement Prop(JsonElement e, string name)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v : default;

        private static string? Text(JsonElement e, string name)
        {
            var v = Prop(e, name);

            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null
            };
        }
    }
}