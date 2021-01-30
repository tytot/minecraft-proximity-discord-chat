using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace MCProximity
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Manager manager;

        private ConnectionStatus status;
        private enum ConnectionStatus
        {
            DISCONNECTED,
            CONNECTING,
            CONNECTED,
            CONNECTION_FAILED
        }


        public MainWindow()
        {
            InitializeComponent();
            manager = new Manager();

            DispatcherTimer timer = new DispatcherTimer();
            timer.Tick += Refresh;
            timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            timer.Start();
        }

        private void Connect(object sender, RoutedEventArgs e)
        {
            if (Username.Text.Length >= 3)
            {
                Trace.WriteLine("Connecting...");
                SetHeaderEnabled(false);
                SetStatus(ConnectionStatus.CONNECTING);

                manager.StartProximity(Username.Text, (success) =>
                {
                    if (success)
                    {
                        SetStatus(ConnectionStatus.CONNECTED);
                    }
                    else
                    {
                        SetHeaderEnabled(true);
                        SetStatus(ConnectionStatus.CONNECTION_FAILED);
                    }
                });
            }
            else
            {
                MessageBox.Show("Enter a valid Minecraft username.", "boi");
            }
        }

        private void Disconnect(object sender, RoutedEventArgs e)
        {
            if (status == ConnectionStatus.CONNECTED)
            {
                manager.DisconnectFromProximityLobby(() =>
                {
                    SetHeaderEnabled(true);
                    SetStatus(ConnectionStatus.DISCONNECTED);
                });
            }
        }

        private void SetHeaderEnabled(bool enabled)
        {
            UsernameLabel.IsEnabled = enabled;
            Username.IsEnabled = enabled;
            Join.IsEnabled = enabled;
        }

        private void SetStatus(ConnectionStatus status)
        {
            if (status == ConnectionStatus.DISCONNECTED)
            {
                Status.Text = "Disconnected";
                Status.Foreground = Brushes.Red;
            }
            else if (status == ConnectionStatus.CONNECTING)
            {
                Status.Text = "Connecting";
                Status.Foreground = Brushes.Yellow;
            }
            else if (status == ConnectionStatus.CONNECTED)
            {
                Status.Text = "Connected";
                Status.Foreground = Brushes.Green;
            }
            else
            {
                Status.Text = "Failed to Connect";
                Status.Foreground = Brushes.Red;
            }
            this.status = status;
        }

        private void Mute(object sender, RoutedEventArgs e)
        {

        }

        private void Unmute(object sender, RoutedEventArgs e)
        {

        }

        private async void Refresh(object sender, EventArgs e)
        {
            await manager.RunCallbacks();
        }
    }
}
