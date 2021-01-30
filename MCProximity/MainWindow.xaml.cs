using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Media;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MCProximity
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Manager manager;

        private bool processingCallbacks = false;

        private ConnectionStatus status;
        private enum ConnectionStatus
        {
            DISCONNECTED,
            CONNECTING,
            CONNECTED,
            CONNECTION_FAILED
        }

        private Dictionary<string, Canvas> canvasCache;

        private MediaPlayer player;

        public MainWindow()
        {
            InitializeComponent();
            manager = new Manager();

            status = ConnectionStatus.DISCONNECTED;
            
            canvasCache = new Dictionary<string, Canvas>();

            player = new MediaPlayer();
            player.Volume = 0.5;

            manager.Join += (object sender, EventArgs e) =>
            {
                PlayJoinSound();
            };
            manager.Leave += (object sender, EventArgs e) =>
            {
                PlayLeaveSound();
            };

            DispatcherTimer timer = new DispatcherTimer();
            timer.Tick += Refresh;
            timer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            timer.Start();
        }

        private void PlayJoinSound()
        {
            player.Open(new Uri("Sound/join.wav", UriKind.Relative));
            player.Play();
        }

        private void PlayLeaveSound()
        {
            player.Open(new Uri("Sound/leave.wav", UriKind.Relative));
            player.Play();
        }

        private void Connect(object sender, RoutedEventArgs e)
        {
            if (Username.Text.Length >= 3)
            {
                Trace.WriteLine("Connecting...");
                SetHeaderEnabled(false);
                SetConnectionStatus(ConnectionStatus.CONNECTING);

                manager.StartProximity(Username.Text, (success) =>
                {
                    if (success)
                    {
                        SetConnectionStatus(ConnectionStatus.CONNECTED);
                        PlayJoinSound();
                    }
                    else
                    {
                        SetHeaderEnabled(true);
                        SetConnectionStatus(ConnectionStatus.CONNECTION_FAILED);
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
                    SetConnectionStatus(ConnectionStatus.DISCONNECTED);

                    Participants.Children.Clear();

                    PlayLeaveSound();
                });
            }
        }

        private async void WindowClosing(object sender, CancelEventArgs e)
        {
            if (status == ConnectionStatus.CONNECTED)
            {
                Trace.WriteLine(await manager.UnmapName());
            }
            manager.Dispose();
        }

        private void SetHeaderEnabled(bool enabled)
        {
            UsernameLabel.IsEnabled = enabled;
            Username.IsEnabled = enabled;
            Join.IsEnabled = enabled;
        }

        private void SetConnectionStatus(ConnectionStatus status)
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

        private void UpdateVoiceView(List<VoiceMember> members)
        {
            Participants.Children.Clear();

            foreach (VoiceMember member in members)
            {
                Canvas c = VoiceMemberToCanvas(member);
                if (Participants.Children.Contains(c))
                {
                    Participants.Children.Remove(c);
                }
                Participants.Children.Add(c);
            }
        }

        private Canvas VoiceMemberToCanvas(VoiceMember member)
        {
            Canvas c;

            if (canvasCache.ContainsKey(member.Username))
            {
                c = canvasCache[member.Username];
            }
            else
            {
                c = new Canvas();
                c.Height = 120;
                c.Width = 100;

                Image i;
                i = new Image();
                i.Height = 60;
                i.Width = 60;
                i.Source = new BitmapImage(new Uri("https://mc-heads.net/avatar/" + member.Username, UriKind.Absolute));
                Canvas.SetLeft(i, 20);
                Canvas.SetTop(i, 20);
                c.Children.Add(i);

                TextBlock t = new TextBlock();
                t.Text = member.Username;
                t.TextAlignment = TextAlignment.Center;
                t.TextWrapping = TextWrapping.NoWrap;
                t.Width = 80;
                Canvas.SetLeft(t, 10);
                Canvas.SetTop(t, 92);
                c.Children.Add(t);

                TextBlock e = new TextBlock();
                e.Name = "State";
                e.Background = Brushes.White;
                e.FontFamily = new FontFamily("Segoe MDL2 Assets");
                e.FontSize = 16;
                e.FontWeight = FontWeights.Bold;
                e.Height = 20;
                e.Width = 20;
                e.Padding = new Thickness(2);
                e.Text = "\xF270;";
                e.TextAlignment = TextAlignment.Center;
                Canvas.SetLeft(e, 70);
                Canvas.SetTop(e, 70);
                c.Children.Add(e);

                canvasCache.Add(member.Username, c);
            }

            TextBlock state = LogicalTreeHelper.FindLogicalNode(c, "State") as TextBlock;
            if (member.IsInServer)
            {
                if (state.Text == "\xF270;")
                {
                    state.Text = "\xE774;";
                }
            }
            else
            {
                if (state.Text == "\xE774;")
                {
                    state.Text = "\xF270;";
                }
            }

            return c;
        }

        private void Mute(object sender, RoutedEventArgs e)
        {

        }

        private void Unmute(object sender, RoutedEventArgs e)
        {

        }

        private async void Refresh(object sender, EventArgs e)
        {
            if (!processingCallbacks)
            {
                processingCallbacks = true;
                bool update = manager.RunCallbacks();
                if (update)
                {
                    UpdateVoiceView(await manager.UpdateProximityData());
                }
                processingCallbacks = false;
            }
        }
    }
}
