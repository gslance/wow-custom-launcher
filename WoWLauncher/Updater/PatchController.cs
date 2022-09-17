using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Windows;

namespace WoWLauncher.Patcher
{
    /// <summary>
    /// Responsible for downloading new patches
    /// </summary>
    internal class PatchController
    {
        // Reference parent window
        private MainWindow m_WndRef;

        // Data
        private int m_PatchIndex;
        private bool m_Patching;
        private List<PatchData> m_Patches;
        private readonly Stopwatch m_DownloadStopWatch;

        // Textfile containing patches (seperated on each line, md5 checksum next to it, e.g: Patch-L.mpq 6fd76dec2bbca6b58c7dce68b497e2bf)
        private string m_PatchListUri = "https://example.com/Patch/plist.txt";
        // Folder containing the individual patches, as listed in the patch list file
        private string m_PatchUri = "https://example.com/Patch/";

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
                    |--- Patch-C.MPQ         and filenames must not contain spaces
                    |--- ... etc
         
         *
         *
         */

        // Accessor
        public bool IsPatching { get { return m_Patching; } }

        public PatchController(MainWindow _wndRef)
        {
            m_WndRef = _wndRef;
            m_DownloadStopWatch = new Stopwatch();
            m_Patches = new List<PatchData>();
            m_PatchIndex = -1;
        }

        /// <summary>
        /// Begins checking server for patch files.
        /// </summary>
        /// <param name="_init">Is this the beginning of the check?</param>
        public void CheckPatch(bool _init = true)
        {
            if (_init)
            {
                // Reset and hide the progress info
                m_WndRef.progressInfo.IsEnabled = false;
                m_WndRef.progressBar.Value = 0;

                // Check if patch list exists
                WebRequest request = WebRequest.Create(m_PatchListUri);
                try
                {
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                }
                catch
                {
                    // Reset various controls and stop
                    m_WndRef.progressBar.Value = 100;
                    m_WndRef.playBtn.IsEnabled = true;
                    m_WndRef.progressInfo.Visibility = Visibility.Visible;
                    m_WndRef.progressInfo.Content = "Unable to download patch list!";
                    m_DownloadStopWatch.Reset();
                    return;
                }

                // Update texts
                m_WndRef.progressInfo.Visibility = Visibility.Visible;
                m_WndRef.progressInfo.Content = "Getting patch list...";

                // Prepare folders
                if (!Directory.Exists("Cache/L"))
                    Directory.CreateDirectory("Cache/L");
                if (File.Exists("Cache/L/plist.txt"))
                    File.Delete("Cache/L/plist.txt");

                // Begin downloading patch list
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFileAsync(
                        new Uri(m_PatchListUri),
                        "Cache/L/plist.txt"
                    );
                    wc.DownloadFileCompleted += patch_DonePatchListAsync;
                }
                return;
            }

            // Check if file was placed correctly
            if (File.Exists("Cache/L/plist.txt"))
            {
                // Check if there's any patches available
                m_Patches = PreparePatchList(File.ReadLines("Cache/L/plist.txt"));
                if (m_Patches.Count > 0)
                {
                    // Prepare game data folder
                    if (!Directory.Exists("Data"))
                        Directory.CreateDirectory("Data");

                    // Check for incomplete data
                    if (File.Exists("Cache/L/patching"))
                    {
                        string _incomplete = File.ReadAllText("Cache/L/patching");

                        // Remove incomplete patch files so we can download again
                        if (File.Exists($"Data/{_incomplete}"))
                            File.Delete($"Data/{_incomplete}");
                    }

                    // Begin the patch, start with first line
                    m_PatchIndex = 0;
                    m_WndRef.progressInfo.Content = "0% (Patch ?/?, downloaded 0/0 MB at 0 Mb/s)";
                    // Create recovery flag
                    File.WriteAllText("Cache/L/patching", m_Patches[m_PatchIndex].Filename);
                    // Begin patching
                    m_Patching = true;
                    DownloadPatch(m_PatchIndex);
                }
                else
                {
                    // Finish up and return control
                    FinishPatch();
                }
            }
        }

