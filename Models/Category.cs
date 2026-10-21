using SQLite;

namespace HabitDaily.Models
{
    [Table("Category")]
    public class Category
    {
        [PrimaryKey]
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Color { get; set; } = string.Empty;

        public string Icon { get; set; } = string.Empty;

        public double DefaultCo2 { get; set; }

        [Ignore]
        public string DisplayName => $"{Icon} {Name}";

        public static readonly List<Category> DefaultCategories = new()
        {
            new Category { Code = "Transport", Name = "Transport", Color = "#00E676", Icon = "🚲", DefaultCo2 = 0.5 },
            new Category { Code = "Energie", Name = "Énergie", Color = "#EEFF41", Icon = "⚡", DefaultCo2 = 0.3 },
            new Category { Code = "Dechets", Name = "Déchets", Color = "#C6FF00", Icon = "♻️", DefaultCo2 = 0.2 },
            new Category { Code = "Alimentation", Name = "Alimentation", Color = "#76FF03", Icon = "🥗", DefaultCo2 = 0.4 },
            new Category { Code = "Bien-etre", Name = "Bien-être", Color = "#B9F6CA", Icon = "🧘", DefaultCo2 = 0.0 },
            new Category { Code = "Autre", Name = "Autre", Color = "#9E9E9E", Icon = "🌱", DefaultCo2 = 0.0 }
        };
    }
}