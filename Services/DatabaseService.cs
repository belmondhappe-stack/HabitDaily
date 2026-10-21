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
        private const int CurrentSchemaVersion = 4;

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
                await database.CreateTableAsync<Goal>();
                await database.CreateTableAsync<Habit>();
                await database.CreateTableAsync<HabitLog>();
                await database.CreateTableAsync<Category>();
                await database.CreateTableAsync<UserProfileType>();

                var schemaVersion = await database.ExecuteScalarAsync<int>("PRAGMA user_version");
                if (schemaVersion < CurrentSchemaVersion)
                {
                    await database.RunInTransactionAsync(connection =>
                    {
                        EnsureColumn(connection, "Habits", nameof(Habit.Co2ReductionPerAction), "REAL NOT NULL DEFAULT 0.2");
                        var addedColor = EnsureColumn(connection, "Habits", nameof(Habit.Color), "TEXT NOT NULL DEFAULT '#00E676'");
                        EnsureColumn(connection, "Habits", nameof(Habit.GoalId), "INTEGER NULL");
                        EnsureColumn(connection, "Habits", nameof(Habit.Category), "TEXT NOT NULL DEFAULT 'Autre'");
                        var addedIcon = EnsureColumn(connection, "Habits", nameof(Habit.Icon), "TEXT NOT NULL DEFAULT '🌱'");
                        var addedFrequencyType = EnsureColumn(connection, "Habits", nameof(Habit.FrequencyType), "TEXT NOT NULL DEFAULT 'Quotidienne'");
                        var addedSelectedDays = EnsureColumn(connection, "Habits", nameof(Habit.SelectedDays), "TEXT NOT NULL DEFAULT ''");
                        EnsureColumn(connection, "Habits", nameof(Habit.QuotaCount), "INTEGER NOT NULL DEFAULT 0");
                        EnsureColumn(connection, "Habits", nameof(Habit.ScheduledTime), "TIME NULL");
                        var addedStartDate = EnsureColumn(connection, "Habits", nameof(Habit.StartDate), "DATETIME NOT NULL DEFAULT '2000-01-01 00:00:00'");
                        EnsureColumn(connection, "Habits", nameof(Habit.EndDate), "DATETIME NULL");
                        EnsureColumn(connection, "Habits", nameof(Habit.IsArchived), "INTEGER NOT NULL DEFAULT 0");

                        if (addedColor)
                            connection.Execute("UPDATE Habits SET Color = HexColor");
                        if (addedIcon)
                            connection.Execute("UPDATE Habits SET Icon = IconName");
                        if (addedFrequencyType)
                            connection.Execute("UPDATE Habits SET FrequencyType = Frequency");
                        if (addedStartDate)
                            connection.Execute("UPDATE Habits SET StartDate = CreatedAt");

                        var addedDate = EnsureColumn(connection, "HabitLogs", nameof(HabitLog.Date), "DATETIME NOT NULL DEFAULT '2000-01-01 00:00:00'");
                        var addedUserId = EnsureColumn(connection, "HabitLogs", nameof(HabitLog.UserId), "INTEGER NOT NULL DEFAULT 0");
                        var addedStatus = EnsureColumn(connection, "HabitLogs", nameof(HabitLog.Status), "TEXT NOT NULL DEFAULT 'À faire'");
                        EnsureColumn(connection, "HabitLogs", nameof(HabitLog.ScheduledTime), "TIME NULL");
                        EnsureColumn(connection, "HabitLogs", nameof(HabitLog.RescheduledTime), "TIME NULL");
                        EnsureColumn(connection, "HabitLogs", nameof(HabitLog.ActionTime), "TIME NULL");
                        EnsureColumn(connection, "HabitLogs", nameof(HabitLog.ReportCount), "INTEGER NOT NULL DEFAULT 0");
                        var addedCo2Saved = EnsureColumn(connection, "HabitLogs", nameof(HabitLog.Co2Saved), "REAL NOT NULL DEFAULT 0");

                        if (addedDate)
                            connection.Execute("UPDATE HabitLogs SET Date = LogDate");
                        if (addedUserId)
                            connection.Execute(
                                "UPDATE HabitLogs SET UserId = (SELECT UserId FROM Habits WHERE Habits.Id = HabitLogs.HabitId)");
                        if (addedStatus)
                            connection.Execute(
                                "UPDATE HabitLogs SET Status = CASE WHEN IsCompleted = 1 THEN 'Faite' ELSE 'À faire' END");
                        if (addedCo2Saved)
                            connection.Execute(
                                "UPDATE HabitLogs SET Co2Saved = COALESCE((SELECT Co2ReductionPerAction FROM Habits WHERE Habits.Id = HabitLogs.HabitId), 0)");

                        if (addedFrequencyType || addedSelectedDays || schemaVersion < CurrentSchemaVersion)
                            MigrateLegacyHabits(connection);

                        connection.Execute($"PRAGMA user_version = {CurrentSchemaVersion}");
                    });
                }

                await SeedReferenceDataAsync(database);

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

        private static bool EnsureColumn(
            SQLiteConnection connection,
            string tableName,
            string columnName,
            string columnDefinition)
        {
            var columnExists = connection.ExecuteScalar<int>(
                $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name = ?",
                columnName);
            if (columnExists > 0)
                return false;

            connection.Execute(
                $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}");
            return true;
        }

        private static void MigrateLegacyHabits(SQLiteConnection connection)
        {
            connection.Execute(
                "UPDATE Habits SET Category = 'Autre' WHERE Category IS NULL OR TRIM(Category) = ''");

            foreach (var habit in connection.Table<Habit>().ToList())
            {
                var legacyFrequency = habit.Frequency?.Trim() ?? string.Empty;
                var storedFrequencyType = habit.FrequencyType?.Trim() ?? string.Empty;
                var frequencyTypeIsLegacy = string.IsNullOrEmpty(storedFrequencyType)
                    || string.Equals(storedFrequencyType, legacyFrequency, StringComparison.OrdinalIgnoreCase)
                    || (legacyFrequency != "Quotidienne" && storedFrequencyType == "Quotidienne");
                if (!frequencyTypeIsLegacy)
                    continue;

                switch (legacyFrequency)
                {
                    case "Hebdomadaire":
                        habit.FrequencyType = "JoursChoisis";
                        if (string.IsNullOrWhiteSpace(habit.SelectedDays))
                            habit.SelectedDays = "L";
                        break;
                    case "Jours de semaine":
                        habit.FrequencyType = "JoursChoisis";
                        if (string.IsNullOrWhiteSpace(habit.SelectedDays))
                            habit.SelectedDays = "L,M,Me,J,V";
                        break;
                    case "Week-ends":
                        habit.FrequencyType = "JoursChoisis";
                        if (string.IsNullOrWhiteSpace(habit.SelectedDays))
                            habit.SelectedDays = "S,D";
                        break;
                    case "Quotidienne":
                        habit.FrequencyType = "TousLesJours";
                        break;
                    default:
                        continue;
                }

                connection.Update(habit);
            }
        }

        private static async Task SeedReferenceDataAsync(SQLiteAsyncConnection database)
        {
            foreach (var category in Category.DefaultCategories)
            {
                var existing = await database.Table<Category>()
                    .Where(item => item.Code == category.Code)
                    .FirstOrDefaultAsync();
                if (existing == null)
                    await database.InsertAsync(category);
            }

            var profiles = new[]
            {
                new UserProfileType { Code = "Dynamique", Title = "Le Dynamique", Description = "Journées remplies, objectifs ambitieux", SuggestedHabitsJson = "[\"Se déplacer à vélo ou à pied\",\"Remplir sa gourde\",\"Éteindre les veilles\"]" },
                new UserProfileType { Code = "Etudiant", Title = "L'Étudiant studieux", Description = "Rythme d'études, budget serré", SuggestedHabitsJson = "[\"Éviter l'impression\",\"Éteindre l'ordinateur la nuit\",\"Repas maison\"]" },
                new UserProfileType { Code = "Casanier", Title = "Le Casanier", Description = "Vie à domicile, calme", SuggestedHabitsJson = "[\"Éteindre les lumières inutiles\",\"Trier les déchets\",\"Repas végétarien\"]" },
                new UserProfileType { Code = "Sportif", Title = "Le Sportif", Description = "Activité régulière", SuggestedHabitsJson = "[\"Boire 2 L d'eau\",\"Sport 3 fois par semaine\",\"Repas équilibré\"]" },
                new UserProfileType { Code = "Noctambule", Title = "Le Noctambule", Description = "Couche-tard", SuggestedHabitsJson = "[\"Dormir avant minuit\",\"Débrancher les chargeurs\",\"Limiter les écrans le soir\"]" },
                new UserProfileType { Code = "Parent", Title = "Le Parent organisé", Description = "Emploi du temps familial", SuggestedHabitsJson = "[\"Repas maison\",\"Sacs réutilisables\",\"Marche ou covoiturage\"]" },
                new UserProfileType { Code = "Pro", Title = "Le Pro hyperactif", Description = "Travail intense, peu de temps", SuggestedHabitsJson = "[\"Transports partagés\",\"Pause de déconnexion\",\"Imprimer moins\"]" },
                new UserProfileType { Code = "Gourmand", Title = "Le Gourmand", Description = "Attention à l'assiette", SuggestedHabitsJson = "[\"Manger de saison\",\"Éviter le gaspillage\",\"Composter\"]" },
                new UserProfileType { Code = "Mobile", Title = "Le Mobile", Description = "Beaucoup de déplacements", SuggestedHabitsJson = "[\"Marche ou vélo\",\"Transports en commun\",\"Gourde\"]" },
                new UserProfileType { Code = "Zen", Title = "Le Zen", Description = "Recherche d'équilibre", SuggestedHabitsJson = "[\"Méditer 10 minutes\",\"Douche courte\",\"Marche dans la nature\"]" }
            };

            foreach (var profile in profiles)
            {
                var existing = await database.Table<UserProfileType>()
                    .Where(item => item.Code == profile.Code)
                    .FirstOrDefaultAsync();
                if (existing == null)
                    await database.InsertAsync(profile);
            }
        }

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
            return await database.Table<Habit>()
                .Where(habit => habit.UserId == userId && !habit.IsArchived)
                .ToListAsync();
        }

        public static Task<List<Habit>> GetHabitsAsync(int userId) =>
            GetHabitsByUserAsync(userId);

        public static async Task<List<Habit>> GetHabitsByGoalAsync(int goalId)
        {
            var database = await GetDatabaseAsync();
            return await database.Table<Habit>()
                .Where(habit => habit.GoalId == goalId && !habit.IsArchived)
                .ToListAsync();
        }

        public static async Task<Habit?> GetHabitByIdAsync(int habitId)
        {
            var database = await GetDatabaseAsync();
            return await database.FindAsync<Habit>(habitId);
        }

        public static async Task<int> ArchiveHabitAsync(int habitId)
        {
            var habit = await GetHabitByIdAsync(habitId);
            if (habit == null)
                return 0;

            habit.IsArchived = true;
            return await SaveHabitAsync(habit);
        }

        public static async Task<List<Habit>> GetArchivedHabitsAsync(int userId)
        {
            var database = await GetDatabaseAsync();
            return await database.Table<Habit>()
                .Where(habit => habit.UserId == userId && habit.IsArchived)
                .ToListAsync();
        }

        public static async Task<int> RestoreHabitAsync(int habitId)
        {
            var habit = await GetHabitByIdAsync(habitId);
            if (habit == null || !habit.IsArchived)
                return 0;

            habit.IsArchived = false;
            return await SaveHabitAsync(habit);
        }

        public static async Task<List<Goal>> GetGoalsAsync(int userId)
        {
            var database = await GetDatabaseAsync();
            return await database.Table<Goal>()
                .Where(goal => goal.UserId == userId && goal.Status != "Archivé")
                .ToListAsync();
        }

        public static async Task<Goal?> GetGoalByIdAsync(int goalId)
        {
            var database = await GetDatabaseAsync();
            return await database.FindAsync<Goal>(goalId);
        }

        public static async Task<int> SaveGoalAsync(Goal goal)
        {
            ArgumentNullException.ThrowIfNull(goal);
            var database = await GetDatabaseAsync();
            return goal.Id == 0
                ? await database.InsertAsync(goal)
                : await database.UpdateAsync(goal);
        }

        public static async Task<List<Category>> GetCategoriesAsync()
        {
            var database = await GetDatabaseAsync();
            return await database.Table<Category>().ToListAsync();
        }

        public static async Task<List<UserProfileType>> GetUserProfileTypesAsync()
        {
            var database = await GetDatabaseAsync();
            return await database.Table<UserProfileType>().ToListAsync();
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

        public static async Task SaveHabitCompletionAsync(
            int userId,
            int habitId,
            bool isCompleted,
            DateTime logDate,
            TimeSpan? actionTime = null,
            DateTime? actionDateTime = null)
        {
            var effectiveActionTime = actionDateTime ?? DateTime.Now;
            if (logDate.Date != effectiveActionTime.Date
                || effectiveActionTime.TimeOfDay > new TimeSpan(23, 59, 0))
                throw new InvalidOperationException("Une validation ne peut modifier que la journée en cours.");

            var database = await GetDatabaseAsync();
            var date = logDate.Date;

            await database.RunInTransactionAsync(connection =>
            {
                var user = connection.Find<User>(userId)
                    ?? throw new InvalidOperationException("L'utilisateur connecté n'existe plus.");
                var habit = connection.Find<Habit>(habitId);
                if (habit == null || habit.UserId != userId || habit.IsArchived)
                    throw new InvalidOperationException("Cette habitude n'appartient pas à l'utilisateur connecté.");

                var existingLog = connection.Table<HabitLog>()
                    .Where(log => log.HabitId == habitId && log.LogDate == date)
                    .FirstOrDefault();

                if (isCompleted)
                {
                    var wasLate = existingLog?.Status is "Manquée" or "Annulée";
                    var log = existingLog ?? new HabitLog
                    {
                        HabitId = habitId,
                        UserId = userId,
                        Date = date,
                        LogDate = date
                    };
                    log.UserId = userId;
                    log.Date = date;
                    log.LogDate = date;
                    log.Status = wasLate ? "Faite en retard" : "Faite";
                    log.ScheduledTime ??= habit.ScheduledTime;
                    log.ActionTime = actionTime ?? effectiveActionTime.TimeOfDay;
                    log.Co2Saved = habit.Co2ReductionPerAction;
                    log.IsCompleted = true;

                    if (existingLog == null)
                        connection.Insert(log);
                    else
                        connection.Update(log);
                }
                else if (!isCompleted && existingLog?.IsCompleted == true)
                {
                    existingLog.IsCompleted = false;
                    existingLog.Status = "À faire";
                    existingLog.Co2Saved = 0;
                    existingLog.ActionTime = null;
                    connection.Update(existingLog);
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
                "Habits.Title AS HabitTitle, Habits.IconName, Habits.HexColor, HabitLogs.Co2Saved AS Co2ReductionPerAction " +
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
            NormalizeHabitFrequency(habit);
            var conflictDate = await new EtatDuJourService()
                .FindFirstScheduleConflictDateAsync(habit, habit.Id == 0 ? null : habit.Id);
            if (conflictDate.HasValue)
                throw new InvalidOperationException(
                    $"Un conflit d'horaire existe le {conflictDate.Value:dd/MM/yyyy}; les habitudes doivent être espacées d'au moins 30 minutes.");

            if (habit.Id != 0)
            {
                return await database.UpdateAsync(habit);
            }
            else
            {
                return await database.InsertAsync(habit);
            }
        }

        private static void NormalizeHabitFrequency(Habit habit)
        {
            var frequencyType = habit.FrequencyType?.Trim() ?? string.Empty;
            var legacyFrequency = habit.Frequency?.Trim() ?? string.Empty;
            var frequencyTypeIsLegacy = string.IsNullOrEmpty(frequencyType)
                || string.Equals(frequencyType, legacyFrequency, StringComparison.OrdinalIgnoreCase)
                || (legacyFrequency != "Quotidienne" && frequencyType == "Quotidienne");
            if (!frequencyTypeIsLegacy)
                return;

            switch (legacyFrequency)
            {
                case "Hebdomadaire":
                    habit.FrequencyType = "JoursChoisis";
                    if (string.IsNullOrWhiteSpace(habit.SelectedDays))
                        habit.SelectedDays = GetDayToken(habit.CreatedAt.DayOfWeek);
                    break;
                case "Jours de semaine":
                    habit.FrequencyType = "JoursChoisis";
                    if (string.IsNullOrWhiteSpace(habit.SelectedDays))
                        habit.SelectedDays = "L,M,Me,J,V";
                    break;
                case "Week-ends":
                    habit.FrequencyType = "JoursChoisis";
                    if (string.IsNullOrWhiteSpace(habit.SelectedDays))
                        habit.SelectedDays = "S,D";
                    break;
                case "Quotidienne":
                    habit.FrequencyType = "TousLesJours";
                    break;
            }
        }

        private static string GetDayToken(DayOfWeek dayOfWeek) => dayOfWeek switch
        {
            DayOfWeek.Monday => "L",
            DayOfWeek.Tuesday => "M",
            DayOfWeek.Wednesday => "Me",
            DayOfWeek.Thursday => "J",
            DayOfWeek.Friday => "V",
            DayOfWeek.Saturday => "S",
            _ => "D"
        };

        public static async Task<int> DeleteHabitAsync(Habit habit)
        {
            ArgumentNullException.ThrowIfNull(habit);
            if (!habit.IsArchived)
                return 0;

            return await DeleteArchivedHabitAsync(habit.Id);
        }

        public static async Task<int> DeleteArchivedHabitAsync(int habitId)
        {
            var database = await GetDatabaseAsync();
            var deletedHabitCount = 0;
            await database.RunInTransactionAsync(connection =>
            {
                var habit = connection.Find<Habit>(habitId);
                if (habit == null || !habit.IsArchived)
                    return;

                connection.Table<HabitLog>()
                    .Where(log => log.HabitId == habitId)
                    .Delete();
                deletedHabitCount = connection.Delete(habit);
            });

            return deletedHabitCount;
        }

        public static async Task<int> ArchiveGoalAsync(int goalId)
        {
            var database = await GetDatabaseAsync();
            var archivedGoalCount = 0;
            await database.RunInTransactionAsync(connection =>
            {
                var goal = connection.Find<Goal>(goalId);
                if (goal == null)
                    return;

                goal.Status = "Archivé";
                archivedGoalCount = connection.Update(goal);
                var habits = connection.Table<Habit>()
                    .Where(habit => habit.GoalId == goalId && !habit.IsArchived)
                    .ToList();
                foreach (var habit in habits)
                {
                    habit.IsArchived = true;
                    connection.Update(habit);
                }
            });

            return archivedGoalCount;
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

        public static async Task<User?> GetUserByIdAsync(int userId)
        {
            var database = await GetDatabaseAsync();
            return await database.FindAsync<User>(userId);
        }

        public static async Task<List<HabitLog>> GetHabitLogsForHabitAsync(int habitId)
        {
            var database = await GetDatabaseAsync();
            return await database.Table<HabitLog>()
                .Where(log => log.HabitId == habitId)
                .ToListAsync();
        }

        public static async Task<HabitLog?> GetLogForDateAsync(int habitId, DateTime date)
        {
            var database = await GetDatabaseAsync();
            var targetDate = date.Date;
            return await database.Table<HabitLog>()
                .Where(log => log.HabitId == habitId && log.Date == targetDate)
                .FirstOrDefaultAsync();
        }

        public static async Task<int> SaveHabitLogAsync(HabitLog log)
        {
            ArgumentNullException.ThrowIfNull(log);
            var database = await GetDatabaseAsync();
            if (log.Date == default && log.LogDate != default)
                log.Date = log.LogDate.Date;
            if (log.LogDate == default && log.Date != default)
                log.LogDate = log.Date.Date;

            return log.Id == 0
                ? await database.InsertAsync(log)
                : await database.UpdateAsync(log);
        }

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
            var logsByHabit = logs.GroupBy(log => log.HabitId)
                .ToDictionary(group => group.Key, group => group.Select(log => log.LogDate.Date).Distinct().ToList());
            var bestStreak = 0;
            var today = DateTime.Today;

            foreach (var habit in habits)
            {
                var dates = logsByHabit.TryGetValue(habit.Id, out var completedDates)
                    ? completedDates
                    : new List<DateTime>();
                if (IsQuotaFrequency(habit))
                {
                    var quotaLogs = logs.Where(log => log.HabitId == habit.Id && log.IsCompleted).ToList();
                    var (currentQuotaStreak, bestQuotaStreak) = CalculateQuotaStreaks(habit, quotaLogs, today);
                    habit.CurrentStreak = currentQuotaStreak;
                    bestStreak = Math.Max(bestStreak, bestQuotaStreak);
                }
                else
                {
                    habit.CurrentStreak = CalculateCurrentStreak(habit, dates, today);
                    bestStreak = Math.Max(bestStreak, CalculateLongestStreak(habit, dates));
                }
            }

            var overallStreaks = EtatDuJourService.CalculateDailyStreaks(habits, logs, today);
            user.TotalCo2Saved = Math.Round(logs.Sum(log => log.Co2Saved), 2);
            user.CurrentStreak = overallStreaks.Current;
            user.BestStreak = Math.Max(user.BestStreak, Math.Max(bestStreak, overallStreaks.Best));
        }

        private static (int Current, int Best) CalculateQuotaStreaks(
            Habit habit,
            IReadOnlyCollection<HabitLog> logs,
            DateTime today)
        {
            var isWeekly = habit.FrequencyType is "QuotaSemaine" or "QuotaHebdomadaire";
            var periodLength = isWeekly ? 7 : DateTime.DaysInMonth(today.Year, today.Month);
            var startOfCurrentPeriod = isWeekly
                ? today.Date.AddDays(-(((int)today.DayOfWeek + 6) % 7))
                : new DateTime(today.Year, today.Month, 1);
            var firstStart = habit.StartDate.Date > habit.CreatedAt.Date
                ? habit.StartDate.Date
                : habit.CreatedAt.Date;
            var firstPeriodStart = isWeekly
                ? firstStart.AddDays(-(((int)firstStart.DayOfWeek + 6) % 7))
                : new DateTime(firstStart.Year, firstStart.Month, 1);
            var best = 0;
            var run = 0;
            var current = 0;

            for (var periodStart = firstPeriodStart; periodStart <= startOfCurrentPeriod; periodStart = isWeekly
                    ? periodStart.AddDays(7)
                    : periodStart.AddMonths(1))
            {
                var periodEnd = isWeekly ? periodStart.AddDays(6) : periodStart.AddMonths(1).AddDays(-1);
                var activeStart = periodStart > firstStart ? periodStart : firstStart;
                var activeEnd = habit.EndDate.HasValue && habit.EndDate.Value.Date < periodEnd
                    ? habit.EndDate.Value.Date
                    : periodEnd;
                var activeDays = activeStart > activeEnd ? 0 : (activeEnd - activeStart).Days + 1;
                if (activeDays == 0)
                    continue;

                var required = Math.Max(1, (int)Math.Ceiling(habit.QuotaCount * activeDays / (double)periodLength));
                var completed = logs.Count(log => log.Date.Date >= activeStart && log.Date.Date <= activeEnd);
                if (completed >= required)
                {
                    run++;
                    best = Math.Max(best, run);
                    if (periodStart == startOfCurrentPeriod)
                        current = run;
                }
                else if (periodStart != startOfCurrentPeriod || today.Date >= periodEnd)
                {
                    run = 0;
                    if (periodStart == startOfCurrentPeriod)
                        current = 0;
                }
                else
                {
                    current = run;
                }
            }

            return (current, best);
        }

        private static bool IsQuotaFrequency(Habit habit) =>
            habit.FrequencyType is "QuotaSemaine" or "QuotaMois" or "QuotaHebdomadaire" or "QuotaMensuel";

        private static int CalculateCurrentStreak(Habit habit, IEnumerable<DateTime> completedDates, DateTime today)
        {
            var dates = completedDates.Select(date => date.Date).ToHashSet();
            var streak = 0;
            var firstDate = habit.StartDate.Date > habit.CreatedAt.Date
                ? habit.StartDate.Date
                : habit.CreatedAt.Date;

            for (var currentDate = today.Date; currentDate >= firstDate; currentDate = currentDate.AddDays(-1))
            {
                if (!IsHabitScheduledOnDate(habit, currentDate))
                    continue;
                if (dates.Contains(currentDate))
                    streak++;
                else if (currentDate != today.Date)
                    break;
            }

            return streak;
        }

        private static int CalculateLongestStreak(Habit habit, IEnumerable<DateTime> completedDates)
        {
            var dates = completedDates.Select(date => date.Date).Distinct().Order().ToList();
            if (dates.Count == 0)
                return 0;

            var longestStreak = 0;
            var currentStreak = 0;
            var firstDate = Math.Max(habit.StartDate.Date.Ticks, habit.CreatedAt.Date.Ticks);
            var firstDateTime = new DateTime(firstDate);
            var completedDateSet = dates.ToHashSet();

            for (var date = firstDateTime; date <= dates[^1]; date = date.AddDays(1))
            {
                if (!IsHabitScheduledOnDate(habit, date))
                    continue;

                currentStreak = completedDateSet.Contains(date.Date) ? currentStreak + 1 : 0;
                longestStreak = Math.Max(longestStreak, currentStreak);
            }

            return longestStreak;
        }

        private static bool IsHabitScheduledOnDate(Habit habit, DateTime date)
        {
            if (date.Date < habit.StartDate.Date
                || date.Date < habit.CreatedAt.Date
                || (habit.EndDate.HasValue && date.Date > habit.EndDate.Value.Date))
            {
                return false;
            }

            var frequencyType = string.IsNullOrWhiteSpace(habit.FrequencyType)
                ? habit.Frequency
                : habit.FrequencyType;
            return frequencyType switch
            {
                "JoursChoisis" => habit.SelectedDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Contains(GetDayToken(date.DayOfWeek), StringComparer.OrdinalIgnoreCase),
                "Hebdomadaire" => date.DayOfWeek == habit.CreatedAt.DayOfWeek,
                "Jours de semaine" => date.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday,
                "Week-ends" => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                "QuotaSemaine" or "QuotaMois" or "QuotaHebdomadaire" or "QuotaMensuel" => false,
                _ => true
            };
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