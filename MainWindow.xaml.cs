using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Input;

namespace ImageToFontConverter
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isConverting = false;

        public MainWindow()
        {
            InitializeComponent();
            CheckFontForgeInstallation();
        }

        private void CheckFontForgeInstallation()
        {
            string fontForgeExe = FontConverter.FindFontForgeExecutable();

            if (string.IsNullOrEmpty(fontForgeExe))
            {
                StatusTextBlock.Text = "Warning: FontForge not detected. Please install FontForge to use this application.";
            }
            else
            {
                StatusTextBlock.Text = $"FontForge found at: {fontForgeExe}";
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select folder containing PNG images";
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FolderPathTextBox.Text = dialog.SelectedPath;
                    UpdateGlyphsFoundDisplay(dialog.SelectedPath);
                }
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPath = FolderPathTextBox.Text;
            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                try
                {
                    Process.Start("explorer.exe", folderPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                StatusTextBlock.Text = "Please select a valid folder first";
            }
        }

        private void SimplificationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SimplificationLabel != null)
            {
                SimplificationLabel.Content = $"Simplification: {SimplificationSlider.Value * 100:F4}";
            }
        }

        private async void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FolderPathTextBox.Text))
            {
                MessageBox.Show("Please select a folder first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(FolderPathTextBox.Text))
            {
                MessageBox.Show("Selected folder does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await StartConversion();
        }

        private async Task StartConversion()
        {
            if (_isConverting) return;

            _isConverting = true;
            _cancellationTokenSource = new CancellationTokenSource();

            ConvertButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            ProgressBar.Value = 0;

            try
            {
                var converter = new FontConverter();
                await converter.ConvertToFontAsync(
                    FolderPathTextBox.Text,
                    FontNameTextBox.Text,
                    SimplificationSlider.Value,
                    UpdateProgress,
                    _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                UpdateProgress(0, "Conversion cancelled.");
            }
            catch (Exception ex)
            {
                UpdateProgress(0, $"Error: {ex.Message}");
                MessageBox.Show($"Conversion failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isConverting = false;
                ConvertButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            CancelButton.IsEnabled = false;
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isConverting)
            {
                var result = MessageBox.Show("Conversion is in progress. Are you sure you want to quit?",
                    "Confirm Exit", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                    return;

                _cancellationTokenSource?.Cancel();
            }

            Close();
        }

        private void UpdateProgress(double value, string message)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = value;
                StatusTextBlock.Text = $"{value:F2}% - {message}";
            });
        }

        private void UpdateGlyphsFoundDisplay(string folderPath)
        {
            Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath) || GlyphsFoundTextBlock == null)
                {
                    if (GlyphsFoundTextBlock != null)
                    {
                        GlyphsFoundTextBlock.Visibility = Visibility.Collapsed;
                    }
                    return;
                }

                try
                {
                    var pngFiles = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);
                    var baseNames = new HashSet<string>(pngFiles.Select(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);

                    int expected = FontConverter.ExpectedGlyphs.Length;
                    int found = FontConverter.ExpectedGlyphs.Count(n => baseNames.Contains(n, StringComparer.OrdinalIgnoreCase));

                    GlyphsFoundTextBlock.Text = $"{found}/{expected} glyphs found in target folder";
                    GlyphsFoundTextBlock.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    GlyphsFoundTextBlock.Visibility = Visibility.Collapsed;
                    StatusTextBlock.Text = $"Error reading folder: {ex.Message}";
                }
            });
        }

        private void GlyphsFoundTextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var folder = FolderPathTextBox.Text;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                StatusTextBlock.Text = "Please select a valid folder first";
                return;
            }

            var wnd = new GlyphsWindow(folder);
            wnd.Owner = this;
            wnd.ShowDialog();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isConverting)
            {
                var result = MessageBox.Show("Conversion is in progress. Are you sure you want to close?",
                    "Confirm Close", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                _cancellationTokenSource?.Cancel();
            }

            base.OnClosing(e);
        }
    }
}