        /// <summary>
        /// Create patch list
        /// </summary>
        /// <param name="_list">Raw patch list</param>
        /// <returns>Organized patch data structre</returns>
        private List<PatchData> PreparePatchList(IEnumerable<string> _list)
        {
            m_Patches = new List<PatchData>();
            foreach (string _patch in _list)
            {
                string[] _data = _patch.Split(' ');
                m_Patches.Add(new PatchData()
                {
                    Filename = _data[0],
                    Checksum = _data[1]
                });
            }
            return m_Patches;
        }

        /// <summary>
        /// The patch was downloaded, check remaining patches.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void patch_DonePatchListAsync(object? sender, AsyncCompletedEventArgs e)
        {
            CheckPatch(false);
        }

        /// <summary>
        /// Download patch at given index
        /// </summary>
        /// <param name="_index">Patch index from patch list</param>
        private void DownloadPatch(int _index)
        {
            // Check if this patch was already downloaded previously
            if (File.Exists($"Data/{m_Patches[m_PatchIndex]}"))
            {
                // Calculate hash of local downloaded patch
                string _localHash = string.Empty;
                using (MD5 _crypto = MD5.Create())
                {
                    using FileStream stream = File.OpenRead($"Data/{m_Patches[m_PatchIndex]}");
                    _localHash = BitConverter.ToString(_crypto.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }

                // Compare checksums and skip patch if it matches (no changes)
                if (_localHash.Equals(m_Patches[m_PatchIndex].Checksum))
                {
                    // Continue with next patch
                    m_PatchIndex++;
                    if (m_PatchIndex >= m_Patches.Count)
                        FinishPatch(); // finish if nothing left
                    else
                        DownloadPatch(m_PatchIndex);
                    return;
                }
            }

            // Check if the given patch actually exists on server?
            string url = $"{m_PatchUri}{m_Patches[m_PatchIndex]}";
            WebRequest request = WebRequest.Create(url);
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            }
            catch
            {
                // Stop process.
                FinishPatch();
                return;
            }

            // Update recovery flag
            File.WriteAllText("Cache/L/patching", m_Patches[m_PatchIndex].Filename);

            // Patch it in
            using (WebClient wc = new())
            {
                wc.DownloadProgressChanged += patch_GetPatchesAsync;
                wc.DownloadFileAsync(
                    new Uri($"{m_PatchUri}{m_Patches[m_PatchIndex]}"),
                    $"Data/{m_Patches[m_PatchIndex]}"
                );
                wc.DownloadFileCompleted += patch_DonePatchesAsync;
                m_DownloadStopWatch.Reset();
                m_DownloadStopWatch.Start();
            }
        }

        /// <summary>
        /// Completed patch, check for next.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void patch_DonePatchesAsync(object? sender, AsyncCompletedEventArgs e)
        {
            m_PatchIndex++;
            if (m_PatchIndex >= m_Patches.Count)
                FinishPatch();
            else
                DownloadPatch(m_PatchIndex);
        }

        /// <summary>
        /// Finish patching process and return control.
        /// </summary>
        private void FinishPatch()
        {
            // Reset visual elements
            m_WndRef.progressBar.Value = 100;
            m_WndRef.playBtn.IsEnabled = true;
            m_WndRef.progressInfo.Visibility = Visibility.Hidden;

            // Reset download data and flags
            m_Patching = false;
            m_PatchIndex = -1;
            m_Patches.Clear();
            m_DownloadStopWatch.Reset();

            // Clean up folders
            if (File.Exists("Cache/L/patching"))
                File.Delete("Cache/L/patching");
            if (File.Exists("Cache/L/plist.txt"))
                File.Delete("Cache/L/plist.txt");
        }

        /// <summary>
        /// Update download progress of current patch.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void patch_GetPatchesAsync(object sender, DownloadProgressChangedEventArgs e)
        {
            m_WndRef.progressInfo.Content = $"{e.ProgressPercentage}% (Patch {m_PatchIndex + 1}/{m_Patches.Count}, downloaded {e.BytesReceived / 1024f / 1024f:0.0}/{e.TotalBytesToReceive / 1024f / 1024f:0.0} MB at {(e.BytesReceived / 1024f / 1024f / m_DownloadStopWatch.Elapsed.TotalSeconds).ToString("0.0")} Mb/s)";
            m_WndRef.progressBar.Value = e.ProgressPercentage;
        }

        /// <summary>
        /// Data structure for each patch file
        /// </summary>
        private struct PatchData
        {
            public string Filename;
            public string Checksum;
        }
    }
}
