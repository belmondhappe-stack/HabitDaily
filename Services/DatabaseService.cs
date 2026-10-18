using SQLite;
using HabitDaily.Models;
using System.Globalization;
using System.Security.Cryptography;

namespace HabitDaily.Services
{
    public class DatabaseService
    {
        private static SQLiteAsyncConnection? _database;
        private static readonly SemaphoreSlim InitializationLock = new(1, 1);

        private const string CurrentUserIdPreferenceKey = "CurrentUserId";
        private const string PasswordHashPrefix = "pbkdf2-sha256";
        private const int PasswordHashIterations = 600_000;
        private const int PasswordSaltSize = 16;
        private const int PasswordHashSize = 32;

        private static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, "habitdaily.db3");

        public static async Task Initialize()
        {
            if (_database != null)
                return;

            await InitializationLock.WaitAsync();
            try
            {
                if (_database != null)
                    return;

                var database = new SQLiteAsyncConnection(DatabasePath);

                await database.CreateTableAsync<User>();
                await database.CreateTableAsync<Habit>();
                await database.CreateTableAsync<HabitLog>();

                var hasCo2ReductionColumn = await database.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM pragma_table_info('Habits') WHERE name = ?",
                    nameof(Habit.Co2ReductionPerAction));
                if (hasCo2ReductionColumn == 0)
                {
                    await database.ExecuteAsync(
                        "ALTER TABLE Habits ADD COLUMN Co2ReductionPerAction REAL NOT NULL DEFAULT 0.2");
                }

                var legacyUsersTableExists = await database.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Users'");
                if (legacyUsersTableExists > 0)
                {
                    await database.ExecuteAsync(
                        "INSERT OR IGNORE INTO User " +
                        "(Id, FullName, Email, PasswordHash, CurrentStreak, BestStreak, SuccessRate, TotalCo2Saved, LevelBadge) " +
                        "SELECT Id, FullName, Email, PasswordHash, CurrentStreak, BestStreak, SuccessRate, TotalCo2Saved, LevelBadge " +
                        "FROM Users");
                }

                _database = database;
            }
            finally
            {
                InitializationLock.Release();
            }
        }

        public static async Task Init() => await Initialize();

        private static async Task<SQLiteAsyncConnection> GetDatabaseAsync()
        {
            await Initialize();
            return _database ?? throw new InvalidOperationException("La base de données n'a pas été initialisée.");
        }

        // --- GESTION DES HABITUDES ---
        public static async Task<List<Habit>> GetHabitsAsync()
        {
            var database = await GetDatabaseAsync();
            return await database.Table<Habit>().ToListAsync();
        }

        public static async Task<List<Habit>> GetHabitsByUserAsync(int userId)
        {
            var database = await GetDatabaseAsync();
            return await database.Table<Habit>().Where(habit => habit.UserId == userId).ToListAsync();
        }

        public static async Task<(User User, List<Habit> Habits, List<HabitLog> Logs)> GetDashboardDataAsync(int userId)
        {
            var database = await GetDatabaseAsync();
            var user = await database.FindAsync<User>(userId)
                ?? throw new InvalidOperationException("L'utilisateur connecté n'existe plus.");
            var habits = await GetHabitsByUserAsync(userId);
            var logs = await GetCompletedHabitLogsByUserAsync(userId);

            RecalculateUserStatistics(user, habits, logs);
            await SaveUserAsync(user);

            return (user, habits, logs);
        }

        public static async Task SaveHabitCompletionAsync(int userId, int habitId, bool isCompleted, DateTime logDate)
        {
            var database = await GetDatabaseAsync();
            var date = logDate.Date;

            await database.RunInTransactionAsync(connection =>
            {
                var user = connection.Find<User>(userId)
                    ?? throw new InvalidOperationException("L'utilisateur connecté n'existe plus.");
                var habit = connection.Find<Habit>(habitId);
                if (habit == null || habit.UserId != userId)
                    throw new InvalidOperationException("Cette habitude n'appartient pas à l'utilisateur connecté.");

                var existingLog = connection.Table<HabitLog>()
                    .Where(log => log.HabitId == habitId && log.LogDate == date)
                    .FirstOrDefault();

                if (isCompleted && existingLog == null)
                {
                    connection.Insert(new HabitLog
                    {
                        HabitId = habitId,
                        LogDate = date,
                        IsCompleted = true
                    });
                }
                else if (!isCompleted && existingLog != null)
                {
                    connection.Delete(existingLog);
                }

                var habits = connection.Table<Habit>()
                    .Where(item => item.UserId == user.Id)
                    .ToList();
                var logs = GetCompletedHabitLogsByUser(connection, user.Id);
                RecalculateUserStatistics(user, habits, logs);
                connection.Update(user);
            });
        }

        public static async Task<List<HabitLogDetails>> GetHabitLogsByDateRangeAsync(
            int userId,
            DateTime startDate,
            DateTime endDateExclusive)
        {
            if (endDateExclusive <= startDate)
                throw new ArgumentOutOfRangeException(nameof(endDateExclusive), "La date de fin doit être après la date de début.");

            var database = await GetDatabaseAsync();
            return await database.QueryAsync<HabitLogDetails>(
                "SELECT HabitLogs.Id, HabitLogs.HabitId, HabitLogs.LogDate, HabitLogs.IsCompleted, " +
                "Habits.Title AS HabitTitle, Habits.IconName, Habits.HexColor, Habits.Co2ReductionPerAction " +
                "FROM HabitLogs " +
                "INNER JOIN Habits ON Habits.Id = HabitLogs.HabitId " +
                "WHERE Habits.UserId = ? AND HabitLogs.IsCompleted = 1 " +
                "AND HabitLogs.LogDate >= ? AND HabitLogs.LogDate < ? " +
                "ORDER BY HabitLogs.LogDate, Habits.Title",
                userId,
                startDate.Date,
                endDateExclusive.Date);
        }

        public static async Task<int> SaveHabitAsync(Habit habit)
        {
            ArgumentNullException.ThrowIfNull(habit);
            var database = await GetDatabaseAsync();
            if (habit.Id != 0)
            {
                return await database.UpdateAsync(habit);
            }
            else
            {
                return await database.InsertAsync(habit);
            }
        }

        public static async Task<int> DeleteHabitAsync(Habit habit)
        {
            ArgumentNullException.ThrowIfNull(habit);
            var database = await GetDatabaseAsync();
            return await database.DeleteAsync(habit);
        }

        // --- GESTION DE L'UTILISATEUR ---
        public static async Task<User?> GetUserAsync()
        {
            var database = await GetDatabaseAsync();
            return await database.Table<User>().FirstOrDefaultAsync();
        }

        public static async Task<User?> GetCurrentUserAsync()
        {
            if (!Preferences.ContainsKey(CurrentUserIdPreferenceKey))
                return null;

            var userId = Preferences.Get(CurrentUserIdPreferenceKey, 0);
            if (userId <= 0)
                return null;

            var database = await GetDatabaseAsync();
            return await database.FindAsync<User>(userId);
        }

        public static void SetCurrentUser(User user)
        {
            ArgumentNullException.ThrowIfNull(user);
            if (user.Id <= 0)
                throw new ArgumentException("L'utilisateur doit être enregistré avant l'ouverture de session.", nameof(user));

            Preferences.Set(CurrentUserIdPreferenceKey, user.Id);
        }

        public static void ClearCurrentUser() =>
            Preferences.Remove(CurrentUserIdPreferenceKey);

        private static async Task<List<HabitLog>> GetCompletedHabitLogsByUserAsync(int userId)
        {
            var database = await GetDatabaseAsync();
            return await database.QueryAsync<HabitLog>(
                "SELECT HabitLogs.* FROM HabitLogs " +
                "INNER JOIN Habits ON Habits.Id = HabitLogs.HabitId " +
                "WHERE Habits.UserId = ? AND HabitLogs.IsCompleted = 1",
                userId);
        }

        private static List<HabitLog> GetCompletedHabitLogsByUser(SQLiteConnection connection, int userId)
        {
            return connection.Query<HabitLog>(
                "SELECT HabitLogs.* FROM HabitLogs " +
                "INNER JOIN Habits ON Habits.Id = HabitLogs.HabitId " +
                "WHERE Habits.UserId = ? AND HabitLogs.IsCompleted = 1",
                userId);
        }

        private static void RecalculateUserStatistics(User user, List<Habit> habits, List<HabitLog> logs)
        {
            var habitsById = habits.ToDictionary(habit => habit.Id);
            var logsByHabit = logs.GroupBy(log => log.HabitId)
                .ToDictionary(group => group.Key, group => group.Select(log => log.LogDate.Date).Distinct().ToList());
            var currentStreak = 0;
            var bestStreak = 0;

            foreach (var habit in habits)
            {
                var dates = logsByHabit.TryGetValue(habit.Id, out var completedDates)
                    ? completedDates
                    : new List<DateTime>();
                habit.CurrentStreak = CalculateCurrentStreak(dates, DateTime.Today);
                currentStreak = Math.Max(currentStreak, habit.CurrentStreak);
                bestStreak = Math.Max(bestStreak, CalculateLongestStreak(dates));
            }

            user.TotalCo2Saved = Math.Round(
                logs.Where(log => habitsById.ContainsKey(log.HabitId))
                    .Sum(log => habitsById[log.HabitId].Co2ReductionPerAction),
                2);
            user.CurrentStreak = currentStreak;
            user.BestStreak = Math.Max(user.BestStreak, bestStreak);
        }

        private static int CalculateCurrentStreak(IEnumerable<DateTime> completedDates, DateTime today)
        {
            var dates = completedDates.Select(date => date.Date).ToHashSet();
            var currentDate = dates.Contains(today.Date) ? today.Date : today.Date.AddDays(-1);
            var streak = 0;

            while (dates.Contains(currentDate))
            {
                streak++;
                currentDate = currentDate.AddDays(-1);
            }

            return streak;
        }

        private static int CalculateLongestStreak(IEnumerable<DateTime> completedDates)
        {
            var dates = completedDates.Select(date => date.Date).Distinct().Order().ToList();
            var longestStreak = 0;
            var currentStreak = 0;
            DateTime? previousDate = null;

            foreach (var date in dates)
            {
                currentStreak = previousDate.HasValue && date == previousDate.Value.AddDays(1)
                    ? currentStreak + 1
                    : 1;
                longestStreak = Math.Max(longestStreak, currentStreak);
                previousDate = date;
            }

            return longestStreak;
        }

        public static async Task<User?> GetUserByEmailAsync(string email)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(email);
            var database = await GetDatabaseAsync();
            
            var users = await database.QueryAsync<User>(
                "SELECT * FROM User WHERE Email = ? COLLATE NOCASE LIMIT 1",
                email.Trim());

            return users.FirstOrDefault();
        }

        public static async Task<int> SaveUserAsync(User user)
        {
            ArgumentNullException.ThrowIfNull(user);
            var database = await GetDatabaseAsync();

            user.Email = user.Email.Trim();
            if (!string.IsNullOrEmpty(user.PasswordHash) && !user.PasswordHash.StartsWith(PasswordHashPrefix + "$", StringComparison.Ordinal))
            {
                user.PasswordHash = HashPassword(user.PasswordHash);
            }

            if (user.Id != 0)
            {
                return await database.UpdateAsync(user);
            }
            else
            {
                return await database.InsertAsync(user);
            }
        }

        public static string HashPassword(string password)
        {
            ArgumentNullException.ThrowIfNull(password);

            var salt = RandomNumberGenerator.GetBytes(PasswordSaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                PasswordHashIterations,
                HashAlgorithmName.SHA256,
                PasswordHashSize);

            return string.Join(
                '$',
                PasswordHashPrefix,
                PasswordHashIterations.ToString(CultureInfo.InvariantCulture),
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        public static bool VerifyPassword(string password, string storedPassword, out bool needsRehash)
        {
            ArgumentNullException.ThrowIfNull(password);
            ArgumentNullException.ThrowIfNull(storedPassword);

            needsRehash = false;
            var prefix = PasswordHashPrefix + "$";
            if (!storedPassword.StartsWith(prefix, StringComparison.Ordinal))
            {
                needsRehash = true;
                return string.Equals(password, storedPassword, StringComparison.Ordinal);
            }

            var parts = storedPassword.Split('$');
            if (parts.Length != 4
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
                || iterations < 100_000
                || iterations > 2_000_000)
            {
                return false;
            }

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expectedHash = Convert.FromBase64String(parts[3]);
                if (salt.Length != PasswordSaltSize || expectedHash.Length != PasswordHashSize)
                    return false;

                var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expectedHash.Length);

                var isValid = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
                needsRehash = isValid && iterations != PasswordHashIterations;
                return isValid;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}