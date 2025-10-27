// Файл: MainWindow.axaml.cs
// Полный код-behind для главного окна приложения
// Все ошибки исправлены: out, ItemsSource, null-проверки, CS8618
// Готов к dotnet build и dotnet run

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace Restoran1
{
    public partial class MainWindow : Window
    {
        private readonly RestoranManager _manager;

        public MainWindow()
        {
            InitializeComponent();
            _manager = new RestoranManager();
            UpdateGrids();
            UpdateVirtualNowText();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // Обновление всех таблиц данными (с проверкой на null)
        private void UpdateGrids()
        {
            Dispatcher.UIThread.Post(() =>
            {
                var tablesGrid = this.FindControl<DataGrid>("TablesGrid");
                if (tablesGrid != null)
                    tablesGrid.ItemsSource = _manager.Tables.OrderBy(t => t.Id).ToList();

                var reservationsGrid = this.FindControl<DataGrid>("ReservationsGrid");
                if (reservationsGrid != null)
                    reservationsGrid.ItemsSource = _manager.Reservations.OrderBy(r => r.Start).ToList();

                var dishesGrid = this.FindControl<DataGrid>("DishesGrid");
                if (dishesGrid != null)
                    dishesGrid.ItemsSource = _manager.Dishes
                        .GroupBy(d => d.Category)
                        .SelectMany(g => g.OrderBy(d => d.Id))
                        .ToList();

                var ordersGrid = this.FindControl<DataGrid>("OrdersGrid");
                if (ordersGrid != null)
                    ordersGrid.ItemsSource = _manager.Orders.OrderBy(o => o.CreatedAt).ToList();
            });
        }

        // Обновление текста виртуального времени (с проверкой на null)
        private void UpdateVirtualNowText()
        {
            Dispatcher.UIThread.Post(() =>
            {
                var textBlock = this.FindControl<TextBlock>("VirtualNowText");
                if (textBlock != null)
                    textBlock.Text = $"Текущее виртуальное время: {_manager.VirtualNow:yyyy-MM-dd HH:mm}";
            });
        }

        // Вспомогательные методы для диалогов
        private async Task<int> GetIntInput(string title, string message, int defaultValue = 0)
        {
            var dialog = new InputDialog(title, message, defaultValue.ToString());
            await dialog.ShowDialog(this);
            return int.TryParse(dialog.Result, out int value) ? value : defaultValue;
        }

        private async Task<string?> GetStringInput(string title, string message, string defaultValue = "")
        {
            var dialog = new InputDialog(title, message, defaultValue);
            await dialog.ShowDialog(this);
            return dialog.Result;
        }

        private async Task<decimal> GetDecimalInput(string title, string message, decimal defaultValue = 0m)
        {
            var dialog = new InputDialog(title, message, defaultValue.ToString(CultureInfo.InvariantCulture));
            await dialog.ShowDialog(this);
            return decimal.TryParse(dialog.Result, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value)
                ? value
                : defaultValue;
        }

        private async Task ShowMessage(string title, string message)
        {
            var msgDialog = new MessageDialog(title, message);
            await msgDialog.ShowDialog(this);
        }

        private async Task<bool> Confirm(string title, string message)
        {
            var confirmDialog = new ConfirmDialog(title, message);
            await confirmDialog.ShowDialog(this);
            return confirmDialog.Result;
        }

        // === ОБРАБОТЧИКИ КНОПОК ===

        // --- СТОЛЫ ---
        private async void AddTable_Click(object sender, RoutedEventArgs e)
        {
            var location = await GetStringInput("Добавить стол", "Расположение (например, у окна):");
            if (string.IsNullOrWhiteSpace(location)) return;

            var seats = await GetIntInput("Добавить стол", "Количество мест:", 4);
            if (seats <= 0) { await ShowMessage("Ошибка", "Количество мест должно быть больше 0."); return; }

            var table = new Table
            {
                Id = _manager.NextTableId,
                Location = location,
                Seats = seats
            };

            _manager.Tables.Add(table);
            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", $"Стол добавлен. ID: {table.Id}");
        }

        private async void EditTable_Click(object sender, RoutedEventArgs e)
        {
            var id = await GetIntInput("Редактировать стол", "ID стола:");
            var table = _manager.Tables.FirstOrDefault(t => t.Id == id);
            if (table == null) { await ShowMessage("Ошибка", "Стол не найден."); return; }

            if (_manager.Reservations.Any(r => r.TableId == id && r.Covers(_manager.VirtualNow)))
            {
                await ShowMessage("Ошибка", "Нельзя редактировать стол, который сейчас занят.");
                return;
            }

            var newLocation = await GetStringInput("Редактировать стол", $"Текущее расположение: {table.Location}\nНовое:", table.Location);
            if (string.IsNullOrWhiteSpace(newLocation)) return;

            var newSeats = await GetIntInput("Редактировать стол", $"Текущие места: {table.Seats}\nНовые:", table.Seats);
            if (newSeats <= 0) { await ShowMessage("Ошибка", "Количество мест должно быть больше 0."); return; }

            table.Location = newLocation;
            table.Seats = newSeats;
            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", "Стол обновлён.");
        }

        private async void DeleteTable_Click(object sender, RoutedEventArgs e)
        {
            var id = await GetIntInput("Удалить стол", "ID стола:");
            var table = _manager.Tables.FirstOrDefault(t => t.Id == id);
            if (table == null) { await ShowMessage("Ошибка", "Стол не найден."); return; }

            if (_manager.Reservations.Any(r => r.TableId == id))
            {
                await ShowMessage("Ошибка", "Нельзя удалить стол, на который есть бронирования.");
                return;
            }

            var confirm = await Confirm("Подтверждение", $"Удалить стол ID {id} ({table.Location})?");
            if (!confirm) return;

            _manager.Tables.Remove(table);
            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", "Стол удалён.");
        }

        private async void ShowTableInfo_Click(object sender, RoutedEventArgs e)
        {
            var id = await GetIntInput("Информация о столе", "ID стола:");
            var info = _manager.GetTableInfoString(id);
            await ShowMessage("Информация о столе", info);
        }

        // --- БРОНИРОВАНИЯ ---
        private async void AddReservation_Click(object sender, RoutedEventArgs e)
        {
            if (!_manager.Tables.Any())
            {
                await ShowMessage("Ошибка", "Сначала добавьте хотя бы один стол.");
                return;
            }

            var tableId = await GetIntInput("Добавить бронь", "ID стола:");
            if (!_manager.Tables.Any(t => t.Id == tableId))
            {
                await ShowMessage("Ошибка", "Стол не найден.");
                return;
            }

            var clientId = await GetIntInput("Добавить бронь", "ID клиента:");
            var name = await GetStringInput("Добавить бронь", "Имя клиента:");
            if (string.IsNullOrWhiteSpace(name)) { await ShowMessage("Ошибка", "Имя не может быть пустым."); return; }

            var phone = await GetStringInput("Добавить бронь", "Телефон:");
            if (string.IsNullOrWhiteSpace(phone)) { await ShowMessage("Ошибка", "Телефон не может быть пустым."); return; }

            var startStr = await GetStringInput("Добавить бронь", "Начало (yyyy-MM-dd HH:mm):");
            if (!DateTime.TryParseExact(startStr, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime start))
            {
                await ShowMessage("Ошибка", "Неверный формат даты начала.");
                return;
            }

            var endStr = await GetStringInput("Добавить бронь", "Конец (yyyy-MM-dd HH:mm):");
            if (!DateTime.TryParseExact(endStr, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime end))
            {
                await ShowMessage("Ошибка", "Неверный формат даты конца.");
                return;
            }

            if (end <= start) { await ShowMessage("Ошибка", "Конец брони должен быть после начала."); return; }

            var comment = await GetStringInput("Добавить бронь", "Комментарий (опционально):");

            var reservation = new Reservation
            {
                Id = _manager.NextReservationId,
                ClientId = clientId,
                ClientName = name,
                Phone = phone,
                Start = start,
                End = end,
                Comment = comment ?? string.Empty,
                TableId = tableId
            };

            if (_manager.Reservations.Any(r => r.TableId == tableId && r.Overlaps(start, end)))
            {
                await ShowMessage("Ошибка", "Стол занят в это время.");
                return;
            }

            _manager.Reservations.Add(reservation);
            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", $"Бронь добавлена. ID: {reservation.Id}");
        }

        private async void EditReservation_Click(object sender, RoutedEventArgs e)
        {
            var id = await GetIntInput("Редактировать бронь", "ID брони:");
            var res = _manager.Reservations.FirstOrDefault(r => r.Id == id);
            if (res == null) { await ShowMessage("Ошибка", "Бронь не найдена."); return; }

            var newStartStr = await GetStringInput("Редактировать бронь", $"Текущее начало: {res.Start:yyyy-MM-dd HH:mm}\nНовое:", res.Start.ToString("yyyy-MM-dd HH:mm"));
            if (!DateTime.TryParseExact(newStartStr, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime newStart))
            {
                await ShowMessage("Ошибка", "Неверный формат.");
                return;
            }

            var newEndStr = await GetStringInput("Редактировать бронь", $"Текущий конец: {res.End:yyyy-MM-dd HH:mm}\nНовый:", res.End.ToString("yyyy-MM-dd HH:mm"));
            if (!DateTime.TryParseExact(newEndStr, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime newEnd))
            {
                await ShowMessage("Ошибка", "Неверный формат.");
                return;
            }

            if (newEnd <= newStart) { await ShowMessage("Ошибка", "Конец должен быть после начала."); return; }

            if (_manager.Reservations.Any(r => r.Id != id && r.TableId == res.TableId && r.Overlaps(newStart, newEnd)))
            {
                await ShowMessage("Ошибка", "Стол занят в новое время.");
                return;
            }

            res.Start = newStart;
            res.End = newEnd;
            res.Comment = await GetStringInput("Редактировать бронь", $"Текущий комментарий: {res.Comment}\nНовый:", res.Comment) ?? string.Empty;

            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", "Бронь обновлена.");
        }

        private async void CancelReservation_Click(object sender, RoutedEventArgs e)
        {
            var id = await GetIntInput("Отменить бронь", "ID брони:");
            var res = _manager.Reservations.FirstOrDefault(r => r.Id == id);
            if (res == null) { await ShowMessage("Ошибка", "Бронь не найдена."); return; }

            var confirm = await Confirm("Подтверждение", $"Отменить бронь ID {id}?");
            if (!confirm) return;

            _manager.Reservations.Remove(res);
            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", "Бронь отменена.");
        }

        private async void FindReservation_Click(object sender, RoutedEventArgs e)
        {
            var search = await GetStringInput("Поиск брони", "Имя или телефон:");
            if (string.IsNullOrWhiteSpace(search)) return;

            var results = _manager.Reservations
                .Where(r => r.ClientName.Contains(search, StringComparison.OrdinalIgnoreCase) || r.Phone.Contains(search))
                .ToList();

            if (!results.Any())
            {
                await ShowMessage("Поиск", "Ничего не найдено.");
                return;
            }

            var sb = new StringBuilder("Результаты поиска:\n");
            foreach (var r in results)
            {
                sb.AppendLine(r.ToShortString());
            }
            await ShowMessage("Поиск брони", sb.ToString());
        }

        private async void ExtendReservation_Click(object sender, RoutedEventArgs e)
        {
            var id = await GetIntInput("Продлить бронь", "ID брони:");
            var res = _manager.Reservations.FirstOrDefault(r => r.Id == id);
            if (res == null) { await ShowMessage("Ошибка", "Бронь не найдена."); return; }

            var newEndStr = await GetStringInput("Продлить бронь", $"Текущий конец: {res.End:yyyy-MM-dd HH:mm}\nНовый конец:", res.End.ToString("yyyy-MM-dd HH:mm"));
            if (!DateTime.TryParseExact(newEndStr, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime newEnd))
            {
                await ShowMessage("Ошибка", "Неверный формат.");
                return;
            }

            if (newEnd <= res.End) { await ShowMessage("Ошибка", "Новый конец должен быть позже текущего."); return; }

            if (_manager.Reservations.Any(r => r.Id != id && r.TableId == res.TableId && r.Overlaps(res.End, newEnd)))
            {
                await ShowMessage("Ошибка", "Стол занят после текущего конца.");
                return;
            }

            res.End = newEnd;
            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", "Бронь продлена.");
        }

        // --- МЕНЮ (БЛЮДА) ---
        private async void AddDish_Click(object sender, RoutedEventArgs e)
        {
            var name = await GetStringInput("Добавить блюдо", "Название:");
            if (string.IsNullOrWhiteSpace(name)) return;

            var composition = await GetStringInput("Добавить блюдо", "Состав:");
            var weight = await GetStringInput("Добавить блюдо", "Вес (например, 250г):");
            var price = await GetDecimalInput("Добавить блюдо", "Цена:", 0m);
            if (price <= 0) { await ShowMessage("Ошибка", "Цена должна быть больше 0."); return; }

            var categoryStr = await GetStringInput("Добавить блюдо", "Категория (Напитки, Салаты, etc.):");
            if (!Enum.TryParse<DishCategory>(categoryStr, out var category))
                category = DishCategory.Другое;

            var cookTime = await GetIntInput("Добавить блюдо", "Время готовки (минуты):", 0);

            var dish = new Dish
            {
                Id = _manager.NextDishId,
                Name = name,
                Composition = composition ?? string.Empty,
                Weight = weight ?? string.Empty,
                Price = price,
                Category = category,
                CookTimeMinutes = cookTime
            };

            _manager.Dishes.Add(dish);
            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", $"Блюдо добавлено. ID: {dish.Id}");
        }

        private async void EditDish_Click(object sender, RoutedEventArgs e)
        {
            var id = await GetIntInput("Редактировать блюдо", "ID блюда:");
            var dish = _manager.Dishes.FirstOrDefault(d => d.Id == id);
            if (dish == null) { await ShowMessage("Ошибка", "Блюдо не найдено."); return; }

            dish.Name = await GetStringInput("Редактировать блюдо", $"Текущее название: {dish.Name}\nНовое:", dish.Name) ?? string.Empty;
            dish.Composition = await GetStringInput("Редактировать блюдо", $"Текущий состав: {dish.Composition}\nНовый:", dish.Composition) ?? string.Empty;
            dish.Weight = await GetStringInput("Редактировать блюдо", $"Текущий вес: {dish.Weight}\nНовый:", dish.Weight) ?? string.Empty;
            dish.Price = await GetDecimalInput("Редактировать блюдо", $"Текущая цена: {dish.Price}\nНовая:", dish.Price);

            var categoryStr = await GetStringInput("Редактировать блюдо", $"Текущая категория: {dish.Category}\nНовая:", dish.Category.ToString());
            if (!Enum.TryParse<DishCategory>(categoryStr, out var category))
                category = DishCategory.Другое;
            dish.Category = category;

            dish.CookTimeMinutes = await GetIntInput("Редактировать блюдо", $"Текущее время: {dish.CookTimeMinutes}\nНовое:", dish.CookTimeMinutes);

            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", "Блюдо обновлено.");
        }

        private async void DeleteDish_Click(object sender, RoutedEventArgs e)
        {
            var id = await GetIntInput("Удалить блюдо", "ID блюда:");
            var dish = _manager.Dishes.FirstOrDefault(d => d.Id == id);
            if (dish == null) { await ShowMessage("Ошибка", "Блюдо не найдено."); return; }

            if (_manager.Orders.Any(o => o.Items.Any(i => i.DishId == id)))
            {
                await ShowMessage("Ошибка", "Нельзя удалить блюдо, которое есть в заказах.");
                return;
            }

            var confirm = await Confirm("Подтверждение", $"Удалить блюдо ID {id} ({dish.Name})?");
            if (!confirm) return;

            _manager.Dishes.Remove(dish);
            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", "Блюдо удалено.");
        }

        // --- ЗАКАЗЫ ---
        private async void CreateOrder_Click(object sender, RoutedEventArgs e)
        {
            var clientId = await GetIntInput("Создать заказ", "ID клиента:");
            var tableId = await GetIntInput("Создать заказ", "ID стола:");
            if (!_manager.Tables.Any(t => t.Id == tableId)) { await ShowMessage("Ошибка", "Стол не найден."); return; }

            if (!_manager.Reservations.Any(r => r.ClientId == clientId && r.TableId == tableId && r.Covers(_manager.VirtualNow)))
            {
                await ShowMessage("Ошибка", "Нет активной брони для этого клиента за этим столом.");
                return;
            }

            var order = new Order
            {
                Id = _manager.NextOrderId,
                ClientId = clientId,
                TableId = tableId,
                CreatedAt = _manager.VirtualNow,
                Items = new List<OrderItem>(),
                WaiterId = await GetIntInput("Создать заказ", "ID официанта:", 1),
                Comment = await GetStringInput("Создать заказ", "Комментарий:") ?? string.Empty
            };

            _manager.Orders.Add(order);
            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", $"Заказ создан. ID: {order.Id}");
        }

        private async void EditOrder_Click(object sender, RoutedEventArgs e)
        {
            var id = await GetIntInput("Редактировать заказ", "ID заказа:");
            var order = _manager.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) { await ShowMessage("Ошибка", "Заказ не найден."); return; }
            if (order.IsClosed) { await ShowMessage("Ошибка", "Нельзя редактировать закрытый заказ."); return; }

            var dishId = await GetIntInput("Добавить позицию", "ID блюда:");
            var quantity = await GetIntInput("Добавить позицию", "Количество:", 1);
            if (quantity <= 0) return;

            if (!_manager.Dishes.Any(d => d.Id == dishId)) { await ShowMessage("Ошибка", "Блюдо не найдено."); return; }

            order.Items.Add(new OrderItem { DishId = dishId, Quantity = quantity });
            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", "Позиция добавлена.");
        }

        private async void CloseOrder_Click(object sender, RoutedEventArgs e)
        {
            var id = await GetIntInput("Закрыть заказ", "ID заказа:");
            var order = _manager.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) { await ShowMessage("Ошибка", "Заказ не найден."); return; }
            if (order.IsClosed) { await ShowMessage("Ошибка", "Заказ уже закрыт."); return; }

            // БЕЗОПАСНЫЙ РАСЧЁТ СУММЫ
            order.Total = order.Items.Sum(i =>
            {
                var dish = _manager.Dishes.FirstOrDefault(d => d.Id == i.DishId);
                return i.Quantity * (dish?.Price ?? 0m);
            });

            order.ClosedAt = _manager.VirtualNow;

            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", $"Заказ закрыт. Итого: {order.Total:0.00} руб.");
        }

        private async void DeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            var id = await GetIntInput("Удалить заказ", "ID заказа:");
            var order = _manager.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) { await ShowMessage("Ошибка", "Заказ не найден."); return; }

            var confirm = await Confirm("Подтверждение", $"Удалить заказ ID {id}?");
            if (!confirm) return;

            _manager.Orders.Remove(order);
            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Успех", "Заказ удалён.");
        }

        // --- СТАТИСТИКА ---
        private async void SumClosedOrders_Click(object sender, RoutedEventArgs e)
        {
            var sum = _manager.Orders.Where(o => o.IsClosed).Sum(o => o.Total);
            await ShowMessage("Сумма закрытых заказов", $"Общая сумма: {sum:0.00} руб.");
        }

        private async void PrintClientCheck_Click(object sender, RoutedEventArgs e)
        {
            var clientId = await GetIntInput("Чек клиента", "ID клиента:");
            var clientOrders = _manager.Orders.Where(o => o.ClientId == clientId && o.IsClosed).ToList();
            if (!clientOrders.Any())
            {
                await ShowMessage("Чек", "У клиента нет закрытых заказов.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Чек для клиента ID {clientId}");
            sb.AppendLine("====================================");
            foreach (var o in clientOrders)
            {
                sb.AppendLine($"Заказ ID {o.Id} | {o.ClosedAt:yyyy-MM-dd HH:mm} | Итого: {o.Total:0.00} руб.");
            }
            sb.AppendLine("====================================");
            sb.AppendLine($"ИТОГО: {clientOrders.Sum(o => o.Total):0.00} руб.");

            await ShowMessage("Чек клиента", sb.ToString());
        }

        private async void StatsDishCounts_Click(object sender, RoutedEventArgs e)
        {
            var stats = _manager.Orders
                .Where(o => o.IsClosed)
                .SelectMany(o => o.Items)
                .GroupBy(i => i.DishId)
                .Select(g => new
                {
                    Dish = _manager.Dishes.FirstOrDefault(d => d.Id == g.Key),
                    Count = g.Sum(i => i.Quantity)
                })
                .Where(x => x.Dish != null)
                .OrderByDescending(x => x.Count)
                .ToList();

            if (!stats.Any())
            {
                await ShowMessage("Статистика", "Нет данных по закрытым заказам.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Статистика блюд (по количеству):");
            foreach (var s in stats)
            {
                sb.AppendLine($"{s.Dish.Name} — {s.Count} шт.");
            }
            await ShowMessage("Статистика блюд", sb.ToString());
        }

        // --- НАСТРОЙКИ ---
        private async void SetDataPath_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Выберите папку данных" });
            if (folder.Count == 0) return;

            var newPath = folder[0].Path.LocalPath;
            var currentPath = _manager.DataPath;

            if (newPath != currentPath)
            {
                var transfer = await Confirm("Перенос данных", "Скопировать существующие данные в новую папку?");
                if (transfer)
                {
                    var files = new[] { "tables.json", "reservations.json", "dishes.json", "orders.json" };
                    foreach (var file in files)
                    {
                        var src = Path.Combine(currentPath, file);
                        var dest = Path.Combine(newPath, file);
                        if (File.Exists(src))
                        {
                            try { File.Copy(src, dest, true); } catch { }
                        }
                    }
                }
            }

            _manager.SetDataPath(newPath);
            _manager.LoadAll();
            UpdateGrids();
            await ShowMessage("Успех", "Папка данных изменена.");
        }

        private async void SaveData_Click(object sender, RoutedEventArgs e)
        {
            _manager.SaveAll();
            await ShowMessage("Сохранение", "Данные сохранены.");
        }

        private async void LoadData_Click(object sender, RoutedEventArgs e)
        {
            _manager.LoadAll();
            UpdateGrids();
            await ShowMessage("Загрузка", "Данные загружены.");
        }

        private async void ClearAllData_Click(object sender, RoutedEventArgs e)
        {
            var confirm = await Confirm("ОЧИСТКА", "Удалить ВСЕ данные? Это действие нельзя отменить!");
            if (!confirm) return;

            _manager.Tables.Clear();
            _manager.Reservations.Clear();
            _manager.Dishes.Clear();
            _manager.Orders.Clear();
            _manager.SaveAll();
            UpdateGrids();
            await ShowMessage("Очистка", "Все данные удалены.");
        }

        private async void InitDefaults_Click(object sender, RoutedEventArgs e)
        {
            var confirm = await Confirm("Тестовые данные", "Создать тестовые данные? Текущие данные будут заменены.");
            if (!confirm) return;

            _manager.InitDefaults();
            UpdateGrids();
            await ShowMessage("Успех", "Тестовые данные созданы.");
        }

        private async void SetVirtualNow_Click(object sender, RoutedEventArgs e)
        {
            var input = await GetStringInput("Виртуальное время", "Новое время (yyyy-MM-dd HH:mm) или пусто для текущего:");
            if (string.IsNullOrWhiteSpace(input))
            {
                _manager.VirtualNow = DateTime.Now;
            }
            else if (DateTime.TryParseExact(input, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                _manager.VirtualNow = dt;
            }
            else
            {
                await ShowMessage("Ошибка", "Неверный формат времени.");
                return;
            }

            UpdateVirtualNowText();
            UpdateGrids();
            await ShowMessage("Успех", "Виртуальное время обновлено.");
        }
    }

    // === ДИАЛОГОВЫЕ ОКНА ===
    public class InputDialog : Window
    {
        public string Result { get; private set; } = string.Empty; // ИСПРАВЛЕНО: CS8618

        public InputDialog(string title, string message, string defaultValue = "")
        {
            Title = title;
            Width = 350;
            Height = 150;
            CanResize = false;

            var stack = new StackPanel { Spacing = 10, Margin = new Thickness(15) };
            stack.Children.Add(new TextBlock { Text = message });
            var tb = new TextBox { Text = defaultValue, Margin = new Thickness(0, 5, 0, 0) };
            stack.Children.Add(tb);

            var btn = new Button
            {
                Content = "ОК",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };
            btn.Click += (s, e) =>
            {
                Result = tb.Text ?? string.Empty;
                Close();
            };

            stack.Children.Add(btn);
            Content = stack;

            Opened += (s, e) => tb.Focus();
        }
    }

    public class MessageDialog : Window
    {
        public MessageDialog(string title, string message)
        {
            Title = title;
            Width = 450;
            Height = 250;
            CanResize = false;

            var stack = new StackPanel { Spacing = 15, Margin = new Thickness(15) };
            var tb = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            stack.Children.Add(tb);

            var btn = new Button
            {
                Content = "ОК",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            btn.Click += (s, e) => Close();

            stack.Children.Add(btn);
            Content = stack;
        }
    }

    public class ConfirmDialog : Window
    {
        public bool Result { get; private set; } = false;

        public ConfirmDialog(string title, string message)
        {
            Title = title;
            Width = 350;
            Height = 150;
            CanResize = false;

            var stack = new StackPanel { Spacing = 10, Margin = new Thickness(15) };
            stack.Children.Add(new TextBlock { Text = message });

            var panel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            var yes = new Button { Content = "Да" };
            yes.Click += (s, e) => { Result = true; Close(); };

            var no = new Button { Content = "Нет" };
            no.Click += (s, e) => { Result = false; Close(); };

            panel.Children.Add(yes);
            panel.Children.Add(no);
            stack.Children.Add(panel);
            Content = stack;
        }
    }
}