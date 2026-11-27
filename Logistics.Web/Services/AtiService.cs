using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static System.Net.WebRequestMethods;

namespace Logistics.Web.Models
{
    public class AtiNotification
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Date { get; set; } = DateTime.Now;
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
    }

    /// Локальная лента событий АТИ.
    public static class AtiInbox
    {
        private const int MaxItems = 200;
        private static readonly object _lock = new();
        private static readonly List<AtiNotification> _items = new();

        public static void Push(DateTime date, string title, string text)
        {
            lock (_lock)
            {
                _items.Insert(0, new AtiNotification
                {
                    Date = date == default ? DateTime.Now : date,
                    Title = title ?? "",
                    Text = text ?? ""
                });

                if (_items.Count > MaxItems)
                    _items.RemoveRange(MaxItems, _items.Count - MaxItems);

                try
                {
                    SqliteStore.SaveAtiNotifications(_items);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[ATI] Не удалось сохранить уведомления: " + ex.Message);
                }
            }
        }

        internal static void Restore(IEnumerable<AtiNotification> items)
        {
            lock (_lock)
            {
                _items.Clear();
                _items.AddRange(items.Take(MaxItems));
            }
        }

        public static List<AtiNotification> Snapshot()
        {
            lock (_lock)
            {
                return _items.ToList();
            }
        }
    }

    /// Подсказка автокомплита городов.
    public class AtiCitySuggestion
    {
        public int CityId { get; set; }
        public int RegionId { get; set; }
        public int CountryId { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
    }

    /// Контакт (сотрудник) своей фирмы.
    public class AtiContact
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Position { get; set; } = "";
        public bool IsVisible { get; set; } = true;
    }

    /// Аккаунт, которому принадлежит токен.
    public class AtiAccount
    {
        public string AtiId { get; set; } = "";
        public string FirmName { get; set; } = "";
        public int CurrentContactId { get; set; }
        public List<AtiContact> Contacts { get; set; } = new();
    }

    /// Тип кузова из справочника carTypes.
    /// Id — бит маски (для v1.0), TypeId — значение для body_types (v2).
    public class AtiCarType
    {
        public ulong Id { get; set; }
        public int TypeId { get; set; }
        public string Name { get; set; } = "";
    }

    public static class AtiBoards
    {
        /// Общая площадка — биржа ATI.SU.
        public const string CommonId = "a0a0a0a0a0a0a0a0a0a0a0a0";
        public const string CommonName = "Общая площадка ATI.SU";
    }

    /// Площадка из GET v2/boards/public/boards/list.
    public class AtiBoard
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Color { get; set; } = "";
        public string OwnerAtiId { get; set; } = "";
        public string Direction { get; set; } = "";
        public bool CanAdd { get; set; }
        public bool CanView { get; set; }
        public bool IsOwner { get; set; }
        public int ContentCount { get; set; }
        public int ParticipantsCount { get; set; }
    }

    /// Опубликованный груз из GET v1.0/loads — данные для карточки.
    public class AtiLoad
    {
        public string Id { get; set; } = "";
        public string Number { get; set; } = "";

        public int LoadingCityId { get; set; }
        public string LoadingStreet { get; set; } = "";
        public int UnloadingCityId { get; set; }
        public string UnloadingStreet { get; set; } = "";

        public string CargoName { get; set; } = "";
        public decimal Weight { get; set; }
        public decimal Volume { get; set; }
        public ulong CarTypeMask { get; set; }

        public int DateType { get; set; }
        public DateTime? FirstDate { get; set; }
        public DateTime? LastDate { get; set; }

        public bool FixedRate { get; set; }
        public bool Torg { get; set; }
        public decimal RateSum { get; set; }
        public decimal SumWithNds { get; set; }
        public decimal SumWithoutNds { get; set; }

        public int Distance { get; set; }
        public int OfferCount { get; set; }
        public DateTime? AddedAt { get; set; }
        public string Note { get; set; } = "";

        public bool CanBeEdited { get; set; }
        public bool CanBeRenewed { get; set; }
        public string RenewRestriction { get; set; } = "";
        public List<string> BoardIds { get; set; } = new();
        public int ContactId { get; set; }
        public DateTime? ArchiveDate { get; set; }
        public string ArchiveReason { get; set; } = "";
        public bool CanBeRestored { get; set; }
    }

    /// Комментарий своей фирмы к архивному грузу.
    public class AtiLoadComment
    {
        public string Id { get; set; } = "";
        public string LoadId { get; set; } = "";
        public string Message { get; set; } = "";
        public int ContactId { get; set; }
        public DateTime? Date { get; set; }
    }

    /// Черновик груза. Идентификаторы — числовые ID из справочников ATI.SU.
    public class AtiCargoDraft
    {
        // Загрузка
        public int LoadingCityId { get; set; }
        public int LoadingRegionId { get; set; }
        public int LoadingCountryId { get; set; } = 1;
        public string LoadingCityName { get; set; } = "";
        public string LoadingAddress { get; set; } = "";
        public DateTime LoadingDate { get; set; } = DateTime.Today;
        public List<string> BoardIds { get; set; } = new() { AtiBoards.CommonId };
        /// now, 15m, 30m, 1h, 3h, 6h
        public string PublicationMode { get; set; } = "now";

        // Разгрузка
        public int UnloadingCityId { get; set; }
        public int UnloadingRegionId { get; set; }
        public int UnloadingCountryId { get; set; } = 1;
        public string UnloadingCityName { get; set; } = "";
        public string UnloadingAddress { get; set; } = "";

        // Груз
        public string CargoName { get; set; } = "";
        public int CargoId { get; set; }
        public decimal Weight { get; set; }
        public decimal Volume { get; set; }

        // Машина
        // Машина
        public List<int> BodyTypeIds { get; set; } = new();

        public HashSet<int> BodyLoadingTypeIds { get; set; } = new();
        public HashSet<int> BodyUnloadingTypeIds { get; set; } = new();

        public bool IsAllBodyLoadingRequired { get; set; }
        public bool IsAllBodyUnloadingRequired { get; set; }

        public int TrucksCount { get; set; } = 1;

        public string LoadType { get; set; } = "ftl";
        public decimal RequiredCapacity { get; set; } = 20;

        // Оплата
        public string PaymentType { get; set; } = "without-bargaining";
        public int CurrencyType { get; set; } = 1;
        public decimal? RateWithoutVat { get; set; }
        public decimal? Cash { get; set; }
        public decimal? RateWithVat { get; set; }
        public int? VatPercent { get; set; }

        // Контакты и площадки
        public int ContactId { get; set; }
        public string BoardId { get; set; } = "";
        public string Note { get; set; } = "";
    }

    public class AtiFieldError
    {
        public string? Property { get; set; }
        public string? Reason { get; set; }
        public string? Error { get; set; }
    }

    public class AtiApiException : Exception
    {
        public HttpStatusCode Status { get; }
        public string Code { get; }
        public IReadOnlyList<AtiFieldError> FieldErrors { get; }

        public bool IsDuplicate => Code is "duplicate_error" or "load_conflict_error";

        public AtiApiException(
            HttpStatusCode status, string code, string? reason,
            IReadOnlyList<AtiFieldError>? fieldErrors = null)
            : base(BuildMessage(code, reason, fieldErrors))
        {
            Status = status;
            Code = code;
            FieldErrors = fieldErrors ?? Array.Empty<AtiFieldError>();
        }

        private static string BuildMessage(
            string code, string? reason, IReadOnlyList<AtiFieldError>? fields)
        {
            var text = code + (string.IsNullOrWhiteSpace(reason) ? "" : " — " + reason);

            if (fields is { Count: > 0 })
            {
                text += "\n• " + string.Join("\n• ", fields.Select(f =>
                    (f.Property ?? "?") + ": " + (f.Reason ?? f.Error ?? "")));
            }

            return text;
        }
    }
}


namespace Logistics.Web.Services
{
    using Logistics.Web.Models;
    using static System.Formats.Asn1.AsnWriter;

    /// Встречное предложение (отзыв) на груз.
    public class AtiOffer
    {
        public string Id { get; set; } = "";
        public string CargoId { get; set; } = "";
        public string CargoNumber { get; set; } = "";
        public string FirmName { get; set; } = "";
        public decimal Score { get; set; }
        public string ContactName { get; set; } = "";
        public string Phone { get; set; } = "";
        public decimal Price { get; set; }
        public decimal NdsPrice { get; set; }
        public decimal NotNdsPrice { get; set; }
        public decimal NdsPercent { get; set; }
        public string Note { get; set; } = "";
        public DateTime? LoadingDate { get; set; }
        public DateTime? AddedAt { get; set; }
        public bool IsOutdated { get; set; }
        public bool IsNoLoad { get; set; }
    }



    /// Груз с закрытой площадки (результат поиска по площадкам).
    public class AtiBoardCargo
    {
        public string Id { get; set; } = "";
        public string Number { get; set; } = "";
        public int AtiId { get; set; }

        public int LoadingCityId { get; set; }
        public string LoadingAddress { get; set; } = "";
        public int UnloadingCityId { get; set; }
        public string UnloadingAddress { get; set; } = "";

