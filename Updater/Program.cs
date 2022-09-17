using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;

internal class Program
{
    // Program running flag
    private static bool m_Running;
    private static string m_ClientUpdateUri = "https://www.example.com/Patch/client.zip";

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

    /// <summary>
    /// Program entry
    /// </summary>
    /// <param name="args"></param>
    private static void Main(string[] args)
    {
        m_Running = true;
        Console.WriteLine("*** Welcome to the Launcher Updater! ***");
        Console.WriteLine("\n\r");
        Console.WriteLine("> Fetching update...");

        // prepare folders
        if (!Directory.Exists("Cache/L"))
            Directory.CreateDirectory("Cache/L");

        // fetch zip
        using (WebClient wc = new())
        {
            wc.DownloadProgressChanged += update_Progress;
            wc.DownloadFileAsync(new Uri(m_ClientUpdateUri), "Cache/L/launcher.zip");
            wc.DownloadFileCompleted += update_Completed;
        }

        // wait to finish before exiting
        while (m_Running)
        {
            Thread.Sleep(100);
        }
    }

    /// <summary>
    /// Unpacks update when download task finishes.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void update_Completed(object? sender, AsyncCompletedEventArgs e)
    {
        Console.WriteLine("\r\n");
        Console.WriteLine("> Downloading complete!");
        Console.WriteLine("> Unpacking...");
        Unpack();
    }

    /// <summary>
    /// Progress while downloading update.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void update_Progress(object sender, DownloadProgressChangedEventArgs e)
    {
        Console.Write($"\r> Downloading... {e.ProgressPercentage}%");
    }

    /// <summary>
    /// Starts unpacking process before finishing.
    /// </summary>
    private async static void Unpack()
    {
        await Task.Run(() => Unzip());
        Finish();
    }

    /// <summary>
    /// Unpacks update using ZIP.
    /// </summary>
    private static void Unzip()
    {
        try
        {
            ZipFile.ExtractToDirectory("Cache/L/launcher.zip", Directory.GetCurrentDirectory(), true);
        }
        catch
        {
            Console.WriteLine($"> Can't unzip update. Try again... ");
        };
    }

    /// <summary>
    /// Finish up and re-launch original application.
    /// </summary>
    private static void Finish()
    {
        // Clean up files
        if (File.Exists("Cache/L/launcher.zip"))
            File.Delete("Cache/L/launcher.zip");

        // If the launcher is here (it should be), launch it again
        if (File.Exists("WoWLauncher.exe"))
        {
            Process.Start(new ProcessStartInfo("WoWLauncher.exe")
            {
                UseShellExecute = true
            });
        }
        
        // Exit
        m_Running = false;
    }
}