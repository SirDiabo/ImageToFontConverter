using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageToFontConverter
{
    public partial class GlyphsWindow : Window
    {
        public ObservableCollection<GlyphEntry> Glyphs { get; } = new ObservableCollection<GlyphEntry>();

        public GlyphsWindow(string folderPath)
        {
            InitializeComponent();
            DataContext = this;
            BuildGlyphList(folderPath);
        }

        private void BuildGlyphList(string folderPath)
        {
            var symbolMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                {"space", 32}, {"exclamation", 33}, {"questionmark", 63}, {"period", 46}, {"comma", 44},
                {"colon", 58}, {"semicolon", 59}, {"hyphen", 45}, {"plus", 43},
                {"equal", 61}, {"at", 64}, {"hash", 35}, {"dollar", 36},
                {"percent", 37}, {"caret", 94}, {"ampersand", 38}, {"asterisk", 42},
                {"leftparenthesis", 40}, {"rightparenthesis", 41}, {"underscore", 95},
                {"backtick", 96}, {"tilde", 126}, {"leftbracket", 91},
                {"rightbracket", 93}, {"leftbrace", 123}, {"rightbrace", 125},
                {"backslash", 92}, {"forwardslash", 47}, {"verticalbar", 124},
                {"lessthan", 60}, {"greaterthan", 62}, {"singlequote", 39}, {"doublequote", 34}
            };

            var expected = FontConverter.ExpectedGlyphs;
            var pngSet = new HashSet<string>(Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);

            foreach (var name in expected)
            {
                var entry = new GlyphEntry();
                entry.ExpectedName = name;
                entry.ExpectedFileDisplay = $"{name}.png";
                entry.HasImage = pngSet.Contains(name);
                entry.Image = LoadImageIfExists(folderPath, name);
                entry.StatusText = entry.HasImage ? "Found" : "Missing";
                entry.ForegroundBrush = entry.HasImage ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D5DBE2")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));

                if (name.StartsWith("upper_", StringComparison.OrdinalIgnoreCase))
                {
                    entry.Category = "Uppercase";
                    entry.Description = $"Uppercase letter '{name.Substring(6)}'";
                    entry.DisplayChar = name.Substring(6);
                }
                else if (name.StartsWith("lower_", StringComparison.OrdinalIgnoreCase))
                {
                    entry.Category = "Lowercase";
                    entry.Description = $"Lowercase letter '{name.Substring(6)}'";
                    entry.DisplayChar = name.Substring(6);
                }
                else if (name.All(char.IsDigit))
                {
                    entry.Category = "Numbers";
                    entry.Description = $"Digit '{name}'";
                    entry.DisplayChar = name;
                }
                else
                {
                    entry.Category = "Symbols";
                    entry.Description = $"Symbol '{name}'";
                    if (symbolMap.TryGetValue(name, out var code))
                    {
                        try
                        {
                            entry.DisplayChar = char.ConvertFromUtf32(code);
                        }
                        catch
                        {
                            entry.DisplayChar = name;
                        }
                    }
                    else
                    {
                        entry.DisplayChar = name;
                    }
                }

                Glyphs.Add(entry);
            }
        }

        private ImageSource LoadImageIfExists(string folderPath, string baseName)
        {
            try
            {
                var path = Path.Combine(folderPath, baseName + ".png");
                if (!File.Exists(path)) return null;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class GlyphEntry
    {
        public string Category { get; set; }
        public string ExpectedName { get; set; }
        public string ExpectedFileDisplay { get; set; }
        public string Description { get; set; }
        public bool HasImage { get; set; }
        public ImageSource Image { get; set; }
        public string StatusText { get; set; }
        public string DisplayChar { get; set; }
        public SolidColorBrush ForegroundBrush { get; set; }
    }

    public class InverseBooleanToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b) return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}