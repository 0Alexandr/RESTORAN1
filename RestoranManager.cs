// Файл: RestoranManager.cs
// Основной класс управления данными ресторана
// Содержит все логики работы с данными, сохранение/загрузку, проверку конфликтов и т.д.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Restoran1
{
    /// <summary>
    /// Класс Table — описывает стол в ресторане.
    /// </summary>
    class Table
    {
        public int Id { get; set; } = 0;
        public string Location { get; set; } = string.Empty;
        public int Seats { get; set; } = 0;

        public string ToShortString() => $"ID {Id} | {Location} | Мест: {Seats}";
    }

    /// <summary>
    /// Класс Reservation — описание брони.
    /// </summary>
    class Reservation
    {
        public int Id { get; set; } = 0;
        public int ClientId { get; set; } = 0;
        public string ClientName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime Start { get; set; } = DateTime.MinValue;
        public DateTime End { get; set; } = DateTime.MinValue;
        public string Comment { get; set; } = string.Empty;
        public int TableId { get; set; } = 0;

        public bool Overlaps(DateTime s, DateTime e)
        {
            return Start < e && s < End;
        }

        public bool Covers(DateTime t) => Start <= t && t < End;

        public string ToShortString()
        {
            var comment = string.IsNullOrWhiteSpace(Comment) ? "пусто" : Comment;
            return $"ID {Id} | КлиентID {ClientId} | Стол {TableId} | {ClientName} ({Phone}) | {Start:yyyy-MM-dd HH:mm} — {End:yyyy-MM-dd HH:mm} | Комментарий: {comment}";
        }
    }

    /// <summary>
    /// Перечисление категорий блюд.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    enum DishCategory
    {
        Напитки,
        Салаты,
        ХолодныеЗакуски,
        ГорячиеЗакуски,
        Супы,
        ГорячиеБлюда,
        Десерт,
        Другое
    }

    /// <summary>
    /// Класс Dish — блюдо меню.
    /// </summary>
    class Dish
    {
        public int Id { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public string Composition { get; set; } = string.Empty;
        public string Weight { get; set; } = string.Empty;
        public decimal Price { get; set; } = 0m;
        public DishCategory Category { get; set; } = DishCategory.Другое;
        public int CookTimeMinutes { get; set; } = 0;

        public string ToShortString() => $"ID {Id} | {Name} | {Category} | {Price:0.00} руб.";
    }

    /// <summary>
    /// Элемент заказа: ссылка на DishId и количество.
    /// </summary>
    class OrderItem
    {
        public int DishId { get; set; } = 0;
        public int Quantity { get; set; } = 0;
    }

    /// <summary>
    /// Класс Order — заказ, привязанный к ClientId и столу.
    /// </summary>
    class Order
    {
        public int Id { get; set; } = 0;
        public int ClientId { get; set; } = 0;
        public int TableId { get; set; } = 0;
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.MinValue;
        public int WaiterId { get; set; } = 0;
        public DateTime? ClosedAt { get; set; } = null;
        public decimal Total { get; set; } = 0m;

        public bool IsClosed => ClosedAt.HasValue;

        public string ToShortString()
        {
            var comment = string.IsNullOrWhiteSpace(Comment) ? "пусто" : Comment;
            return $"ID {Id} | КлиентID {ClientId} | Стол {TableId} | Позиции: {Items.Sum(i => i.Quantity)} | Создан: {CreatedAt:yyyy-MM-dd HH:mm} | {(IsClosed ? "Закрыт" : "Открыт")} | Комментарий: {comment}";
        }
    }

    /// <summary>
    /// Класс Config хранит путь к папке данных.
    /// </summary>
    class Config
    {
        public string DataPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// RestoranManager — основной менеджер приложения.
    /// </summary>
    class RestoranManager
    {
        // Следующие ID
        public int NextTableId => Tables.Any() ? Tables.Max(t => t.Id) + 1 : 1;
        public int NextReservationId => Reservations.Any() ? Reservations.Max(r => r.Id) + 1 : 1;
        public int NextDishId => Dishes.Any() ? Dishes.Max(d => d.Id) + 1 : 1;
        public int NextOrderId => Orders.Any() ? Orders.Max(o => o.Id) + 1 : 1;

        // Файлы
        private const string CONFIG_FILE = "config.json";
        private Config config = new Config();
        private readonly string defaultDataPath = @"C:\Restoran_Data";

        public string DataPath => string.IsNullOrWhiteSpace(config.DataPath) ? defaultDataPath : config.DataPath;
        private string TablesFile => Path.Combine(DataPath, "tables.json");
        private string ReservationsFile => Path.Combine(DataPath, "reservations.json");
        private string DishesFile => Path.Combine(DataPath, "dishes.json");
        private string OrdersFile => Path.Combine(DataPath, "orders.json");

        // JSON опции
        private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        // Коллекции
        public List<Table> Tables { get; private set; } = new List<Table>();
        public List<Reservation> Reservations { get; private set; } = new List<Reservation>();
        public List<Dish> Dishes { get; private set; } = new List<Dish>();
        public List<Order> Orders { get; private set; } = new List<Order>();

        // Виртуальное время
        public DateTime VirtualNow { get; set; } = DateTime.Now;

        public RestoranManager()
        {
            LoadConfig();
            EnsureDataFolderExists();
            LoadAll();
        }

        #region Config

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(CONFIG_FILE))
                {
                    var json = File.ReadAllText(CONFIG_FILE);
                    config = JsonSerializer.Deserialize<Config>(json, jsonOptions) ?? new Config();
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                var json = JsonSerializer.Serialize(config, jsonOptions);
                File.WriteAllText(CONFIG_FILE, json);
            }
            catch { }
        }

        private void EnsureDataFolderExists()
        {
            try
            {
                if (!Directory.Exists(DataPath))
                    Directory.CreateDirectory(DataPath);
            }
            catch { }
        }

        public void SetDataPath(string newPath)
        {
            config.DataPath = newPath;
            SaveConfig();
            EnsureDataFolderExists();
        }

        #endregion

        #region Load/Save

        public void LoadAll()
        {
            Tables = LoadOrCreate<Table>(TablesFile);
            Reservations = LoadOrCreate<Reservation>(ReservationsFile);
            Dishes = LoadOrCreate<Dish>(DishesFile);
            Orders = LoadOrCreate<Order>(OrdersFile);
        }

        private List<T> LoadOrCreate<T>(string path)
        {
            try
            {
                if (!File.Exists(path)) return new List<T>();
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<T>>(json, jsonOptions) ?? new List<T>();
            }
            catch
            {
                return new List<T>();
            }
        }

        public void SaveAll()
        {
            EnsureDataFolderExists();
            Save(Tables, TablesFile);
            Save(Reservations, ReservationsFile);
            Save(Dishes, DishesFile);
            Save(Orders, OrdersFile);
        }

        private void Save<T>(List<T> list, string path)
        {
            try
            {
                var json = JsonSerializer.Serialize(list, jsonOptions);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        #endregion

        #region Test Data

        public void InitDefaults()
        {
            Tables = new List<Table>
            {
                new Table { Id = 1, Location = "у окна", Seats = 4 },
                new Table { Id = 2, Location = "у прохода", Seats = 2 },
                new Table { Id = 3, Location = "в глубине", Seats = 6 },
                new Table { Id = 4, Location = "у выхода", Seats = 4 },
            };

            Dishes = new List<Dish>
            {
                new Dish { Id = 1, Name = "Кофе Американо", Composition="вода, кофе", Weight="200", Price=120m, Category=DishCategory.Напитки, CookTimeMinutes=5 },
                new Dish { Id = 2, Name = "Цезарь с курицей", Composition="салат, курица, соус", Weight="250", Price=420m, Category=DishCategory.Салаты, CookTimeMinutes=15 },
                new Dish { Id = 3, Name = "Суп грибной", Composition="грибы, бульон", Weight="300", Price=280m, Category=DishCategory.Супы, CookTimeMinutes=20 },
                new Dish { Id = 4, Name = "Чизкейк", Composition="сыр, печенье", Weight="120", Price=350m, Category=DishCategory.Десерт, CookTimeMinutes=30 }
            };

            Reservations = new List<Reservation>
            {
                new Reservation { Id = 1, ClientId = 101, ClientName="Макс", Phone="88005553535", Start = DateTime.Today.AddHours(12), End = DateTime.Today.AddHours(15), Comment="День рождения", TableId = 3 },
                new Reservation { Id = 2, ClientId = 102, ClientName="Анна", Phone="5745552377", Start = DateTime.Today.AddHours(16), End = DateTime.Today.AddHours(17), Comment="Деловая встреча", TableId = 3 }
            };

            Orders = new List<Order>
            {
                new Order
                {
                    Id = 1, ClientId = 101, TableId = 3,
                    Items = new List<OrderItem> { new OrderItem{ DishId = 2, Quantity = 2 }, new OrderItem{ DishId = 3, Quantity = 1 } },
                    CreatedAt = DateTime.Now.AddHours(-2), WaiterId = 1, ClosedAt = DateTime.Now.AddHours(-1),
                    Total = 2 * 420m + 1 * 280m, Comment = "Оплата наличными"
                },
                new Order
                {
                    Id = 2, ClientId = 102, TableId = 1,
                    Items = new List<OrderItem> { new OrderItem{ DishId = 1, Quantity = 3 } },
                    CreatedAt = DateTime.Now.AddMinutes(-30), WaiterId = 2, Comment = ""
                }
            };

            SaveAll();
        }

        #endregion

        #region Table Info

        public string GetTableInfoString(int id)
        {
            var t = Tables.FirstOrDefault(x => x.Id == id);
            if (t == null) return "Стол не найден.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"ID: {t.Id}");
            sb.AppendLine($"Расположение: {t.Location}");
            sb.AppendLine($"Количество мест: {t.Seats}");
            sb.AppendLine("Расписание бронирований:");

            var tableRes = Reservations.Where(r => r.TableId == t.Id).OrderBy(r => r.Start).ToList();
            if (!tableRes.Any())
            {
                sb.AppendLine("  Нет бронирований.");
            }
            else
            {
                foreach (var r in tableRes)
                {
                    var activeMark = r.Covers(VirtualNow) ? " (СЕЙЧАС ЗАНЯТ)" : string.Empty;
                    var comment = string.IsNullOrWhiteSpace(r.Comment) ? "пусто" : r.Comment;
                    sb.AppendLine($"  {r.Start:yyyy-MM-dd HH:mm} — {r.End:yyyy-MM-dd HH:mm}{activeMark}  | ID брони {r.Id} | КлиентID {r.ClientId} | {r.ClientName} | Тел: {r.Phone} | Комментарий: {comment}");
                }
            }

            return sb.ToString();
        }

        #endregion
    }
}