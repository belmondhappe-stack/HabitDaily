# HabitDaily

HabitDaily est une application mobile .NET MAUI de suivi d'habitudes écologiques.

## Technologies

- .NET MAUI
- SQLite (`sqlite-net-pcl`)

## Création d'habitudes

L'écran de création propose un catalogue d'icônes classées par catégorie et recherchables, ainsi qu'un sélecteur de couleurs HSL avec aperçu en direct.

## Calendrier

L'historique présente les validations enregistrées dans SQLite par mois et par date, avec le nombre d'écogestes et le CO₂ économisé pour la journée sélectionnée.

## Structure

- `Models/` : modèles de données
- `Services/` : services, notamment l'accès à la base de données
- `ViewModels/` : logique de présentation
- `Views/` : interfaces de l'application
- `Platforms/` : configuration spécifique à chaque plateforme
