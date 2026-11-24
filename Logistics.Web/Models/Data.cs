using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace Logistics.Web.Models
{
    // ==================== ЗАЯВКИ ====================

    public class TaskComment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Author { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime Created { get; set; } = DateTime.Now;
        public bool IsEdited { get; set; }

        [JsonIgnore]
        public string CreatedAt => Created.ToString("dd.MM.yyyy HH:mm");
    }

    public class WorkTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = "";
        public string ClientName { get; set; } = "";
        public string Status { get; set; } = "Новая";
        public string Description { get; set; } = "";
        public DateTime? TaskDate { get; set; } = DateTime.Today;
        public DateTime? Deadline { get; set; }
        public bool IsHot { get; set; }
        public string LoadingPoint { get; set; } = "";
        public string UnloadingPoint { get; set; } = "";
        public string Volume { get; set; } = "";
        public string Inn { get; set; } = "";
        public string Kpp { get; set; } = "";

        public List<string> Vehicles { get; set; } = new();
        public List<string> Cargos { get; set; } = new();
        public List<string> LoadingMethods { get; set; } = new();
        public List<string> UnloadingMethods { get; set; } = new();
        public List<string> Executors { get; set; } = new();
        public List<string> Observers { get; set; } = new();

        public string TakenBy { get; set; } = "";
        public int ChangeCount { get; set; }

        public double? Cost { get; set; }
        public double CostKm { get; set; }
        public double CostSalary { get; set; }
        public double CostAvtodor { get; set; }
        public bool CostRoundTrip { get; set; }
        public bool CostLeasingTruck { get; set; }
        public bool CostLeasingTrailer { get; set; }

        public ObservableCollection<TaskComment> Comments { get; set; } = new();
    }

    public static class TaskStore
    {
        public static ObservableCollection<WorkTask> Tasks { get; } = new();

        public static void Notify(WorkTask? t, string title, string text)
        {
            if (t != null) t.ChangeCount++;
            NotificationStore.Add("Заявка", title, text);
        }
    }

    // ==================== КЛИЕНТЫ ====================

    public class ClientContact
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Fio { get; set; } = "";
        public string Position { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";

        [JsonIgnore]
        public string Display =>
            (string.IsNullOrWhiteSpace(Fio) ? "Без имени" : Fio)
            + (string.IsNullOrWhiteSpace(Position) ? "" : " | " + Position)
            + (string.IsNullOrWhiteSpace(Phone) ? "" : " | " + Phone)
            + (string.IsNullOrWhiteSpace(Email) ? "" : " | " + Email);
    }

    public class ClientRoute
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Loading { get; set; } = "";
        public string Unloading { get; set; } = "";

        [JsonIgnore]
        public string Display => Loading + " — " + Unloading;
    }

    public class Client
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string ShortDesc { get; set; } = "";
        public string Inn { get; set; } = "";
        public string Kpp { get; set; } = "";
        public string EdoId { get; set; } = "";

        public List<ClientContact> Contacts { get; set; } = new();
        public List<ClientRoute> Routes { get; set; } = new();

        public List<string> CargoTypes { get; set; } = new();
        public List<string> VehicleTypes { get; set; } = new();
        public List<string> LoadingMethods { get; set; } = new();
        public List<string> UnloadingMethods { get; set; } = new();
    }

    public static class ClientStore
    {
        public static ObservableCollection<Client> Clients { get; } = new();
    }

    // ==================== СОТРУДНИКИ И АККАУНТЫ ====================

    public class Employee
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FullName { get; set; } = "";
        public string Position { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Login { get; set; } = "";
        public string Password { get; set; } = "";

        [JsonIgnore]
        public string Role
        {
            get => Position;
            set => Position = value ?? "";
        }

        [JsonIgnore]
        public string Fio => FullName;

        [JsonIgnore]
        public string Initials
        {
            get
            {
                var p = (FullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length == 0) return "?";
                if (p.Length == 1) return p[0].Substring(0, 1).ToUpper();
                return (p[0][0].ToString() + p[1][0].ToString()).ToUpper();
            }
        }
    }

    public static class EmployeeStore
    {
        public static ObservableCollection<Employee> Employees { get; } = new();
    }

    public static class AuthStore
    {
        public static Employee? Current { get; set; }
        public static string? Error { get; private set; }
        public static bool IsAuthorized => Current != null;

        public const string RememberKey = "mplastRemember";
        public const string SavedLoginKey = "mplastSavedLogin";
        public const string SavedPassKey = "mplastSavedPass";
        public const string LogoutKey = "mplastLogout";

        public static void Logout()
        {
            Current = null;
            Error = null;
        }

        public static bool Login(string? login, string? password)
        {
            Error = null;

            var l = (login ?? "").Trim();

            if (l.Length == 0) { Error = "Введите логин"; return false; }
            if (string.IsNullOrEmpty(password)) { Error = "Введите пароль"; return false; }

            var emp = EmployeeStore.Employees.FirstOrDefault(x =>
                string.Equals(x.Login, l, StringComparison.OrdinalIgnoreCase));

            if (emp == null) { Error = "Пользователь с таким логином не найден"; return false; }
            if (!string.Equals(emp.Password, password, StringComparison.Ordinal)) { Error = "Неверный пароль"; return false; }

            Current = emp;
            return true;
        }

        public static bool Register(
            string? fio, string? position, string? phone, string? email,
            string? login, string? password, out string error)
        {
            error = "";

            var f = (fio ?? "").Trim();
            var p = (position ?? "").Trim();
            var ph = (phone ?? "").Trim();
            var em = (email ?? "").Trim();
            var lg = (login ?? "").Trim();
            var pw = password ?? "";

            if (f.Length < 5) { error = "Укажите ФИО полностью"; return false; }
            if (p.Length == 0) { error = "Укажите должность"; return false; }
            if (ph.Length < 6) { error = "Укажите номер телефона"; return false; }
            if (!em.Contains('@') || !em.Contains('.')) { error = "Проверьте правильность email"; return false; }
            if (lg.Length < 3) { error = "Логин — минимум 3 символа"; return false; }
            if (pw.Length < 4) { error = "Пароль — минимум 4 символа"; return false; }

            if (EmployeeStore.Employees.Any(x => string.Equals(x.Login, lg, StringComparison.OrdinalIgnoreCase)))
            { error = "Такой логин уже занят"; return false; }

            if (EmployeeStore.Employees.Any(x => string.Equals(x.Email, em, StringComparison.OrdinalIgnoreCase)))
            { error = "Такой email уже зарегистрирован"; return false; }

            var existed = EmployeeStore.Employees.ToList();

            var emp = new Employee
            {
                FullName = f,
                Position = p,
                Phone = ph,
                Email = em,
                Login = lg,
                Password = pw
            };

            EmployeeStore.Employees.Add(emp);

            const string hello = "Теперь я с вами в команде";

            foreach (var other in existed)
            {
                var dialog = ChatStore.GetPrivate(other.Login, emp.Login);
                ChatStore.Send(dialog, emp.FullName, hello);
            }

            if (existed.Count > 0)
                NotificationStore.Add("Чат", "Новый сотрудник", f + " присоединился к команде");

            Error = null;
            Current = emp;
            return true;
        }
    }

    // ==================== УВЕДОМЛЕНИЯ ====================

    public class AppNotification
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Date { get; set; } = DateTime.Now;
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        public bool IsRead { get; set; }

        [JsonIgnore]
        public string DateStr => Date.ToString("dd.MM.yyyy HH:mm");
    }

    public static class NotificationStore
    {
        public static ObservableCollection<AppNotification> Items { get; } = new();
        public static event Action? Changed;

        public static void Add(string type, string title, string text)
        {
            Items.Insert(0, new AppNotification { Type = type, Title = title, Text = text });
            Changed?.Invoke();
        }

        public static int UnreadCount() => Items.Count(x => !x.IsRead);

        public static void MarkRead(AppNotification n)
        {
            if (n.IsRead) return;
            n.IsRead = true;
            Changed?.Invoke();
        }

        public static void MarkAllRead()
        {
            foreach (var n in Items) n.IsRead = true;
            Changed?.Invoke();
        }

        public static void Remove(AppNotification n) { Items.Remove(n); Changed?.Invoke(); }
        public static void Clear() { Items.Clear(); Changed?.Invoke(); }
    }

    // ==================== ЧАТ ====================

    public class ChatMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Author { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime Sent { get; set; } = DateTime.Now;
        public bool IsEdited { get; set; }

        [JsonIgnore]
        public string Time => Sent.ToString("HH:mm");
    }

    public class ChatDialog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Kind { get; set; } = "group";
        public string Name { get; set; } = "";
        public List<string> Members { get; set; } = new();
        public List<ChatMessage> Messages { get; set; } = new();
        public List<string> ReadBy { get; set; } = new();

        [JsonIgnore]
        public bool IsGroup => Kind == "group";

        [JsonIgnore]
        public ChatMessage? Last => Messages.Count == 0 ? null : Messages.OrderBy(m => m.Sent).Last();
    }

    public static class ChatStore
    {
        public static ObservableCollection<ChatDialog> Dialogs { get; } = new();
        public static event Action? Changed;

        static void Raise() => Changed?.Invoke();

        public static IEnumerable<ChatDialog> Groups =>
            Dialogs.Where(d => d.IsGroup).OrderBy(d => d.Name);

        public static ChatDialog GetPrivate(string loginA, string loginB)
        {
            var d = Dialogs.FirstOrDefault(x =>
                !x.IsGroup && x.Members.Count == 2 &&
                x.Members.Contains(loginA) && x.Members.Contains(loginB));

            if (d == null)
            {
                d = new ChatDialog { Kind = "private", Members = new List<string> { loginA, loginB } };
                Dialogs.Add(d);
                Raise();
            }

            return d;
        }

        public static ChatDialog CreateGroup(string name, IEnumerable<string> members)
        {
            var d = new ChatDialog
            {
                Kind = "group",
                Name = (name ?? "").Trim(),
                Members = members.Distinct().ToList()
            };
            Dialogs.Add(d);
            Raise();
            return d;
        }

        public static void Rename(ChatDialog d, string name) { d.Name = (name ?? "").Trim(); Raise(); }
        public static void SetMembers(ChatDialog d, IEnumerable<string> members) { d.Members = members.Distinct().ToList(); Raise(); }

        public static void Send(ChatDialog d, string author, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            d.Messages.Add(new ChatMessage { Author = author, Text = text.Trim() });
            Raise();
        }

        public static void EditMessage(ChatDialog d, ChatMessage m, string text)
        {
            m.Text = (text ?? "").Trim();
            m.IsEdited = true;
            Raise();
        }

        public static void DeleteMessage(ChatDialog d, ChatMessage m)
        {
            d.Messages.Remove(m);
            Raise();
        }

        public static int UnreadFor(ChatDialog d, string? myFio)
        {
            if (myFio == null) return 0;
            return d.Messages.Count(m =>
                !string.Equals(m.Author, myFio, StringComparison.Ordinal) &&
                !d.ReadBy.Contains(m.Id.ToString()));
        }

        public static int UnreadTotal(string? myFio) =>
            myFio == null ? 0 : Dialogs.Sum(d => UnreadFor(d, myFio));

        public static void MarkRead(ChatDialog d, string? myFio)
        {
            if (myFio == null) return;

            var unread = d.Messages
                .Where(m => !string.Equals(m.Author, myFio, StringComparison.Ordinal))
                .Select(m => m.Id.ToString())
                .Where(id => !d.ReadBy.Contains(id))
                .ToList();

            if (unread.Count == 0) return;

            d.ReadBy.AddRange(unread);
            Raise();
        }

        public static void Remove(ChatDialog d) { Dialogs.Remove(d); Raise(); }
    }

    // ==================== СПРАВОЧНИКИ ====================

    public static class Dictionaries
    {
        public static List<string> VehicleTypes { get; } = new()
        {
            "тентованный", "контейнер", "фургон", "цельнометалл.", "изотермический",
            "реф.+изотерм", "рефрижератор", "реф. с перегородкой", "реф. мультирежимный",
            "реф.-тушевоз", "все открытые", "бортовой", "открытый конт.", "самосвал",
            "шаланда", "площадка без бортов", "негабарит", "низкорамный", "трал",
            "низкорам.платф.", "телескопический", "балковоз(негабарит)", "автобус",
            "автовоз", "автовышка", "автотранспортер", "бетоновоз", "битумовоз",
            "бензовоз", "вездеход", "газовоз", "зерновоз", "коневоз", "контейнеровоз",
            "кормовоз", "кран", "лесовоз", "ломовоз", "манипулятор", "микроавтобус",
            "муковоз", "панелевоз", "пикап", "пухтовоз", "пирамида", "рулоновоз",
            "седельный тягач", "скотовоз", "стекловоз", "трубовоз", "цементовоз",
            "автоцистерна", "щеповоз", "эвакуатор", "грузопассажирский", "клюшковоз",
            "мусоровоз", "jumbo", "20' танк-контейнер", "40' танк-контейнер",
            "мега фура", "допельшток", "Раздвижной полуприцеп 20'/40'"
        };

        public static List<string> CargoTypes { get; } = new()
        {
            "Автомобиль(ли)", "Автошины", "Алкогольные напитки", "Арматура",
            "Балки надрессорные", "Безалкогольные напитки", "Боковая рама", "Бумага",
            "Бытовая техника", "Бытовая химия", "Вагонка", "Газосиликатные блоки",
            "Гипс", "Гофрокартон", "Грибы", "ДВП", "ДСП", "Двери", "Домашний переезд",
            "Доски", "Древесина", "Древесный уголь", "Ж/д запчасти (прочие)", "ЖБИ",
            "Животные", "Зерно и семена (в упаковке)", "Зерно и семена (насыпью)",
            "Игрушки", "Изделия из кожи", "Изделия из металла", "Изделия из резины",
            "Инструмент", "Кабель", "Канц. товары", "Кирпич", "Ковры", "Колесная пара",
            "Компьютеры", "Кондитерские изделия", "Консервы", "Кормовые/пищевые добавки",
            "Крупа", "ЛДСП", "Люди", "Макулатура", "Мебель", "Медикаменты", "Мел",
            "Металл", "Металлолом", "Металлопрокат", "Минвата", "Молоко сухое",
            "Мороженое", "Мука", "Мясо", "Нефтепродукты", "Оборудование и запчасти",
            "Оборудование медицинское", "Обувь", "Овощи", "Огнеупорная продукция",
            "Одежда", "Парфюмерия и косметика", "Пенопласт", "Песок", "Пиво",
            "Пиломатериалы", "Пластик", "Поглощающий аппарат", "Поддоны",
            "Продукты питания", "Профлист", "Птица", "Рыба (неживая)", "СОНК (КП)",
            "Сантехника", "Сахар", "Сборный груз", "Соки", "Соль", "Стекло и фарфор",
            "Стеклотара (бутылки и др.)", "Стройматериалы", "Сэндвич-панели", "ТНП",
            "Табачные изделия", "Тара и упаковка", "Текстиль", "Торф",
            "Транспортные средства", "Трубы", "Удобрения", "Утеплитель", "Фанера",
            "Ферросплавы", "Фрукты", "Хим. продукты неопасные", "Хим. продукты опасные",
            "Хозтовары", "Холодильное оборудование", "Цветы", "Цемент", "Чипсы",
            "Шкуры мокросоленые", "Шпалы", "Щебень", "Электроника", "Ягоды",
            "20' контейнер", "20' контейнер HC", "20' реф.контейнер",
            "20' танк-контейнер", "40' контейнер", "40' контейнер HC",
            "40' реф.контейнер", "40' реф.контейнер HC", "40' танк-контейнер",
            "45' контейнер (нов.)", "45' контейнер (стар.)", "45' реф.контейнер"
        };

        public static List<string> LoadingMethods { get; } = new()
        {
            "верхняя", "боковая", "задняя", "с полной растентовкой",
            "со снятием поперечных перекладин", "со снятием стоек", "без ворот",
            "гидроборт", "аппарели", "с обрешеткой", "с бортами",
            "боковая с 2-х сторон", "налив", "электрический", "гидравлический",
            "пневматический", "дизельный компрессор"
        };
    }
}