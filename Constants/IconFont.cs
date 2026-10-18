namespace HabitDaily.Constants;

public static class IconFont
{
    public const string Home = "\ue88a";
    public const string Calendar = "\ue916";
    public const string Stats = "\ue6e1";
    public const string Profile = "\ue7fd";
    public const string Leaf = "\uea35";
    public const string Plus = "\ue145";
    public const string Check = "\ue5ca";
    public const string Unchecked = "\ue836";
    public const string Fire = "\ue80e";
    public const string Email = "\ue0be";
    public const string Mail = "\ue158";
    public const string Lock = "\ue897";
    public const string Person = "\ue7fd";
    public const string Login = "\uea77";
    public const string PersonAdd = "\ue7fe";
    public const string LockReset = "\ueade";
    public const string ArrowBack = "\ue5c4";
    public const string Book = "\ue865";
    public const string Alarm = "\ue855";
    public const string WaterDrop = "\ue798";
    public const string Power = "\ue8ac";
    public const string Restaurant = "\ue56c";
    public const string LocalCafe = "\ue541";
    public const string LunchDining = "\uea61";
    public const string BakeryDining = "\uea53";
    public const string Egg = "\ueacc";
    public const string LocalGroceryStore = "\ue547";
    public const string Computer = "\ue30a";
    public const string Assignment = "\ue85d";
    public const string Calculate = "\uea5f";
    public const string Engineering = "\uea3d";
    public const string Brush = "\ue3ae";
    public const string CleaningServices = "\uf0ff";
    public const string LocalLaundryService = "\ue54a";
    public const string Kitchen = "\ueb47";
    public const string Bed = "\uefdf";
    public const string DirectionsWalk = "\ue536";
    public const string DirectionsBike = "\ue52f";
    public const string DirectionsBus = "\ue530";
    public const string DirectionsCar = "\ue531";
    public const string Train = "\ue533";
    public const string DirectionsRun = "\ue566";
    public const string EnergySavingsLeaf = "\uec1a";
    public const string ElectricBolt = "\uec1c";
    public const string ElectricCar = "\ueb1c";
    public const string SolarPower = "\uf1a8";
    public const string Recycling = "\ue760";
    public const string Compost = "\ue761";
    public const string Park = "\uea63";
    public const string Forest = "\uea99";
    public const string LocalFlorist = "\ue545";
    public const string Pets = "\ue91d";
    public const string Water = "\ue544";
    public const string FitnessCenter = "\ueb43";
    public const string SelfImprovement = "\uea78";
    public const string Medication = "\uf033";
    public const string Favorite = "\ue87d";
    public const string Spa = "\uea99";
    public const string Shower = "\uf061";
    public const string Lightbulb = "\ue0f0";
    public const string Backpack = "\uf19c";
    public const string ShoppingCart = "\ue8cc";
    public const string Savings = "\ue2eb";
    public const string LocalDrink = "\ue544";

    public static string ForHabitTitle(string? title, string? iconName = null)
    {
        switch (iconName)
        {
            case "leaf":
            case "eco":
                return Leaf;
            case "home":
                return Home;
            case "book":
            case "menu_book":
                return Book;
            case "alarm":
                return Alarm;
            case "water":
                return WaterDrop;
            case "power":
                return Power;
            case "restaurant":
            case "restaurant_menu":
                return Restaurant;
            case "local_cafe":
                return LocalCafe;
            case "lunch_dining":
                return LunchDining;
            case "bakery_dining":
                return BakeryDining;
            case "egg":
                return Egg;
            case "local_grocery_store":
                return LocalGroceryStore;
            case "computer":
                return Computer;
            case "assignment":
                return Assignment;
            case "calculate":
                return Calculate;
            case "engineering":
                return Engineering;
            case "brush":
                return Brush;
            case "cleaning_services":
                return CleaningServices;
            case "local_laundry_service":
                return LocalLaundryService;
            case "kitchen":
                return Kitchen;
            case "bed":
                return Bed;
            case "directions_walk":
                return DirectionsWalk;
            case "directions_bike":
                return DirectionsBike;
            case "directions_bus":
                return DirectionsBus;
            case "directions_car":
                return DirectionsCar;
            case "train":
                return Train;
            case "directions_run":
                return DirectionsRun;
            case "energy_savings_leaf":
                return EnergySavingsLeaf;
            case "electric_bolt":
                return ElectricBolt;
            case "electric_car":
                return ElectricCar;
            case "solar_power":
                return SolarPower;
            case "recycling":
                return Recycling;
            case "compost":
                return Compost;
            case "park":
                return Park;
            case "forest":
                return Forest;
            case "local_florist":
                return LocalFlorist;
            case "pets":
                return Pets;
            case "water_drop":
            case "local_drink":
                return Water;
            case "fitness_center":
                return FitnessCenter;
            case "self_improvement":
                return SelfImprovement;
            case "medication":
                return Medication;
            case "favorite":
                return Favorite;
            case "spa":
                return Spa;
            case "shower":
                return Shower;
            case "lightbulb":
                return Lightbulb;
            case "backpack":
                return Backpack;
            case "shopping_cart":
                return ShoppingCart;
            case "savings":
                return Savings;
        }

        var normalizedTitle = title?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalizedTitle.Contains("devoir", StringComparison.Ordinal)
            || normalizedTitle.Contains("cahier", StringComparison.Ordinal)
            || normalizedTitle.Contains("étude", StringComparison.Ordinal)
            || normalizedTitle.Contains("etude", StringComparison.Ordinal))
        {
            return Book;
        }

        if (normalizedTitle.Contains("lever", StringComparison.Ordinal)
            || normalizedTitle.Contains("réveil", StringComparison.Ordinal)
            || normalizedTitle.Contains("reveil", StringComparison.Ordinal)
            || normalizedTitle.Contains("matin", StringComparison.Ordinal))
        {
            return Alarm;
        }

        if (normalizedTitle.Contains("arroser", StringComparison.Ordinal)
            || normalizedTitle.Contains("plante", StringComparison.Ordinal)
            || normalizedTitle.Contains("jardin", StringComparison.Ordinal))
        {
            return WaterDrop;
        }

        if (normalizedTitle.Contains("éteindre", StringComparison.Ordinal)
            || normalizedTitle.Contains("eteindre", StringComparison.Ordinal)
            || normalizedTitle.Contains("veille", StringComparison.Ordinal)
            || normalizedTitle.Contains("énergie", StringComparison.Ordinal)
            || normalizedTitle.Contains("energie", StringComparison.Ordinal)
            || normalizedTitle.Contains("électricité", StringComparison.Ordinal)
            || normalizedTitle.Contains("electricite", StringComparison.Ordinal))
        {
            return Power;
        }

        return Leaf;
    }
}