        public string CargoName { get; set; } = "";
        public decimal Weight { get; set; }
        public decimal Volume { get; set; }
        public List<int> BodyTypeIds { get; set; } = new();

        public string DateType { get; set; } = "";
        public DateTime? FirstDate { get; set; }
        public DateTime? LastDate { get; set; }

        public string PaymentType { get; set; } = "";
        public decimal RateWithVat { get; set; }
        public decimal RateWithoutVat { get; set; }
        public decimal Cash { get; set; }

        public int Distance { get; set; }
        public int OfferCount { get; set; }
        public DateTime? AddedAt { get; set; }
        public string Note { get; set; } = "";
    }

    /// Своё предложение на чужой груз. Валюта всегда рубли (id = 1).
    public class AtiOfferDraft
    {
        public int ContactId { get; set; }
        public decimal? NdsPrice { get; set; }
        public int NdsPercent { get; set; } = 22;
        public decimal? NotNdsPrice { get; set; }
        public decimal? Price { get; set; }
        public int PrepayPercent { get; set; }
        public int PayAfterBankDays { get; set; }
        public DateTime? LoadingDate { get; set; }
        public string Note { get; set; } = "";
    }

    public class AtiEndpoints
    {
        public string CargoCreate { get; set; } = "";
        public string CargoMine { get; set; } = "";
        public string CargoSearchByBoards { get; set; } = "";
        public string CounterOffersAll { get; set; } = "";
        public string CityAutocomplete { get; set; } = "";
        public string CarTypes { get; set; } = "";
        public string Account { get; set; } = "";
    }

    public class AtiOptions
    {
        public string OrdersSearchBodyJson { get; set; } = "";
        public string BaseUrl { get; set; } = "https://api.ati.su";
        public string Token { get; set; } = "";
        public DateTime ExpiresAt { get; set; } = DateTime.MinValue;
        public int PollingSeconds { get; set; } = 20;
        public string UserAgent { get; set; } = "ati_integrator";

        /// true — в ATI ничего не отправляется (публикация, правка, поднятие).
        public bool DryRun { get; set; }

        public AtiEndpoints Endpoints { get; set; } = new();

        public bool IsExpired =>
            ExpiresAt != DateTime.MinValue && ExpiresAt.Date < DateTime.Today;
    }

    public class AtiService

    {
        // Общая площадка ATI.SU — в форму как «ID площадки» не подставляется.
        private const string CommonBoardId = AtiBoards.CommonId;
        private static List<AtiBoard>? _boardsCache;

        // Встречные предложения (новый API).
        private const string OffersApi = "gw/cargo-counter-offers-api/integrator/v1/cargos/";

        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };


