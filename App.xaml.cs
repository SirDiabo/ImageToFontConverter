using System;
using System.Windows;

namespace ImageToFontConverter
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Set up global exception handling
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = args.ExceptionObject as Exception;
                MessageBox.Show($"An unexpected error occurred: {exception?.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
            
            DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"An unexpected error occurred: {args.Exception.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}