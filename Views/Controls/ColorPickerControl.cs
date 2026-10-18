namespace HabitDaily.Views.Controls;

public sealed class ColorPickerControl : ContentView
{
    private const int SaturationColumns = 48;
    private const int LightnessRows = 32;
    private readonly GraphicsView _colorField;
    private readonly GraphicsView _hueField;
    private double _saturation;
    private double _lightness = 0.5;

    public static readonly BindableProperty HueProperty = BindableProperty.Create(
        nameof(Hue),
        typeof(double),
        typeof(ColorPickerControl),
        0d,
        propertyChanged: OnHueChanged);

    public static readonly BindableProperty SelectedColorProperty = BindableProperty.Create(
        nameof(SelectedColor),
        typeof(Color),
        typeof(ColorPickerControl),
        Color.FromArgb("#00E676"),
        BindingMode.TwoWay,
        propertyChanged: OnSelectedColorChanged);

    public ColorPickerControl()
    {
        _colorField = new GraphicsView
        {
            Drawable = new SaturationLightnessDrawable(this),
            AutomationId = "HabitColorField",
            HeightRequest = 176,
            HorizontalOptions = LayoutOptions.Fill
        };
        _hueField = new GraphicsView
        {
            Drawable = new HueDrawable(this),
            AutomationId = "HabitHueSpectrum",
            HeightRequest = 24,
            HorizontalOptions = LayoutOptions.Fill
        };

        SemanticProperties.SetDescription(_colorField, "Choisir la saturation et la luminosité de la couleur");
        SemanticProperties.SetDescription(_hueField, "Choisir la teinte de la couleur");
        _colorField.StartInteraction += OnColorFieldInteraction;
        _colorField.DragInteraction += OnColorFieldInteraction;
        _hueField.StartInteraction += OnHueFieldInteraction;
        _hueField.DragInteraction += OnHueFieldInteraction;

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 10,
            Children =
            {
                _colorField,
                _hueField
            }
        };

        Grid.SetRow(_hueField, 1);
    }

    public double Hue
    {
        get => (double)GetValue(HueProperty);
        set => SetValue(HueProperty, Math.Clamp(value, 0, 1));
    }

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    private static void OnHueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var picker = (ColorPickerControl)bindable;
        picker.UpdateSelectedColor();
        picker._hueField.Invalidate();
        picker._colorField.Invalidate();
    }

    private static void OnSelectedColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var picker = (ColorPickerControl)bindable;
        picker.UpdateSelectionFromColor((Color)newValue);
        picker._colorField.Invalidate();
        picker._hueField.Invalidate();
    }

    private void OnColorFieldInteraction(object? sender, TouchEventArgs e)
    {
        if (_colorField.Width <= 0 || _colorField.Height <= 0 || e.Touches.Length == 0)
            return;

        _saturation = Math.Clamp(e.Touches[0].X / _colorField.Width, 0, 1);
        _lightness = 1 - Math.Clamp(e.Touches[0].Y / _colorField.Height, 0, 1);
        UpdateSelectedColor();
        _colorField.Invalidate();
    }

    private void OnHueFieldInteraction(object? sender, TouchEventArgs e)
    {
        if (_hueField.Width <= 0 || e.Touches.Length == 0)
            return;

        Hue = Math.Clamp(e.Touches[0].X / _hueField.Width, 0, 1);
    }

    private void UpdateSelectedColor()
    {
        var color = Color.FromHsla(Hue, _saturation, _lightness);
        if (SelectedColor != color)
            SelectedColor = color;
    }

    private void UpdateSelectionFromColor(Color color)
    {
        var red = color.Red;
        var green = color.Green;
        var blue = color.Blue;
        var maximum = Math.Max(red, Math.Max(green, blue));
        var minimum = Math.Min(red, Math.Min(green, blue));
        var chroma = maximum - minimum;

        _lightness = (maximum + minimum) / 2;
        _saturation = chroma == 0
            ? 0
            : chroma / (1 - Math.Abs(2 * _lightness - 1));

        if (chroma > 0)
        {
            var hue = maximum == red
                ? (green - blue) / chroma % 6
                : maximum == green
                    ? (blue - red) / chroma + 2
                    : (red - green) / chroma + 4;
            Hue = (hue / 6 + 1) % 1;
        }
    }

    private sealed class SaturationLightnessDrawable(ColorPickerControl picker) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var cellWidth = dirtyRect.Width / SaturationColumns;
            var cellHeight = dirtyRect.Height / LightnessRows;

            for (var row = 0; row < LightnessRows; row++)
            {
                var lightness = 1 - (row + 0.5) / LightnessRows;
                for (var column = 0; column < SaturationColumns; column++)
                {
                    var saturation = (column + 0.5) / SaturationColumns;
                    canvas.FillColor = Color.FromHsla(picker.Hue, saturation, lightness);
                    canvas.FillRectangle(
                        dirtyRect.X + column * cellWidth,
                        dirtyRect.Y + row * cellHeight,
                        cellWidth + 0.5f,
                        cellHeight + 0.5f);
                }
            }

            var markerX = dirtyRect.X + (float)picker._saturation * dirtyRect.Width;
            var markerY = dirtyRect.Y + (float)(1 - picker._lightness) * dirtyRect.Height;
            canvas.StrokeSize = 3;
            canvas.StrokeColor = Colors.Black;
            canvas.DrawCircle(markerX, markerY, 10);
            canvas.StrokeSize = 2;
            canvas.StrokeColor = Colors.White;
            canvas.DrawCircle(markerX, markerY, 8);
        }
    }

    private sealed class HueDrawable(ColorPickerControl picker) : IDrawable
    {
        private const int HueSteps = 60;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var stepWidth = dirtyRect.Width / HueSteps;
            for (var step = 0; step < HueSteps; step++)
            {
                canvas.FillColor = Color.FromHsla((double)step / HueSteps, 1, 0.5);
                canvas.FillRectangle(
                    dirtyRect.X + step * stepWidth,
                    dirtyRect.Y,
                    stepWidth + 0.5f,
                    dirtyRect.Height);
            }

            var markerX = dirtyRect.X + (float)picker.Hue * dirtyRect.Width;
            var markerY = dirtyRect.Center.Y;
            canvas.StrokeSize = 4;
            canvas.StrokeColor = Colors.Black;
            canvas.DrawCircle(markerX, markerY, 9);
            canvas.StrokeSize = 2;
            canvas.StrokeColor = Colors.White;
            canvas.DrawCircle(markerX, markerY, 7);
        }
    }
}
