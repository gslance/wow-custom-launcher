using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows;

namespace WoWLauncher.Updater
{
    /// <summary>
    /// Responsible for updating the launcher itself
    /// </summary>
    internal class UpdateController
    {
        // Reference parent window
        private MainWindow m_WndRef;

        // Data
        private string m_RealmAddress;
        private bool m_NeedsUpdate;

        // Textfile containing version number of latest launcher (e.g 1.2) 
        private string m_UpdateVersionUri = "https://example.com/Patch/update.txt";
        private string m_ServerAddressUri = "https://example.com/Patch/realm.txt";
        /*
         * HOW TO ORGANIZE YOUR PATCH SERVER
         * 

            patch-folder (e.g www.example.com/Patch/) 
                |
                |- Patch
                    |--- plist.txt       <== your list of patch files (each filename on seperate line)
                    |--- realm.txt       <== contains the IP address of your game server
                    |--- update.txt      <== version number of latest launcher
                    |--- client.zip      <== latest launcher files as zip

                    |--- Patch-4.MPQ     <== list of patch files, can be any name (for WoW must start with "Patch-"
                    |--- Patch-C.MPQ    
                    |--- ... etc

         *
         *
         */

        // Accessor
        public bool NeedsUpdate { get { return m_NeedsUpdate; } }
        public string RealmAddress { get { return m_RealmAddress; } }

        public UpdateController(MainWindow _wndRef)
        {
            m_WndRef = _wndRef;
            m_NeedsUpdate = false;
        }

        /// <summary>
        /// Begin checking for launcher updates.
        /// </summary>
        public void CheckForUpdates()
        {
            // Check if update file exists
            string url = m_UpdateVersionUri;
            WebRequest request = WebRequest.Create(url);
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            }
            catch
            {
                // Reset and continue business as usual
                m_NeedsUpdate = false;
                return;
            }

            // Begin downloading update info
            using (WebClient wc = new())
            {
                wc.DownloadStringAsync(new Uri(m_UpdateVersionUri), "Cache/L/version.txt");
                wc.DownloadStringCompleted += update_DoneRetrieveAsync;
            }
        }

        /// <summary>
        /// Retrieve latest game server address.
        /// </summary>
        public void RetrieveRealmIP()
        {
            // Set default and prepare folders
            m_RealmAddress = "127.0.0.1";
            if (!Directory.Exists("Data/enUS"))
                Directory.CreateDirectory("Data/enUS");

            string url = m_ServerAddressUri;
            WebRequest request = WebRequest.Create(url);
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            }
            catch
            {
                // No server file online, check if local file exists
                if (File.Exists("Data/enUS/realmlist.wtf"))
                {
                    // Read existing file and save it for this session
                    string _realmd = File.ReadAllText("Data/enUS/realmlist.wtf");
                    if (_realmd.Length > 0)
                    {
                        string[] _realmParts = _realmd.Split(' ');
                        m_RealmAddress = _realmParts[2];
                    }
                }
                else // create new dummy file if nothing else exists. Silly.
                    File.WriteAllText("Data/enUS/realmlist.wtf", $"set realmlist {m_RealmAddress}");

                return;
            }

            // Update texts
            m_WndRef.progressInfo.Visibility = Visibility.Visible;
            m_WndRef.progressInfo.Content = "Updating server IP...";

            // Prepare folders
            if (!Directory.Exists("Cache/L"))
                Directory.CreateDirectory("Cache/L");
            if (File.Exists("Cache/L/realm.txt"))
                File.Delete("Cache/L/realm.txt");

            // Begin downloading server address update
            using (WebClient wc = new())
            {
                wc.DownloadStringAsync(new Uri(m_ServerAddressUri), "Cache/L/realm.txt");
                wc.DownloadStringCompleted += realm_DonePatchListAsync;
            }
            return;
        }

        /// <summary>
        /// Completed server address update, apply.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void realm_DonePatchListAsync(object sender, DownloadStringCompletedEventArgs e)
        {
            File.WriteAllText("Data/enUS/realmlist.wtf", $"set realmlist {e.Result}");
            if (File.Exists("Cache/L/realm.txt"))
                File.Delete("Cache/L/realm.txt");
        }

        /// <summary>
        /// Completed update download, check if it's newer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void update_DoneRetrieveAsync(object sender, DownloadStringCompletedEventArgs e)
        {
            // Store complete versions
            string _onlineVersion = e.Result;
            string _thisVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0";
            // Split into important bits
            string[] _onlineVersionParts = _onlineVersion.Split('.');
            string[] _localVersionParts = _thisVersion.Split('.');

            // This is a little silly, but it gets the job done
            m_NeedsUpdate = false;
            if (int.TryParse(_onlineVersionParts[0], out int _majorVersionOnline))
            {
                if (int.TryParse(_localVersionParts[0], out int _majorVersionLocal))
                {
                    // Major update, definitely update
                    if (_majorVersionOnline > _majorVersionLocal)
                        m_NeedsUpdate = true;

                    // Same major version? Check for minor update
                    if (_majorVersionOnline == _majorVersionLocal)
                    {
                        if (int.TryParse(_onlineVersionParts[1], out int _minorVersionOnline))
                        {
                            if (int.TryParse(_localVersionParts[1], out int _minorVersionLocal))
                            {
                                // Minor update, update anyway
                                if (_minorVersionOnline > _minorVersionLocal)
                                    m_NeedsUpdate = true;
                            }
                        }
                    }
                }
            }

            // Actual update available, 
            if (m_NeedsUpdate)
            {
                // Ask for installation
                if (MessageBox.Show(m_WndRef, "The launcher has an update. Do you want to update now?", "Update available!", MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.Yes, MessageBoxOptions.DefaultDesktopOnly) == MessageBoxResult.Yes)
                {
                    // Switch to updater software
                    if (File.Exists("Updater.exe"))
                    {
                        Application.Current.Shutdown();
                        Process.Start(new ProcessStartInfo("Updater.exe")
                        {
                            UseShellExecute = true
                        });
                    }
                    else // uh-oh. Oh well.
                        MessageBox.Show(m_WndRef, "The launcher has an update but the updater is missing.", "Updater missing!", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
