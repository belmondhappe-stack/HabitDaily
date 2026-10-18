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