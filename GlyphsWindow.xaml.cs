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
            var glyphUnicodeMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                {"space", 32}, {"exclamation", 33}, {"questionmark", 63}, {"period", 46}, {"comma", 44},
                {"colon", 58}, {"semicolon", 59}, {"hyphen", 45}, {"endash", 8211}, {"emdash", 8212},
                {"ellipsis", 8230}, {"degree", 176}, {"bullet", 8226}, {"middot", 183},
                {"leftparenthesis", 40}, {"rightparenthesis", 41}, {"leftbracket", 91},
                {"rightbracket", 93}, {"leftbrace", 123}, {"rightbrace", 125},
                {"angleleft", 171}, {"angleright", 187}, {"singlequote", 39}, {"doublequote", 34}, {"backtick", 96},
                {"plus", 43}, {"equal", 61}, {"caret", 94}, {"percent", 37}, {"asterisk", 42},
                {"divide", 247}, {"multiply", 215}, {"plusminus", 177}, {"lessthan", 60}, {"greaterthan", 62},
                {"at", 64}, {"hash", 35}, {"ampersand", 38}, {"underscore", 95},
                {"tilde", 126}, {"backslash", 92}, {"forwardslash", 47}, {"verticalbar", 124},
                {"copyright", 169}, {"trademark", 8482}, {"registered", 174},
                {"dollar", 36}, {"euro", 8364}, {"pound", 163}, {"yen", 165}, {"cent", 162},
            };
            var accentedUnicodeMap = new Dictionary<string,  int>(StringComparer.OrdinalIgnoreCase)
            {
                {"lower_e_acute", 233}, {"lower_e_grave", 232}, {"lower_e_circ", 234}, {"lower_e_uml", 235},
                {"lower_a_acute", 225}, {"lower_a_grave", 224}, {"lower_a_circ", 226}, {"lower_a_uml", 228}, {"lower_a_ring", 229},
                {"lower_o_acute", 243}, {"lower_o_circ", 244}, {"lower_o_uml", 246},
                {"lower_u_acute", 250}, {"lower_u_grave", 249}, {"lower_u_uml", 252},
                {"lower_i_acute", 237}, {"lower_i_circ", 238},
                {"lower_n_tilde", 241}, {"lower_c_cedil", 231}, {"lower_ss", 223}, {"lower_ae", 230},
                {"upper_A_uml", 196}, {"upper_O_uml", 214}, {"upper_U_uml", 220},
                {"upper_A_grave", 192}, {"upper_A_ring", 197}, {"upper_E_acute", 201},
                {"upper_N_tilde", 209}, {"upper_C_cedil", 199}, {"upper_AE", 198},
            };

            var punctuationSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "space", "exclamation", "questionmark", "period", "comma", "colon", "semicolon",
                  "hyphen", "endash", "emdash", "ellipsis", "degree", "bullet", "middot" };
            var bracketsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "leftparenthesis", "rightparenthesis", "leftbracket", "rightbracket",
                  "leftbrace", "rightbrace", "angleleft", "angleright", "singlequote", "doublequote", "backtick" };
            var mathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "plus", "equal", "caret", "percent", "asterisk", "divide", "multiply", "plusminus", "lessthan", "greaterthan" };
            var currencySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "dollar", "euro", "pound", "yen", "cent" };

            var expected = FontConverter.ExpectedGlyphs;
            var pngSet = new HashSet<string>(Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);

            foreach (var name in expected)
            {
                var entry = new GlyphEntry
                {
                    ExpectedName = name,
                    ExpectedFileDisplay = $"{name}.png",
                    HasImage = pngSet.Contains(name)
                };
                entry.Image = LoadImageIfExists(folderPath, name);
                entry.StatusText = entry.HasImage ? "Found" : "Missing";
                entry.ForegroundBrush = entry.HasImage
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D5DBE2"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));

                bool isAccented = accentedUnicodeMap.ContainsKey(name);
                bool isUpperBase = name.StartsWith("upper_", StringComparison.OrdinalIgnoreCase) && name.Length == 7;
                bool isLowerBase = name.StartsWith("lower_", StringComparison.OrdinalIgnoreCase) && name.Length == 7;

                if (isUpperBase)
                {
                    entry.Category = "Uppercase";
                    entry.Description = $"Uppercase letter '{name[6]}'";
                    entry.DisplayChar = name[6].ToString().ToUpper();
                }
                else if (isLowerBase)
                {
                    entry.Category = "Lowercase";
                    entry.Description = $"Lowercase letter '{name[6]}'";
                    entry.DisplayChar = name[6].ToString().ToLower();
                }
                else if (name.All(char.IsDigit))
                {
                    entry.Category = "Numbers";
                    entry.Description = $"Digit '{name}'";
                    entry.DisplayChar = name;
                }
                else if (isAccented)
                {
                    entry.Category = "Accented";
                    entry.Description = $"Accented character '{name}'";
                    try { entry.DisplayChar = char.ConvertFromUtf32(accentedUnicodeMap[name]); }
                    catch { entry.DisplayChar = name; }
                }
                else
                {
                    string cat = punctuationSet.Contains(name) ? "Punctuation"
                               : bracketsSet.Contains(name) ? "Brackets & Quotes"
                               : mathSet.Contains(name) ? "Math"
                               : currencySet.Contains(name) ? "Currency"
                                                               : "Symbols";
                    entry.Category = cat;
                    entry.Description = $"{cat} '{name}'";
                    if (glyphUnicodeMap.TryGetValue(name, out var code))
                    {
                        try { entry.DisplayChar = char.ConvertFromUtf32(code); }
                        catch { entry.DisplayChar = name; }
                    }
                    else entry.DisplayChar = name;
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