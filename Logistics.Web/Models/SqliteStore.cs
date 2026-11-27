using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Logistics.Web.Models
{
    public static class SqliteStore
    {
        private static string DbPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logistics.db");
        private static string ConnectionString => $"Data Source={DbPath}";
        private static bool _isLoaded = false;

        private const string AtiNotificationsKey = "AtiNotifications";

        public static void Initialize() => Init();

        public static void Init()


        {
            if (_isLoaded) return;

            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();

                // Проверяем наличие корректной структуры таблицы; если колонка JsonData отсутствует — пересоздаем таблицу
                bool recreateTable = false;
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='AppState';";
                var tableExists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

                if (tableExists)
                {
                    var colCmd = connection.CreateCommand();
                    colCmd.CommandText = "PRAGMA table_info(AppState);";
                    using var reader = colCmd.ExecuteReader();
                    bool hasJsonData = false;
                    while (reader.Read())
                    {
                        if (string.Equals(reader.GetString(1), "JsonData", StringComparison.OrdinalIgnoreCase))
                        {
                            hasJsonData = true;
                            break;
                        }
                    }
                    if (!hasJsonData) recreateTable = true;
                }

                if (recreateTable)
                {
                    var dropCmd = connection.CreateCommand();
                    dropCmd.CommandText = "DROP TABLE IF EXISTS AppState;";
                    dropCmd.ExecuteNonQuery();
                }

                var createCmd = connection.CreateCommand();
                createCmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS AppState (
                        Key TEXT PRIMARY KEY,
                        JsonData TEXT
                    );";
                createCmd.ExecuteNonQuery();

                LoadCollection(connection, "Tasks", TaskStore.Tasks);
                LoadCollection(connection, "Clients", ClientStore.Clients);
                LoadCollection(connection, "Employees", EmployeeStore.Employees);
                LoadCollection(connection, "Dialogs", ChatStore.Dialogs);
                LoadCollection(connection, "Notifications", NotificationStore.Items);
                LoadAtiNotifications(connection); // ← добавь эту строку

            }

            TaskStore.Tasks.CollectionChanged += (s, e) => SaveAll();
            ClientStore.Clients.CollectionChanged += (s, e) => SaveAll();
            EmployeeStore.Employees.CollectionChanged += (s, e) => SaveAll();
            ChatStore.Dialogs.CollectionChanged += (s, e) => SaveAll();

            _isLoaded = true;
        }

        private static void LoadCollection<T>(SqliteConnection connection, string key, ObservableCollection<T> collection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT JsonData FROM AppState WHERE Key = @key";
            cmd.Parameters.AddWithValue("@key", key);

            using var reader = cmd.ExecuteReader();
            if (reader.Read() && !reader.IsDBNull(0))
            {
                var json = reader.GetString(0);
                if (!string.IsNullOrEmpty(json))
                {
                    var items = JsonSerializer.Deserialize<List<T>>(json);
                    if (items != null)
                    {
                        collection.Clear();
                        foreach (var item in items)
                        {
                            collection.Add(item);
                        }
                    }
                }
            }
        }

        private static void LoadAtiNotifications(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT JsonData FROM AppState WHERE Key = @key;";
            cmd.Parameters.AddWithValue("@key", AtiNotificationsKey);

            if (cmd.ExecuteScalar() is not string json || string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                var items = JsonSerializer.Deserialize<List<AtiNotification>>(json);
                if (items != null)
                    AtiInbox.Restore(items);
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine("[ATI] Не удалось загрузить уведомления: " + ex.Message);
            }
        }

        internal static void SaveAtiNotifications(IEnumerable<AtiNotification> items)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            SaveCollection(connection, AtiNotificationsKey, items);
        }

        public static void SaveAll()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                SaveCollection(connection, "Tasks", TaskStore.Tasks);
                SaveCollection(connection, "Clients", ClientStore.Clients);
                SaveCollection(connection, "Employees", EmployeeStore.Employees);
                SaveCollection(connection, "Dialogs", ChatStore.Dialogs);
                SaveCollection(connection, "Notifications", NotificationStore.Items);
            }
            catch
            {
                // Подавление ошибок параллельного доступа
            }
        }

        private static void SaveCollection<T>(SqliteConnection connection, string key, IEnumerable<T> collection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO AppState (Key, JsonData) 
                VALUES (@key, @data)
                ON CONFLICT(Key) DO UPDATE SET JsonData = excluded.JsonData;";

            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@data", JsonSerializer.Serialize(collection.ToList()));
            cmd.ExecuteNonQuery();
        }
    }
}