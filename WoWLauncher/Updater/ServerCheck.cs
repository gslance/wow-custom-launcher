using System;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WoWLauncher.Updater
{
    /// <summary>
    /// Responsible for announcing server status.
    /// </summary>
    internal class ServerCheck
    {
        // Reference parent window
        private MainWindow m_WndRef;

        // Data
        private UpdateController m_UpdaterRef;

        public ServerCheck(MainWindow _wndRef, ref UpdateController _updater)
        {
            m_WndRef = _wndRef;

            // Let's keep a reference to the updater for the server address
            m_UpdaterRef = _updater;

            // Check server status every 1 minute
            DispatcherTimer timer = new()
            {
                Interval = TimeSpan.FromSeconds(60),
            };
            timer.Tick += CheckServerStatus;
            timer.Start();

            // Update status icons
            m_WndRef.serverStatusIcon.Source = new BitmapImage(new Uri(@"/WoWLauncher;component/images/Indicator-Yellow.png", UriKind.Relative));
            m_WndRef.serverStatus.Content = "Checking server status...";

            // Begin checking immediately
            CheckServerStatus(null, null);
        }

        /// <summary>
        /// Checks server status
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckServerStatus(object? sender, EventArgs? e)
        {
            bool _serverAvailable;

            try
            {
                // Create a simple TCP client to ping a port
                using TcpClient _tcpClient = new TcpClient();
                IAsyncResult _asyncConnectionResult = _tcpClient.BeginConnect(m_UpdaterRef.RealmAddress, 8085, null, null);
                WaitHandle _asyncConnectionWaitHandle = _asyncConnectionResult.AsyncWaitHandle;

                try
                {
                    // Allow to time-out after 5 seconds
                    if (!_asyncConnectionWaitHandle.WaitOne(TimeSpan.FromMilliseconds(5000), false))
                    {
                        _tcpClient.EndConnect(_asyncConnectionResult);
                        _tcpClient.Close();
                        throw new SocketException();
                    }

                    // Looks like there's response. The server is alive!
                    _serverAvailable = true;
                    _tcpClient.EndConnect(_asyncConnectionResult);
                }
                finally
                {
                    // Regardless of response, we'll stop connection
                    _asyncConnectionWaitHandle.Close();
                }
            }
            catch
            {
                // Some kind of error prevents us, we'll assume it's inaccessible
                _serverAvailable = false;
            }

            // Update texts and graphics
            if (_serverAvailable)
            {
                m_WndRef.serverStatusIcon.Source = new BitmapImage(new Uri(@"/WoWLauncher;component/images/Indicator-Green.png", UriKind.Relative));
                m_WndRef.serverStatus.Content = "Server online!";
                // we don't enable play button here as patcher may still be active, it will enable it elsewhere 
            }
            else
            {
                m_WndRef.serverStatusIcon.Source = new BitmapImage(new Uri(@"/WoWLauncher;component/images/Indicator-Red.png", UriKind.Relative));
                m_WndRef.serverStatus.Content = "Server offline.";
                m_WndRef.playBtn.IsEnabled = false;
            }
        }
    }
}
