using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WoWLauncher.Patcher;
using WoWLauncher.Updater;

namespace WoWLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // update controllers
        private PatchController m_Patcher;
        private UpdateController m_Updater;

        public MainWindow()
        {
            InitializeComponent();
            playBtn.IsEnabled = false; // Disable during checks

            m_Updater = new UpdateController(this);
            m_Patcher = new PatchController(this);

            // Check launcher update
            m_Updater.CheckForUpdates();
            // Update server address
            m_Updater.RetrieveRealmIP();

#if !DEBUG
            // While debugging, we don't need to check this...
            // But end-users should have the launcher next to this.
            if (!File.Exists("Wow.exe"))
            {
                if (MessageBox.Show(this, "Cannot locate Wow.exe, please make sure it is next to the launcher's executable.", "Error", MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    Application.Current.Shutdown();
            }

            // While debugging, we don't necessarily need this folder.
            if (!Directory.Exists("Data"))
                Directory.CreateDirectory("Data");
#endif
            // Begin checking for game updates
            m_Patcher.CheckPatch();
        }

        // Drag window around holding mouse button
        private void Window_MouseDown(object? sender, MouseButtonEventArgs? e)
        {
            if (e?.ChangedButton == MouseButton.Left)
                DragMove();
        }

        // Quit application
        private void Close_Click(object? sender, RoutedEventArgs? e)
        {
            // Check if patch in progress and ask to continue
            if (!m_Patcher.IsPatching || (m_Patcher.IsPatching && MessageBox.Show(this, "Patch in progress. Are you sure you want to quit?", "Patching", MessageBoxButton.YesNo) == MessageBoxResult.Yes))
                Application.Current.Shutdown();
        }

        // Allow URL clicking inside the rich text block
        private void Hyperlink_RequestNavigate(object? sender, System.Windows.Navigation.RequestNavigateEventArgs? e)
        {
            Process.Start(new ProcessStartInfo(e?.Uri.ToString() ?? "")
            {
                UseShellExecute = true
            });
        }

        // Launch actual game when play button becomes active
        private void playBtn_Click(object? sender, RoutedEventArgs? e)
        {
            // Check if the program exists, first
            if (File.Exists("Wow.exe") && !m_Patcher.IsPatching)
            {
                Process.Start(new ProcessStartInfo("Wow.exe")
                {
                    UseShellExecute = true
                });
                Application.Current.Shutdown(); // close launcher, maybe make this an option?
            }
        }
    }
}