        private static readonly JsonSerializerOptions Pretty = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };


        // Справочники меняются редко — кэшируем на время работы приложения.
        private static List<AtiCarType>? _carTypesCache;
        private static AtiAccount? _accountCache;

        // Названия городов, которые уже встречались в автокомплите.
        private static readonly ConcurrentDictionary<int, string> _cityNames = new()
        {
            [120] = "Краснодар",
            [170] = "Новосибирск"
        };

        private readonly HttpClient _http;
        private readonly AtiOptions _opt;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_opt.BaseUrl) &&
            !string.IsNullOrWhiteSpace(_opt.Token) &&
            !_opt.IsExpired;

        public bool IsExpired => _opt.IsExpired;
        public bool IsDryRun => _opt.DryRun;
        public DateTime ExpiresAt => _opt.ExpiresAt;

        public AtiService(HttpClient http, AtiOptions opt)
        {
            _http = http;
            _opt = opt;

            _http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
            _http.Timeout = TimeSpan.FromSeconds(25);

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", opt.Token);

            if (!string.IsNullOrWhiteSpace(opt.UserAgent))
            {
                _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", opt.UserAgent);
            }

            if (!_http.DefaultRequestHeaders.Contains("Accept"))
            {
                _http.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        // ---------- Справочники ----------

        /// Поиск населённого пункта по началу названия (автокомплит ATI).
        public async Task<List<AtiCitySuggestion>> SearchCitiesAsync(string prefix)
        {
            var raw = await PostAsync<string>(
                Need(_opt.Endpoints.CityAutocomplete, nameof(AtiEndpoints.CityAutocomplete)),
                new { prefix = prefix.Trim(), suggestion_types = 1, limit = 10, country_id = 1 });

            var list = new List<AtiCitySuggestion>();
            if (string.IsNullOrWhiteSpace(raw)) return list;

            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("suggestions", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return list;
            }

            foreach (var s in arr.EnumerateArray())
            {
                if (Int(s, "type") != 1) continue;
                if (!s.TryGetProperty("city", out var city) || city.ValueKind != JsonValueKind.Object)
                    continue;

                var cityId = Int(city, "id");
                if (cityId <= 0) continue;

                var name = Str(city, "name") ?? "";

                list.Add(new AtiCitySuggestion
                {
                    CityId = cityId,
                    RegionId = ChildInt(s, "region", "id"),
                    CountryId = ChildInt(s, "country", "id"),
                    Name = name,
                    Address = Str(s, "address") ?? name
                });

                if (name.Length > 0) _cityNames[cityId] = name;
            }

            return list;
        }

        /// Название города по ID, если оно уже встречалось в автокомплите.
        public string CityName(int cityId)
            => cityId <= 0
                ? "—"
                : (_cityNames.TryGetValue(cityId, out var n) ? n : "Город #" + cityId);

        private async Task LoadCityNamesAsync(IEnumerable<int> cityIds)
        {
            var missingIds = cityIds
                .Where(id => id > 0 && !_cityNames.ContainsKey(id))
                .Distinct()
                .ToArray();

            foreach (var ids in missingIds.Chunk(1000))
            {
                try
                {
                    var raw = await PostAsync<string>(
                        "gw/gis-dict/v1/cities/by-ids",
                        new { ids });

                    using var doc = JsonDocument.Parse(raw);
                    var cities = Child(doc.RootElement, "cities");

                    if (cities.ValueKind != JsonValueKind.Array)
                    {
                        Console.Error.WriteLine("[ATI] Справочник не вернул массив cities.");
                        continue;
                    }

                    var requestedIds = ids.ToHashSet();

                    foreach (var city in cities.EnumerateArray())
                    {
                        var id = Int(city, "city_id");
                        var name = StrAny(city, "name", "clarified_name");

                        if (requestedIds.Contains(id) && !string.IsNullOrWhiteSpace(name))
                        {
                            _cityNames[id] = name.Trim();
                        }
                    }
                }
                catch (Exception ex) when (ex is AtiApiException or HttpRequestException
                                           or TaskCanceledException or JsonException)
                {
                    // Сбой справочника не должен мешать показу грузов.
                    Console.Error.WriteLine(
                        $"[ATI] Не удалось получить названия городов ({ex.GetType().Name}).");
                    break;
                }
            }
        }

        /// Справочник типов кузовов (с кэшем).
        public async Task<List<AtiCarType>> GetCarTypesAsync()
        {
            if (_carTypesCache != null) return _carTypesCache;

            var raw = await GetAsync<string>(
                Need(_opt.Endpoints.CarTypes, nameof(AtiEndpoints.CarTypes)));

            var list = new List<AtiCarType>();
            using var doc = JsonDocument.Parse(raw);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in doc.RootElement.EnumerateArray())
                {
                    list.Add(new AtiCarType
                    {
                        Id = ULong(t, "Id"),
                        TypeId = Int(t, "TypeId"),
                        Name = Str(t, "Name") ?? ""
                    });
                }
            }

            _carTypesCache = list.Where(t => !string.IsNullOrWhiteSpace(t.Name))
                                 .OrderBy(t => t.Name)
                                 .ToList();
            return _carTypesCache;
        }

        /// Значение кузова для body_types: по документации v2 это TypeId.
        public int BodyValue(AtiCarType t) => t.TypeId;

        /// Названия кузовов по битовой маске Transport.CarType (v1.0).
        public string BodyNames(ulong mask)
        {
            if (mask == 0 || _carTypesCache == null) return "";

            return string.Join(", ", _carTypesCache
                .Where(t => t.Id != 0 && (mask & t.Id) == t.Id)
                .Select(t => t.Name));
        }

        /// Названия кузовов по списку TypeId (поле body_types).
        public string BodyNamesByTypeIds(IEnumerable<int> typeIds)
        {
            if (_carTypesCache == null) return "";

            var set = typeIds.ToHashSet();
            return string.Join(", ", _carTypesCache
                .Where(t => set.Contains(t.TypeId))
                .Select(t => t.Name));
        }

        /// Фирма и контакты аккаунта, которому принадлежит токен (с кэшем).
        public async Task<AtiAccount> GetAccountAsync()
        {
            if (_accountCache != null) return _accountCache;

            var raw = await GetAsync<string>(
                Need(_opt.Endpoints.Account, nameof(AtiEndpoints.Account)));

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var acc = new AtiAccount
            {
                AtiId = Str(root, "account_id") ?? "",
                FirmName = Str(root, "full_firm_name") ?? Str(root, "firm_name") ?? "",
                CurrentContactId = Int(root, "contact_id")
            };

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("contacts", out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in arr.EnumerateArray())
                {
                    acc.Contacts.Add(new AtiContact
                    {
                        Id = Int(c, "id"),
                        Name = Str(c, "name") ?? "",
                        Position = Str(c, "position") ?? "",
                        IsVisible = !c.TryGetProperty("is_visibled", out var v) ||
                                    v.ValueKind != JsonValueKind.False
                    });
                }
            }

            _accountCache = acc;
            return acc;
        }

        private static object? BuildBodyHandling(
    HashSet<int> ids, bool isAllRequired)
        {
            if (ids.Count == 0)
                return null;

            return new
            {
                types = ids.OrderBy(id => id).ToArray(),
                is_all_required = isAllRequired
            };
        }

        // ---------- Площадки ----------

        /// Все площадки, где контакт может добавлять или смотреть грузы (с кэшем).
        public async Task<List<AtiBoard>> GetBoardsAsync(bool force = false)
        {
            if (_boardsCache != null && !force) return _boardsCache;

            var raw = await GetAsync<string>("v2/boards/public/boards/list");
            var list = new List<AtiBoard>();

            if (!string.IsNullOrWhiteSpace(raw))
            {
                var myAtiId = _accountCache?.AtiId ?? "";

                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var b in doc.RootElement.EnumerateArray())
                    {
                        var id = Str(b, "id");
                        if (string.IsNullOrEmpty(id) || id == CommonBoardId) continue;

                        var owner = Str(b, "owner_ati_id") ?? "";
                        list.Add(new AtiBoard
                        {
                            Id = id,
                            Name = Str(b, "name") ?? id,
                            Color = Str(b, "color") ?? "",
                            OwnerAtiId = owner,
                            Direction = Str(b, "board_exchange_direction") ?? "",
                            CanAdd = Bool(b, "can_add"),
                            CanView = Bool(b, "can_view"),
                            IsOwner = owner.Length > 0 && owner == myAtiId,
                            ContentCount = Int(b, "content_count"),
                            ParticipantsCount = Int(b, "participants_count")
                        });
                    }
                }
            }

            _boardsCache = list.OrderByDescending(b => b.IsOwner).ThenBy(b => b.Name).ToList();
            return _boardsCache;
        }

        /// Название площадки по ID; общая площадка распознаётся всегда.
        public string BoardName(string boardId)
        {
            if (boardId == CommonBoardId) return AtiBoards.CommonName;
            var b = _boardsCache?.FirstOrDefault(x => x.Id == boardId);
            return b?.Name ?? ("Площадка " + boardId);
        }

        /// Названия площадок через запятую.
        public string BoardNames(IEnumerable<string> boardIds)
            => string.Join(", ", boardIds.Select(BoardName));


        // ---------- Публикация и редактирование ----------

        public Task<string> CreateCargoAsync(AtiCargoDraft d)
        {
            EnsureValid(d);

            if (_opt.DryRun)
            {
                return Task.FromResult("DRY-RUN: запрос не отправлен.\n" + PreviewJson(d));
            }

            return PostAsync<string>(CargoRoute(),
                new { cargo_application = BuildCargoApplication(d) });
        }

        /// Редактирование опубликованного груза. Тело передаётся целиком,
        /// как при добавлении: груз сохранит только то, что есть в форме.
        public Task<string> UpdateCargoAsync(string cargoId, AtiCargoDraft d)
        {
            EnsureValid(d);

            if (_opt.DryRun)
            {
                return Task.FromResult("DRY-RUN: запрос не отправлен.\n" + PreviewJson(d));
            }

            return PutAsync<string>(CargoRoute() + "/" + Uri.EscapeDataString(cargoId),
                new { cargo_application = BuildCargoApplication(d) });

        }

        /// Тело запроса без отправки — для сверки перед публикацией.
        public string PreviewJson(AtiCargoDraft d)
            => JsonSerializer.Serialize(
                new { cargo_application = BuildCargoApplication(d) }, Pretty);

        private string CargoRoute()
            => Need(_opt.Endpoints.CargoCreate, nameof(AtiEndpoints.CargoCreate));

        private static void EnsureValid(AtiCargoDraft d)
        {
            var problems = Validate(d);
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "Форма заполнена с ошибками:\n• " + string.Join("\n• ", problems));
            }
        }

        /// Проверки, которые дешевле сделать до запроса, чем ловить 400 от ATI.
        public static List<string> Validate(AtiCargoDraft d)
        {
            var p = new List<string>();

            if (d.LoadingCityId <= 0) p.Add("не выбран город загрузки");
            if (d.UnloadingCityId <= 0) p.Add("не выбран город разгрузки");
            if (d.LoadingCityId > 0 && d.LoadingCityId == d.UnloadingCityId)
                p.Add("город загрузки и разгрузки совпадают");
            if (string.IsNullOrWhiteSpace(d.CargoName)) p.Add("не указано название груза");
            if (d.Weight <= 0) p.Add("вес должен быть больше 0");
            if (d.LoadingDate.Date < DateTime.Today) p.Add("дата загрузки в прошлом");
            if (d.BodyTypeIds.Count == 0)
                p.Add("выберите хотя бы один кузов");
            if (d.BoardIds.Count == 0) p.Add("выберите хотя бы одну площадку");
            if (d.PublicationMode is not ("now" or "15m" or "30m" or "1h" or "3h" or "6h"))
                p.Add("неверный режим публикации");

            if (IsShortAddress(d.LoadingAddress)) p.Add("адрес загрузки короче 5 символов");
            if (IsShortAddress(d.UnloadingAddress)) p.Add("адрес разгрузки короче 5 символов");

            if (d.PaymentType != "rate-request" &&
                d.RateWithoutVat is null && d.Cash is null && d.RateWithVat is null)
                p.Add("укажите хотя бы одну ставку (без НДС, с НДС или наличными)");

            if (d.RateWithVat is not null && d.RateWithoutVat is not null &&
                d.RateWithVat < d.RateWithoutVat)
                p.Add("ставка с НДС не может быть меньше ставки без НДС");

            if (!string.IsNullOrWhiteSpace(d.Note) &&
                Regex.IsMatch(d.Note, @"(\+?\d[\d\-\s\(\)]{8,})|(https?://)|(@\w)"))
                p.Add("в примечании телефон/ссылка/ник — модератор ATI удалит груз");

            return p;
        }

        private static bool IsShortAddress(string? a)
            => !string.IsNullOrWhiteSpace(a) && a.Trim().Length < 5;

        // ---------- Мои грузы ----------

        public Task<string> GetMyCargoRawAsync()
            => GetAsync<string>(Need(_opt.Endpoints.CargoMine, nameof(AtiEndpoints.CargoMine)));

        /// Опубликованные грузы фирмы — разобранные для карточек.
        public async Task<List<AtiLoad>> GetMyCargosAsync()
        {
            var list = ParseLoads(await GetMyCargoRawAsync());

            await LoadCityNamesAsync(
                list.SelectMany(l => new[] { l.LoadingCityId, l.UnloadingCityId }));

            return list;
        }

        /// Разбор массива грузов формата v1.0 (опубликованные и архивные).
        private static List<AtiLoad> ParseLoads(string raw)
        {
            var list = new List<AtiLoad>();
            if (string.IsNullOrWhiteSpace(raw)) return list;

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;

            foreach (var x in doc.RootElement.EnumerateArray())
            {
                var loading = Child(x, "Loading");
                var unloading = Child(x, "Unloading");
                foreach (var point in new[] { loading, unloading })
                {
                    var cityId = Int(point, "CityId");
                    var cityName = StrAny(point, "CityName", "city_name");

                    if (cityId > 0 && !string.IsNullOrWhiteSpace(cityName))
                    {
                        _cityNames[cityId] = cityName.Trim();
                    }
                }
                var cargo = Child(x, "Cargo");
                var transport = Child(x, "Transport");
                var payment = Child(x, "Payment");

                ulong mask = 0;
                var maskText = Str(transport, "CarTypeString");
                if (maskText == null || !ulong.TryParse(maskText, out mask))
                {
                    mask = ULong(transport, "CarType");
                }

                var boardIds = new List<string>();
                var boardsEl = Child(x, "Boards");
                if (boardsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var b in boardsEl.EnumerateArray())
                    {
                        var bid = Str(b, "BoardId");
                        if (!string.IsNullOrEmpty(bid)) boardIds.Add(bid);
                    }
                }


                list.Add(new AtiLoad
                {
                    Id = Str(x, "Id") ?? "",
                    Number = Str(x, "LoadNumber") ?? "",
                    LoadingCityId = Int(loading, "CityId"),
                    LoadingStreet = Str(loading, "Street") ?? "",
                    UnloadingCityId = Int(unloading, "CityId"),
                    UnloadingStreet = Str(unloading, "Street") ?? "",
                    CargoName = Str(cargo, "CargoType") ?? "",
                    Weight = Dec(cargo, "Weight") ?? 0,
                    Volume = Dec(cargo, "Volume") ?? 0,
                    CarTypeMask = mask,
                    DateType = Int(x, "DateType"),
                    FirstDate = Date(x, "FirstDate")?.UtcDateTime.Date,
                    LastDate = Date(x, "LastDate")?.UtcDateTime.Date,
                    FixedRate = Bool(payment, "FixedRate"),
                    Torg = Bool(payment, "Torg"),
                    RateSum = Dec(payment, "RateSum") ?? 0,
                    SumWithNds = Dec(payment, "SumWithNDS") ?? 0,
                    SumWithoutNds = Dec(payment, "SumWithoutNDS") ?? 0,
                    Distance = (int)(Dec(x, "Distance") ?? 0),
                    OfferCount = Int(x, "OfferCount"),
                    AddedAt = Date(x, "AddedAt")?.LocalDateTime,
                    Note = Str(x, "Note") ?? "",
                    CanBeEdited = Bool(x, "CanBeEdited"),
                    CanBeRenewed = Bool(x, "CanBeRenewed"),
                    RenewRestriction = Str(x, "RenewRestriction") ?? "",
                    BoardIds = boardIds,
                                        ContactId = Int(x, "ContactId1"),
                    ArchiveDate = Date(x, "ArchiveDate")?.LocalDateTime,
                    ArchiveReason = Str(x, "ArchiveReason") ?? "",
                    CanBeRestored = Bool(x, "CanBeRestored")
                });
            }

            return list;
        }

        /// Груз в виде черновика формы — для «Редактировать» и «Копировать».
        /// Груз в виде черновика формы — для «Редактировать» и «Копировать».

        private static (HashSet<int> Ids, bool AllRequired) ReadBodyHandling(
            JsonElement truck, string propertyName)
        {
            var ids = new HashSet<int>();
            var handling = Child(truck, propertyName);

            if (handling.ValueKind != JsonValueKind.Object)
                return (ids, false);

            var types = Child(handling, "types");
            if (types.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in types.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number &&
                        item.TryGetInt32(out var id))
                    {
                        ids.Add(id);
                    }
                }
            }

            var allRequired = Child(handling, "is_all_required");
            return (ids, allRequired.ValueKind == JsonValueKind.True);
        }
        public async Task<AtiCargoDraft> GetCargoDraftAsync(string cargoId)
        {
            var raw = await GetAsync<string>(CargoRoute() + "/" + Uri.EscapeDataString(cargoId));

            using var doc = JsonDocument.Parse(raw);
            var app = Child(doc.RootElement, "cargo_application");
            if (app.ValueKind != JsonValueKind.Object) app = doc.RootElement;

            var route = Child(app, "route");
            var loading = Child(route, "loading");
            var unloading = Child(route, "unloading");
            var lCity = Child(loading, "city");
            var uCity = Child(unloading, "city");
            var cargo = First(Child(loading, "cargos"));
            var truck = Child(app, "truck");

            var loadingHandling = ReadBodyHandling(truck, "body_loading");
            var unloadingHandling = ReadBodyHandling(truck, "body_unloading");
            var payment = Child(app, "payment");
            var rate = First(Child(payment, "rates_with_vat"));

            var weightEl = Child(cargo, "weight");
            var weight = Dec(weightEl, "quantity") ?? 0;
            if (Str(weightEl, "type") == "kilos") weight /= 1000;

            var date = Date(Child(loading, "dates"), "first_date")?.Date ?? DateTime.Today;
            if (date < DateTime.Today) date = DateTime.Today;

            var lCityId = Int(lCity, "city_id");
            if (lCityId == 0) lCityId = Int(loading, "city_id");
            var uCityId = Int(uCity, "city_id");
            var bodyTypeIds = new List<int>();
            var bodyTypes = Child(truck, "body_types");

            if (bodyTypes.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in bodyTypes.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number &&
                        item.TryGetInt32(out var id) &&
                        id > 0 &&
                        !bodyTypeIds.Contains(id))
                    {
                        bodyTypeIds.Add(id);
                    }
                }
            }

            if (bodyTypes.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in bodyTypes.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number &&
                        item.TryGetInt32(out var bodyTypeId) &&
                        bodyTypeId > 0 &&
                        !bodyTypeIds.Contains(bodyTypeId))
                    {
                        bodyTypeIds.Add(bodyTypeId);
                    }
                }
            }

            var boardIds = new List<string>();
            var boards = Child(app, "boards");
            if (boards.ValueKind == JsonValueKind.Array)
            {
                foreach (var b in boards.EnumerateArray())
                {
                    var id = Str(b, "id");
                    if (!string.IsNullOrEmpty(id) && !boardIds.Contains(id)) boardIds.Add(id);
                }
            }

            var currency = Int(payment, "currency_type");

            var d = new AtiCargoDraft
            {
                BodyLoadingTypeIds = loadingHandling.Ids,
                BodyUnloadingTypeIds = unloadingHandling.Ids,
                IsAllBodyLoadingRequired = loadingHandling.AllRequired,
                IsAllBodyUnloadingRequired = unloadingHandling.AllRequired,

                LoadingCityId = lCityId,
                LoadingRegionId = Int(lCity, "region_id"),
                LoadingCityName = CityName(lCityId),
                LoadingAddress = Str(loading, "address") ?? "",
                LoadingDate = date,

                UnloadingCityId = uCityId,
                UnloadingRegionId = Int(uCity, "region_id"),
                UnloadingCityName = CityName(uCityId),
                UnloadingAddress = Str(unloading, "address") ?? "",

                CargoName = Str(cargo, "name") ?? "",
                CargoId = Int(cargo, "cargo_id"),
                Weight = weight,
                Volume = Dec(Child(cargo, "volume"), "quantity") ?? 0,

                BodyTypeIds = bodyTypeIds,
                TrucksCount = Math.Max(1, Int(truck, "trucks_count")),
                LoadType = Str(truck, "load_type") ?? "ftl",
                RequiredCapacity = Dec(truck, "required_capacity") ?? 20,

                PaymentType = Str(payment, "type") ?? "without-bargaining",
                CurrencyType = currency > 0 ? currency : 1,
                RateWithoutVat = Dec(payment, "rate_without_vat"),
                Cash = Dec(payment, "cash"),
                RateWithVat = Dec(rate, "rate_with_vat"),
                VatPercent = rate.ValueKind == JsonValueKind.Object &&
                             rate.TryGetProperty("vat_percent", out var vp) &&
                             vp.ValueKind == JsonValueKind.Number && vp.TryGetInt32(out var vpi)
                    ? vpi
                    : null,

                ContactId = Int(First(Child(app, "application_contacts")), "id"),
                BoardIds = boardIds.Count > 0 ? boardIds : new List<string> { CommonBoardId },
                Note = Str(app, "note") ?? ""
            };

            if (Int(lCity, "country_id") > 0) d.LoadingCountryId = Int(lCity, "country_id");
            if (Int(uCity, "country_id") > 0) d.UnloadingCountryId = Int(uCity, "country_id");

            return d;
        }

        public async Task<List<string>> GetCargoNamesAsync(CancellationToken ct = default)
        {
            const string url = "https://api.ati.su/gw/dictionaries/v1/cargo_typ?locale=ru";

            var items = await _http.GetFromJsonAsync<List<AtiLoadType>>(url, ct)
                        ?? new List<AtiLoadType>();

            return items
                .Select(i => i.Attributes.TryGetValue("cargo_name", out var n) ? n.Trim() : "")
                .Where(n => n.Length > 0)
                .Distinct()
                .ToList();
        }

        public async Task<List<(int Id, string Name)>> GetHandlingTypesAsync(
    CancellationToken ct = default)
        {
            const string url = "https://api.ati.su/gw/dictionaries/v1/load_type?locale=ru";

            var items = await _http.GetFromJsonAsync<List<AtiLoadType>>(url, ct)
                        ?? new List<AtiLoadType>();

            return items
                .Where(i => i.Id > 0 && !string.IsNullOrWhiteSpace(i.Name))
                .Select(i => (i.Id, i.Name))
                .ToList();
        }

        /// Поднять грузы в выдаче (до 100 за запрос, не чаще раза в 60 минут на груз).
        /// Результат: id груза → "" при успехе или текст ошибки.
        public async Task<Dictionary<string, string>> RefreshCargosAsync(IEnumerable<string> cargoIds)
        {
            var ids = cargoIds.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().Take(100).ToList();
            var result = new Dictionary<string, string>();
            if (ids.Count == 0) return result;

            if (_opt.DryRun)
            {
                foreach (var id in ids) result[id] = "DRY-RUN: запрос не отправлен";
                return result;
            }

            var raw = await PutAsync<string>(CargoRoute() + "/refresh", ids);

            if (!string.IsNullOrWhiteSpace(raw))
            {
                using var doc = JsonDocument.Parse(raw);
                var map = Child(doc.RootElement, "refresh_operation_responses");

                if (map.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in map.EnumerateObject())
                    {
                        result[p.Name] = Str(p.Value, "type") == "success"
                            ? ""
                            : (Str(p.Value, "reason") ?? Str(p.Value, "error_code") ?? "ошибка");
                    }
                }
            }

            foreach (var id in ids)
            {
                if (!result.ContainsKey(id)) result[id] = "нет ответа по грузу";
            }

            return result;
        }

        /// Снять грузы с публикации и отправить в архив (до 100 за запрос).
        /// Результат: id груза → "" при успехе или текст ошибки.
        public async Task<Dictionary<string, string>> ArchiveCargosAsync(IEnumerable<string> cargoIds)
        {
            var ids = cargoIds.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().Take(100).ToList();
            var result = new Dictionary<string, string>();
            if (ids.Count == 0) return result;

            if (_opt.DryRun)
            {
                foreach (var id in ids) result[id] = "DRY-RUN: запрос не отправлен";
                return result;
            }

            var raw = await PutAsync<string>(CargoRoute() + "/archive",
                new { cargo_application_ids = ids });

            if (!string.IsNullOrWhiteSpace(raw))
            {
                using var doc = JsonDocument.Parse(raw);
                var map = Child(doc.RootElement, "cargo_application_ids");

                if (map.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in map.EnumerateObject())
                    {
                        result[p.Name] = Str(p.Value, "type") == "success"
                            ? ""
                            : (Str(p.Value, "reason") ?? Str(p.Value, "error_code") ?? "ошибка");
                    }
                }
            }

            foreach (var id in ids)
            {
                if (!result.ContainsKey(id)) result[id] = "нет ответа по грузу";
            }

            return result;
        }

        // ---------- Архив ----------

        private const string ArchiveApi = "v1.0/unpublished-loads";

        /// Архивные грузы фирмы, свежие сверху.
        public async Task<List<AtiLoad>> GetArchivedCargosAsync()
        {
            var list = ParseLoads(await GetAsync<string>(ArchiveApi));

            await LoadCityNamesAsync(
                list.SelectMany(l => new[] { l.LoadingCityId, l.UnloadingCityId }));

            return list.OrderByDescending(l => l.ArchiveDate).ToList();
        }
        /// Восстановить грузы из архива (до 100). Даты ATI сдвигает на сегодня сам.
        /// Результат: id груза → "" при успехе или текст ошибки.
        public async Task<Dictionary<string, string>> RestoreCargosAsync(IEnumerable<string> cargoIds)
        {
            var ids = cargoIds.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().Take(100).ToList();
            var result = new Dictionary<string, string>();
            if (ids.Count == 0) return result;

            if (_opt.DryRun)
            {
                foreach (var id in ids) result[id] = "DRY-RUN: запрос не отправлен";
                return result;
            }

            var url = ArchiveApi + "/restore?ids=" + Uri.EscapeDataString(JsonSerializer.Serialize(ids));
            using var req = new HttpRequestMessage(HttpMethod.Put, url);
            var raw = await ReadAsync<string>(await _http.SendAsync(req));

            if (!string.IsNullOrWhiteSpace(raw))
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in doc.RootElement.EnumerateObject())
                    {
                        var status = DecAny(p.Value, "Status") ?? -1;
                        result[p.Name] = status == 0
                            ? ""
                            : (StrAny(p.Value, "Message") ?? "ошибка " + status);
                    }
                }
            }

            foreach (var id in ids)
            {
                if (!result.ContainsKey(id)) result[id] = "нет ответа по грузу";
            }

            return result;
        }

        /// Удалить архивные грузы безвозвратно (до 100).
        /// Результат: id груза → "" при успехе или текст ошибки.
        public async Task<Dictionary<string, string>> DeleteArchivedCargosAsync(IEnumerable<string> cargoIds)
        {
            var ids = cargoIds.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().Take(100).ToList();
            var result = new Dictionary<string, string>();
            if (ids.Count == 0) return result;

            if (_opt.DryRun)
            {
                foreach (var id in ids) result[id] = "DRY-RUN: запрос не отправлен";
                return result;
            }

            using var req = new HttpRequestMessage(HttpMethod.Delete, CargoRoute())
            {
                Content = JsonContent.Create(new { cargo_application_ids = ids })
            };
            var raw = await ReadAsync<string>(await _http.SendAsync(req));

            if (!string.IsNullOrWhiteSpace(raw))
            {
                using var doc = JsonDocument.Parse(raw);
                var map = Child(doc.RootElement, "cargo_application_ids");
                if (map.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in map.EnumerateObject())
                    {
                        result[p.Name] = Str(p.Value, "type") == "success"
                            ? ""
                            : (Str(p.Value, "reason") ?? Str(p.Value, "error_code") ?? "ошибка");
                    }
                }
            }

            foreach (var id in ids)
            {
                if (!result.ContainsKey(id)) result[id] = "нет ответа по грузу";
            }

            return result;
        }

        /// Комментарии своей фирмы ко всем архивным грузам: id груза → список.
        public async Task<Dictionary<string, List<AtiLoadComment>>> GetArchiveCommentsAsync()
        {
            var raw = await GetAsync<string>(ArchiveApi + "/comments");
            var result = new Dictionary<string, List<AtiLoadComment>>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;

            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.Array) continue;

                var list = new List<AtiLoadComment>();
                foreach (var c in p.Value.EnumerateArray())
                {
                    list.Add(new AtiLoadComment
                    {
                        Id = Str(c, "CommentId") ?? "",
                        LoadId = Str(c, "LoadId") ?? p.Name,
                        Message = Str(c, "Message") ?? "",
                        ContactId = Int(c, "ContactId"),
                        Date = Date(c, "CommentDateTime")?.LocalDateTime
                    });
                }

                if (list.Count > 0) result[p.Name] = list.OrderByDescending(c => c.Date).ToList();
            }

            return result;
        }


        // ---------- Поиск по площадкам ----------

        /// Грузы с закрытых площадок контакта (сырой JSON). Ответ: { cargos: [...], total }.
        public Task<string> SearchCargoRawAsync(List<string>? boardIds = null)
        {
            var route = Need(_opt.Endpoints.CargoSearchByBoards,
                nameof(AtiEndpoints.CargoSearchByBoards));

            var parts = (boardIds ?? new List<string>())
                .Select(id => "board_ids=" + Uri.EscapeDataString(id))
                .ToList();
            parts.Add("skip=0");
            parts.Add("take=100");

            return GetAsync<string>(route + "?" + string.Join("&", parts));
        }

        /// Страница грузов с закрытых площадок, разобранная для карточек.
        /// skip = 0 начинает новый поиск, дальше листается та же выдача (15 минут).
        public async Task<(List<AtiBoardCargo> Items, int Total)> SearchCargosAsync(
            List<string>? boardIds, int skip, int take = 100)
        {
            var route = Need(_opt.Endpoints.CargoSearchByBoards,
                nameof(AtiEndpoints.CargoSearchByBoards));

            var parts = (boardIds ?? new List<string>())
                .Select(id => "board_ids=" + Uri.EscapeDataString(id))
                .ToList();
            parts.Add("skip=" + skip);
            parts.Add("take=" + take);

            var raw = await GetAsync<string>(route + "?" + string.Join("&", parts));
            var list = new List<AtiBoardCargo>();
            if (string.IsNullOrWhiteSpace(raw)) return (list, 0);

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var arr = Child(root, "cargos");

            if (arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in arr.EnumerateArray())
                {
                    var route2 = Child(x, "route");
                    var loading = Child(route2, "loading");
                    var unloading = Child(route2, "unloading");
                    var dates = Child(loading, "dates");
                    var truck = Child(x, "truck");
                    var payment = Child(x, "payment");

                    var names = new List<string>();
                    decimal weight = 0, volume = 0;
                    var cargos = Child(loading, "cargos");
                    if (cargos.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var c in cargos.EnumerateArray())
                        {
                            var name = Str(c, "name");
                            if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name)) names.Add(name);

                            var w = Child(c, "weight");
                            var q = Dec(w, "quantity") ?? 0;
                            weight += Str(w, "type") == "kilos" ? q / 1000 : q;
                            volume += Dec(Child(c, "volume"), "quantity") ?? 0;
                        }
                    }

                    var bodies = new List<int>();
                    var bodyArr = Child(truck, "body_types");
                    if (bodyArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var b in bodyArr.EnumerateArray())
                        {
                            if (b.ValueKind == JsonValueKind.Number && b.TryGetInt32(out var bi)) bodies.Add(bi);
                        }
                    }

                    list.Add(new AtiBoardCargo
                    {
                        Id = Str(x, "cargo_id") ?? "",
                        Number = Str(x, "cargo_number") ?? "",
                        AtiId = Int(x, "ati_id"),
                        LoadingCityId = Int(loading, "city_id"),
                        LoadingAddress = Str(loading, "address") ?? "",
                        UnloadingCityId = Int(unloading, "city_id"),
                        UnloadingAddress = Str(unloading, "address") ?? "",
                        CargoName = string.Join(", ", names),
                        Weight = weight,
                        Volume = volume,
                        BodyTypeIds = bodies,
                        DateType = Str(dates, "type") ?? "",
                        FirstDate = Date(dates, "first_date")?.UtcDateTime.Date,
                        LastDate = Date(dates, "last_date")?.UtcDateTime.Date,
                        PaymentType = Str(payment, "type") ?? "",
                        RateWithVat = Dec(First(Child(payment, "rates_with_vat")), "rate_with_vat") ?? 0,
                        RateWithoutVat = Dec(payment, "rate_without_vat") ?? 0,
                        Cash = Dec(payment, "cash") ?? 0,
                        Distance = Int(x, "distance"),
                        OfferCount = Int(x, "offer_count"),
                        AddedAt = Date(x, "added_at")?.LocalDateTime,
                        Note = Str(x, "note") ?? ""
                    });
                }
            }

            return (list, Int(root, "total"));
        }

        // ---------- Встречные предложения на свои грузы ----------

        public async Task<string> SearchOrdersRawAsync()
        {
            if (string.IsNullOrWhiteSpace(_opt.OrdersSearchBodyJson))
            {
                throw new InvalidOperationException(
                    "Укажите тело запроса из документации ATI в Ati:OrdersSearchBodyJson.");
            }

            using var body = JsonDocument.Parse(_opt.OrdersSearchBodyJson);
            return await PostAsync<string>("v1.2/orders/search", body.RootElement);
        }

        public Task<string> GetIncomingInvitesRawAsync()
    => GetAsync<string>("v1.2/orders/invites/my");

        public Task<string> GetCounterOffersRawAsync()
            => GetAsync<string>(
                Need(_opt.Endpoints.CounterOffersAll, nameof(AtiEndpoints.CounterOffersAll)));

        /// Встречные предложения на свои опубликованные грузы, разобранные для карточек.
        /// Понимает оба формата ATI: v1.0 (ResponseId, NdsPrice) и gw (id, nds_price).
        public async Task<List<AtiOffer>> GetCounterOffersAsync()
        {
            var raw = await GetCounterOffersRawAsync();
            var list = new List<AtiOffer>();
            if (string.IsNullOrWhiteSpace(raw)) return list;

            using var doc = JsonDocument.Parse(raw);
            CollectOffers(doc.RootElement, list, 0);

            return list.OrderByDescending(o => o.AddedAt).ToList();
        }

        /// Встречные предложения на свои архивные (неопубликованные) грузы.
        public async Task<List<AtiOffer>> GetUnpublishedCounterOffersAsync(int days = 30)
        {
            var raw = await GetAsync<string>(OffersApi + "unpublished/counter-offers?fromDate=" +
                                             AtiDate(DateTime.UtcNow.AddDays(-days)));
            var list = new List<AtiOffer>();
            if (string.IsNullOrWhiteSpace(raw)) return list;

            using var doc = JsonDocument.Parse(raw);
            CollectOffers(doc.RootElement, list, 0);
            return list.OrderByDescending(o => o.AddedAt).ToList();
        }

        private static void CollectOffers(JsonElement el, List<AtiOffer> list, int depth)
        {
            if (depth > 4) return;

            if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in el.EnumerateArray()) CollectOffers(x, list, depth + 1);
                return;
            }

            if (el.ValueKind != JsonValueKind.Object) return;

            var id = StrAny(el, "id", "ResponseId");
            var cargoId = StrAny(el, "cargo_id", "LoadId");

            if (id == null || cargoId == null)
            {
                // словарь «id груза → { Result: [...] }»
                foreach (var p in el.EnumerateObject()) CollectOffers(p.Value, list, depth + 1);
                return;
            }

            var firm = ChildAny(el, "firm_info");
            if (firm.ValueKind != JsonValueKind.Object) firm = ChildAny(el, "FirmInfo");
            var contact = ChildAny(firm, "contact");

            list.Add(new AtiOffer
            {
                Id = id,
                CargoId = cargoId,
                CargoNumber = StrAny(el, "cargo_number", "LoadNumber") ?? "",
                FirmName = StrAny(el, "FirmName") ?? StrAny(firm, "full_firm_name", "FullFirmName") ?? "",
                Score = DecAny(firm, "total_score", "TotalScore") ?? 0,
                ContactName = StrAny(contact, "name") ?? "",
                Phone = StrAny(contact, "mobile", "telephone") ?? "",
                Price = DecAny(el, "price") ?? 0,
                NdsPrice = DecAny(el, "nds_price", "NdsPrice") ?? 0,
                NotNdsPrice = DecAny(el, "not_nds_price", "NotNdsPrice") ?? 0,
                NdsPercent = DecAny(el, "nds_percent", "NdsPercent") ?? 0,
                Note = StrAny(el, "note") ?? "",
                LoadingDate = DateAny(el, "loading_date", "LoadingDate")?.Date,
                AddedAt = DateAny(el, "added_at", "AddedAt")?.LocalDateTime,
                IsOutdated = ChildAny(el, "is_outdated").ValueKind == JsonValueKind.True ||
                             ChildAny(el, "IsOutdated").ValueKind == JsonValueKind.True,
                IsNoLoad = ChildAny(el, "IsNoLoad").ValueKind == JsonValueKind.True
            });
        }

        /// Удалить чужое встречное предложение на своём грузе.
        public async Task DeleteCounterOfferAsync(string cargoId, string offerId)
        {
            HttpResponseMessage resp;
            var cargo = Uri.EscapeDataString(cargoId);

            if ((_opt.Endpoints.CounterOffersAll ?? "")
                .Contains("cargo-counter-offers-api", StringComparison.OrdinalIgnoreCase))
            {
                var req = new HttpRequestMessage(HttpMethod.Delete, OfferRoute(cargoId))
                {
                    Content = JsonContent.Create(new[] { offerId })
                };
                resp = await _http.SendAsync(req);
            }
            else
            {
                var ids = Uri.EscapeDataString(JsonSerializer.Serialize(new[] { offerId }));
                resp = await _http.DeleteAsync("v1.0/loads/new/" + cargo + "/responses?ids=" + ids);
            }

            var raw = await ReadAsync<string>(resp);
            if (string.IsNullOrWhiteSpace(raw)) return;

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;

            foreach (var p in doc.RootElement.EnumerateObject())
            {
                var status = DecAny(p.Value, "Status");
                var error = StrAny(p.Value, "error");

                if ((status != null && status != 0) || error != null)
                {
                    throw new InvalidOperationException(
                        "ATI не удалил предложение: " + (error ?? StrAny(p.Value, "message") ?? "ошибка"));
                }
            }
        }

        private static decimal? DecAny(JsonElement el, params string[] names)
        {
            foreach (var n in names)
            {
                var s = ToText(ChildAny(el, n));
                if (s != null &&
                    decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                {
                    return d;
                }
            }

            return null;
        }

        private static DateTimeOffset? DateAny(JsonElement el, params string[] names)
        {
            foreach (var n in names)
            {
                var v = ChildAny(el, n);
                if (v.ValueKind == JsonValueKind.String &&
                    DateTimeOffset.TryParse(v.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var d))
                {
                    return d;
                }
            }

            return null;
        }

        // ---------- Свои встречные предложения на чужие грузы ----------

        private static string OfferRoute(string cargoId)
            => OffersApi + Uri.EscapeDataString(cargoId) + "/counter-offers";

        private static string AtiDate(DateTime utc)
            => Uri.EscapeDataString(new DateTimeOffset(utc, TimeSpan.Zero).ToString("yyyy-MM-dd'T'HH:mm:sszzz"));

        private static string ErrorText(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var reason = StrAny(doc.RootElement, "reason", "Reason", "error", "Error");
                if (reason != null) return reason;
            }
            catch (JsonException)
            {
                // тело не JSON
            }

            return body;
        }

        private static Dictionary<string, object?> BuildOfferBody(AtiOfferDraft d)
        {
            if (!(d.NdsPrice > 0) && !(d.NotNdsPrice > 0) && !(d.Price > 0))
                throw new InvalidOperationException("Укажите хотя бы одну ставку.");
            if (d.NdsPrice > 0 && (d.NdsPercent < 1 || d.NdsPercent > 99))
                throw new InvalidOperationException("Укажите процент НДС от 1 до 99.");
            if (d.PrepayPercent < 0 || d.PrepayPercent > 100)
                throw new InvalidOperationException("Предоплата: от 0 до 100 %.");
            if (d.PayAfterBankDays < 0 || d.PayAfterBankDays > 99)
                throw new InvalidOperationException("Оплата через банковские дни: от 0 до 99.");
            if (d.LoadingDate != null && d.LoadingDate.Value.Date < DateTime.Today)
                throw new InvalidOperationException("Дата загрузки в прошлом.");

            var note = (d.Note ?? "").Trim();
            if (note.Length > 512)
                throw new InvalidOperationException("Примечание длиннее 512 символов.");

            // pay_attributes: 1 нал, 2 безнал, 8 с НДС, 16 предоплата, 64 оплата через N банковских дней
            var attrs = 0;
            if (d.Price > 0) attrs |= 1;
            if (d.NdsPrice > 0 || d.NotNdsPrice > 0) attrs |= 2;
            if (d.NdsPrice > 0) attrs |= 8;
            if (d.PrepayPercent > 0) attrs |= 16;
            if (d.PayAfterBankDays > 0) attrs |= 64;

            var body = new Dictionary<string, object?>();
            if (d.ContactId > 0) body["contact_id"] = d.ContactId;

            if (d.Price > 0)
            {
                body["price"] = d.Price;
                body["currency_id"] = 1;
            }

            if (d.NdsPrice > 0)
            {
                body["nds_price"] = d.NdsPrice;
                body["nds_percent"] = d.NdsPercent;
                body["nds_currency_id"] = 1;
            }

            if (d.NotNdsPrice > 0)
            {
                body["not_nds_price"] = d.NotNdsPrice;
                body["not_nds_currency_id"] = 1;
            }

            if (note.Length > 0) body["note"] = note;
            if (d.PrepayPercent > 0) body["prepay_percent"] = d.PrepayPercent;
            if (d.PayAfterBankDays > 0) body["pay_after_max_bank_days"] = d.PayAfterBankDays;
            if (d.LoadingDate != null) body["loading_date"] = d.LoadingDate.Value.ToString("yyyy-MM-dd");
            body["pay_attributes"] = attrs;

            return body;
        }

        private static AtiOffer? ParseSingleOffer(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var list = new List<AtiOffer>();
            using var doc = JsonDocument.Parse(raw);
            CollectOffers(doc.RootElement, list, 0);
            return list.FirstOrDefault();
        }

        private async Task DeleteMyOffersCore(string cargoId)
        {
            using var resp = await _http.DeleteAsync(OfferRoute(cargoId) + "/my");
            var text = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    "ATI " + (int)resp.StatusCode + ": " + ErrorText(text));
            }
        }

        /// Отправить своё предложение на чужой груз.
        public async Task<(AtiOffer? Offer, string Raw)> CreateCounterOfferAsync(string cargoId, AtiOfferDraft d)
        {
            var body = BuildOfferBody(d);
            var route = OfferRoute(cargoId);

            if (_opt.DryRun)
            {
                return (null, "DRY-RUN: запрос не отправлен.\nPOST " + route + "\n" +
                              JsonSerializer.Serialize(body, Pretty));
            }

            var raw = await PostAsync<string>(route, body);
            return (ParseSingleOffer(raw), raw);
        }

        /// Изменить своё предложение по его ID.
        public async Task<(AtiOffer? Offer, string Raw)> UpdateCounterOfferAsync(
            string cargoId, string offerId, AtiOfferDraft d)
        {
            var body = BuildOfferBody(d);
            var route = OfferRoute(cargoId) + "/" + Uri.EscapeDataString(offerId);

            if (_opt.DryRun)
            {
                return (null, "DRY-RUN: запрос не отправлен.\nPUT " + route + "\n" +
                              JsonSerializer.Serialize(body, Pretty));
            }

            var raw = await PutAsync<string>(route, body);
            return (ParseSingleOffer(raw), raw);
        }

        /// Заменить своё предложение, когда его ID неизвестен: отозвать все свои и отправить новое.
        /// Проверка полей идёт до отзыва, чтобы не потерять предложение из-за ошибки в форме.
        public async Task<(AtiOffer? Offer, string Raw)> ReplaceCounterOfferAsync(string cargoId, AtiOfferDraft d)
        {
            var body = BuildOfferBody(d);
            var route = OfferRoute(cargoId);

            if (_opt.DryRun)
            {
                return (null, "DRY-RUN: запрос не отправлен.\nDELETE " + route + "/my\nPOST " + route + "\n" +
                              JsonSerializer.Serialize(body, Pretty));
            }

            await DeleteMyOffersCore(cargoId);
            var raw = await PostAsync<string>(route, body);
            return (ParseSingleOffer(raw), raw);
        }

        /// Отозвать все свои предложения с чужого груза.
        public async Task DeleteMyCounterOffersAsync(string cargoId)
        {
            if (_opt.DryRun) return;
            await DeleteMyOffersCore(cargoId);
        }

        /// ID чужих грузов, на которых у нас есть активное встречное предложение.
        public async Task<HashSet<string>> GetMyOfferStatusesAsync(int days = 30)
        {
            var raw = await GetAsync<string>(OffersApi + "counter-offers/statuses?dateFrom=" +
                                             AtiDate(DateTime.UtcNow.AddDays(-days)));
            var set = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(raw)) return set;

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return set;

            foreach (var x in doc.RootElement.EnumerateArray())
            {
                var id = Str(x, "cargo_id");
                if (id != null && Bool(x, "has_active")) set.Add(id);
            }

            return set;
        }

        /// Одно встречное предложение по его ID.
        public async Task<AtiOffer?> GetCounterOfferAsync(string offerId)
        {
            var raw = await GetAsync<string>(OffersApi + "counter-offers/" + Uri.EscapeDataString(offerId));
            return ParseSingleOffer(raw);
        }

        // ---------- Поиск транспорта ----------

        public sealed record TruckSearchItem(
            string AtiId,
            string FirmName,
            int LoadingCityId,
            ulong CarType,
            decimal Weight,
            decimal Volume,
            DateTime? FirstDate,
            DateTime? LastDate,
            string Note,
            IReadOnlyList<string> Contacts);

        public async Task<(List<TruckSearchItem> Items, int Total, int Hidden)> SearchTrucksAsync(
            AtiLoad load, bool demo)
        {
            if (load.LoadingCityId <= 0 || load.UnloadingCityId <= 0)
                throw new InvalidOperationException("У груза должны быть указаны оба города.");

            var filter = new Dictionary<string, object>
            {
                ["from"] = new { id = load.LoadingCityId, type = 2, exact_only = true },
                ["to"] = new { id = load.UnloadingCityId, type = 2, exact_only = true },
                ["dates"] = new { date_option = "today-plus" }
            };

            if (load.Weight > 0) filter["weight"] = new { min = load.Weight };
            if (load.Volume > 0) filter["volume"] = new { min = load.Volume };
            if (load.CarTypeMask > 0) filter["truck_type"] = load.CarTypeMask;

            // Поиск использует POST, но не публикует и не изменяет груз.
            // В DryRun не отправляем реальный поиск.
            var useDemo = demo || IsDryRun;
            var raw = await PostAsync<string>(
                "v1.0/trucks/search/by-filter?demo=" + (useDemo ? "true" : "false"),
                new { page = 1, items_per_page = 20, filter });

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var trucks = Child(root, "trucks");

            if (trucks.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("ATI не вернул массив trucks.");

            var accounts = Child(root, "accounts");
            var items = new List<TruckSearchItem>();

            foreach (var t in trucks.EnumerateArray())
            {
                var atiId = Str(t, "ati_id") ?? "";
                var firm = Child(accounts, atiId);
                var transport = Child(t, "transport");
                var contacts = new List<string>();
                var contactsJson = Child(firm, "contacts");
                var firstContactId = Int(t, "contact_id1");
                var secondContactId = Int(t, "contact_id2");

                // Показываем только контакты, которые ATI вернул для фирмы
                // и которые указаны в объявлении машины.
                if (contactsJson.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in contactsJson.EnumerateArray())
                    {
                        var id = Int(c, "id");
                        if (id <= 0 || (id != firstContactId && id != secondContactId))
                            continue;

                        var text = string.Join(" · ", new[]
                        {
                            Str(c, "name"),
                            Str(c, "mobile"),
                            Str(c, "telephone")
                        }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());

                        if (text.Length > 0) contacts.Add(text);
                    }
                }

                items.Add(new TruckSearchItem(
                    atiId,
                    StrAny(firm, "full_firm_name", "firm_name") ?? "",
                    ChildInt(t, "loading", "city_id"),
                    ULong(transport, "car_type"),
                    Dec(transport, "weight") ?? 0,
                    Dec(transport, "volume") ?? 0,
                    Date(t, "first_date")?.UtcDateTime.Date,
                    Date(t, "last_date")?.UtcDateTime.Date,
                    Str(t, "note") ?? "",
                    contacts));
            }

            return (items, Int(root, "total_count"), Int(root, "hidden_count"));
        }

        // ---------- АТИ-Доки ----------

        /// Создать документ на груз в АТИ-Доках. Тип: заказ, заказ (табличный вид),
        /// заявка (к договору), заявка к договору (табличный вид), поручение экспедитору.
        public async Task<(string Id, string Url)> CreateAtiDocAsync(
            string cargoId, string documentType, string? templateId = null)
        {
            var route = "v1.0/loads/" + Uri.EscapeDataString(cargoId) +
                        "/atidocs/" + Uri.EscapeDataString(documentType);

            if (!string.IsNullOrWhiteSpace(templateId))
            {
                route += "?templateId=" + Uri.EscapeDataString(templateId);
            }

            if (_opt.DryRun) return ("", "");

            var raw = await PostAsync<string>(route, new { });
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new InvalidOperationException("ATI не вернул данные документа.");
            }

            using var doc = JsonDocument.Parse(raw);
            var url = StrAny(doc.RootElement, "Url") ?? "";
            if (url.Length == 0)
            {
                throw new InvalidOperationException("ATI не вернул ссылку на документ: " + raw);
            }

            return (StrAny(doc.RootElement, "Id") ?? "", url);
        }

        // ---------- Сборка тела ----------

        /// Структура подтверждена ответами валидатора v2:
        /// city_id — плоский, weight — { quantity, type: tons|kilos },
        /// cargos[].id и name обязательны, contacts — массив int.
        public static Dictionary<string, object?> BuildCargoApplication(AtiCargoDraft d)
        {
            var iso = d.LoadingDate.ToString("yyyy-MM-dd'T'HH:mm:sszzz");

            var cargo = new Dictionary<string, object?>
            {
                ["id"] = d.CargoId > 0 ? d.CargoId : 1,
                ["name"] = d.CargoName
            };

            if (d.Weight > 0)
            {
                cargo["weight"] = new { quantity = d.Weight, type = "tons" };
            }

            if (d.Volume > 0)
            {
                cargo["volume"] = new { quantity = d.Volume };
            }

            var loading = new Dictionary<string, object?>
            {
                ["city_id"] = d.LoadingCityId,
                ["dates"] = new Dictionary<string, object?>
                {
                    ["type"] = "from-date",
                    ["first_date"] = iso,
                    ["last_date"] = iso
                },
                ["cargos"] = new[] { cargo }
            };
            Put(loading, "region_id", d.LoadingRegionId > 0 ? d.LoadingRegionId : null);
            Put(loading, "country_id", d.LoadingCountryId > 0 ? d.LoadingCountryId : null);
            Put(loading, "address", d.LoadingAddress);

            var unloading = new Dictionary<string, object?>
            {
                ["city_id"] = d.UnloadingCityId
            };
            Put(unloading, "region_id", d.UnloadingRegionId > 0 ? d.UnloadingRegionId : null);
            Put(unloading, "country_id", d.UnloadingCountryId > 0 ? d.UnloadingCountryId : null);
            Put(unloading, "address", d.UnloadingAddress);

            var truck = new Dictionary<string, object?>
            {
                ["trucks_count"] = d.TrucksCount,
                ["load_type"] = d.LoadType,
                ["required_capacity"] = d.RequiredCapacity,

                ["body_loading"] = BuildBodyHandling(
                    d.BodyLoadingTypeIds,
                    d.IsAllBodyLoadingRequired),

                ["body_unloading"] = BuildBodyHandling(
                    d.BodyUnloadingTypeIds,
                    d.IsAllBodyUnloadingRequired)
            };

            var bodyTypeIds = d.BodyTypeIds
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            if (bodyTypeIds.Length > 0)
            {
                truck["body_types"] = d.BodyTypeIds;
            }

            var payment = new Dictionary<string, object?>
            {
                ["type"] = d.PaymentType,
                ["currency_type"] = d.CurrencyType
            };
            Put(payment, "rate_without_vat", d.RateWithoutVat);
            Put(payment, "cash", d.Cash);

            if (d.PaymentType == "rate-request")
            {
                payment["rate_with_nds_available"] = true;
                payment["rate_without_nds_available"] = true;
                payment["cash_available"] = true;
            }

            if (d.RateWithVat is not null)
            {
                payment["rates_with_vat"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["rate_with_vat"] = d.RateWithVat.Value,
                        ["vat_percent"] = d.VatPercent
                    }
                };
            }

            var app = new Dictionary<string, object?>
            {
                ["route"] = new Dictionary<string, object?>
                {
                    ["loading"] = loading,
                    ["unloading"] = unloading
                },
                ["truck"] = truck,
                ["payment"] = payment,
                ["contacts"] = new[] { d.ContactId }
            };

            var boardIds = d.BoardIds.Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().ToList();
            if (boardIds.Count > 0)
            {
                var mode = string.IsNullOrWhiteSpace(d.PublicationMode) ? "now" : d.PublicationMode;
                app["boards"] = boardIds
                    .Select(b => new { id = b.Trim(), publication_mode = mode })
                    .ToArray();
            }

            Put(app, "note", d.Note);

            return app;
        }

        private static void Put(Dictionary<string, object?> d, string key, object? value)
        {
            if (value is null) return;
            if (value is string s && string.IsNullOrWhiteSpace(s)) return;
            d[key] = value;
        }

        // ---------- Транспорт ----------

        private static string Need(string route, string name)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                throw new InvalidOperationException(
                    "Функция «" + name + "» не подключена: маршрут не задан в " +
                    "Ati:Endpoints:" + name + " (appsettings.json).");
            }

            return route;
        }

        private async Task<T> GetAsync<T>(string url)
        {
            var resp = await _http.GetAsync(url);
            return await ReadAsync<T>(resp);
        }

        private async Task<T> PostAsync<T>(string url, object body)
        {
            var resp = await _http.PostAsJsonAsync(url, body, Json);
            return await ReadAsync<T>(resp);
        }

        private async Task<T> PutAsync<T>(string url, object body)
        {
            var resp = await _http.PutAsJsonAsync(url, body, Json);
            return await ReadAsync<T>(resp);
        }

        private static async Task<T> ReadAsync<T>(HttpResponseMessage resp)
        {
            var text = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                throw BuildApiError(resp.StatusCode, text);
            }

            if (typeof(T) == typeof(string))
            {
                return (T)(object)text;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return default!;
            }

            return JsonSerializer.Deserialize<T>(text, Json) ?? default!;
        }

        /// Разбирает оба формата ошибок ATI:
        /// v2 — error_code / reason / error_list, v1.0 — Error / Reason / ErrorsList.
        private static AtiApiException BuildApiError(HttpStatusCode status, string body)
        {
            var http = "HTTP " + (int)status;

            if (status == HttpStatusCode.TooManyRequests)
            {
                return new AtiApiException(status, "too_many_requests",
                    "Превышен суточный лимит запросов ATI. Повторите позже.");
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    var code = StrAny(root, "error_code", "error") ?? http;
                    var reason = StrAny(root, "reason", "message");

                    var fields = new List<AtiFieldError>();
                    foreach (var listName in new[] { "error_list", "errors_list", "ErrorsList", "ErrorList", "details" })
                    {
                        var arr = ChildAny(root, listName);
                        if (arr.ValueKind != JsonValueKind.Array) continue;

                        foreach (var e in arr.EnumerateArray())
                        {
                            if (e.ValueKind != JsonValueKind.Object) continue;

                            fields.Add(new AtiFieldError
                            {
                                Property = StrAny(e, "property"),
                                Reason = StrAny(e, "reason"),
                                Error = StrAny(e, "error")
                            });
                        }
                    }

                    return new AtiApiException(status, code, reason, fields);
                }
            }
            catch (JsonException)
            {
                // тело не JSON — покажем как есть
            }

            var shortBody = string.IsNullOrWhiteSpace(body)
                ? null
                : (body.Length > 500 ? body.Substring(0, 500) + "…" : body);

            return new AtiApiException(status, http, shortBody);
        }

        // ---------- Чтение JSON ----------

        /// Вложенный объект или массив; если его нет — пустой элемент (ValueKind.Undefined).
        private static JsonElement Child(JsonElement el, string name)
            => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v)
                ? v
                : default;

        /// То же, но без учёта регистра имени.
        private static JsonElement ChildAny(JsonElement el, string name)
        {
            if (el.ValueKind != JsonValueKind.Object) return default;

            foreach (var p in el.EnumerateObject())
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p.Value;
            }

            return default;
        }

        /// Первый элемент массива или пустой элемент.
        private static JsonElement First(JsonElement arr)
        {
            if (arr.ValueKind != JsonValueKind.Array) return default;

            foreach (var e in arr.EnumerateArray()) return e;

            return default;
        }

        private static string? Str(JsonElement el, string name)
            => ToText(Child(el, name));

        private static string? StrAny(JsonElement el, params string[] names)
        {
            foreach (var n in names)
            {
                var s = ToText(ChildAny(el, n));
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }

            return null;
        }

        private static string? ToText(JsonElement v) => v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null
        };

        private static decimal? Dec(JsonElement el, string name)
        {
            var v = Child(el, name);

            if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;

            if (v.ValueKind == JsonValueKind.String &&
                decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var ds))
            {
                return ds;
            }

            return null;
        }

        private static int Int(JsonElement el, string name)
        {
            var d = Dec(el, name);
            return d is >= int.MinValue and <= int.MaxValue ? (int)d.Value : 0;
        }

        private static int ChildInt(JsonElement el, string child, string name)
            => Int(Child(el, child), name);

        private static ulong ULong(JsonElement el, string name)
        {
            var v = Child(el, name);

            if (v.ValueKind == JsonValueKind.Number && v.TryGetUInt64(out var u)) return u;

            if (v.ValueKind == JsonValueKind.String && ulong.TryParse(v.GetString(), out var us))
            {
                return us;
            }

            return 0;
        }

        private static bool Bool(JsonElement el, string name)
            => Child(el, name).ValueKind == JsonValueKind.True;

        private static DateTimeOffset? Date(JsonElement el, string name)
        {
            var v = Child(el, name);

            if (v.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(v.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var d))
            {
                return d;
            }

            return null;
        }
    }
}