using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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

        private readonly Dictionary<string, Canvas> canvasCache;

        private readonly string EarIcon = "\xF270";
        private readonly string GlobeIcon = "\xE774";
        private readonly string MuteIcon = "\xE74F";

        private readonly string JoinSound = "Sound/join.wav";
        private readonly string LeaveSound = "Sound/leave.wav";
        private readonly string MuteSound = "Sound/mute.wav";
        private readonly string UnmuteSound = "Sound/unmute.wav";

        private readonly MediaPlayer player;

        public MainWindow()
        {
            InitializeComponent();
            SetFooterEnabled(false);

            manager = new Manager();

            status = ConnectionStatus.DISCONNECTED;
            
            canvasCache = new Dictionary<string, Canvas>();

            player = new MediaPlayer
            {
                Volume = 0.5
            };

            manager.Join += (object sender, EventArgs e) =>
            {
                PlaySound(JoinSound);
            };
            manager.Leave += (object sender, EventArgs e) =>
            {
                PlaySound(LeaveSound);
            };

            DispatcherTimer timer = new DispatcherTimer();
            timer.Tick += Refresh;
            timer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            timer.Start();
        }

        private void PlaySound(string path)
        {
            player.Open(new Uri(path, UriKind.Relative));
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
                        SetFooterEnabled(true);
                        SetConnectionStatus(ConnectionStatus.CONNECTED);
                        PlaySound(JoinSound);
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
                    SetFooterEnabled(false);
                    SetConnectionStatus(ConnectionStatus.DISCONNECTED);

                    Participants.Children.Clear();

                    PlaySound(LeaveSound);
                });
            }
        }

        private async void WindowClosing(object sender, CancelEventArgs e)
        {
            if (status == ConnectionStatus.CONNECTED)
            {
                SetHeaderEnabled(true);
                SetFooterEnabled(false);
                await manager.UnmapName();
            }
            manager.Dispose();
        }

        private void SetHeaderEnabled(bool enabled)
        {
            UsernameLabel.IsEnabled = enabled;
            Username.IsEnabled = enabled;
            Join.IsEnabled = enabled;
        }

        private void SetFooterEnabled(bool enabled)
        {
            Muter.IsEnabled = enabled;
            MuterBack.IsEnabled = enabled;
            HangUp.IsEnabled = enabled;
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
                c = new Canvas
                {
                    Height = 120,
                    Width = 100
                };

                Image i;
                i = new Image
                {
                    Height = 60,
                    Width = 60,
                    Source = new BitmapImage(new Uri("https://mc-heads.net/avatar/" + member.Username, UriKind.Absolute))
                };
                Canvas.SetLeft(i, 20);
                Canvas.SetTop(i, 20);
                c.Children.Add(i);

                TextBlock t = new TextBlock
                {
                    Text = member.Username,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.NoWrap,
                    Width = 80
                };
                Canvas.SetLeft(t, 10);
                Canvas.SetTop(t, 92);
                c.Children.Add(t);

                Rectangle r = new Rectangle
                {
                    Name = "Overlay",
                    Fill = Brushes.Black,
                    Height = 60,
                    Width = 60,
                    Opacity = 0.75,
                    Visibility = Visibility.Hidden
                };
                Canvas.SetLeft(r, 20);
                Canvas.SetTop(r, 20);
                c.Children.Add(r);

                TextBlock m = new TextBlock
                {
                    Name = "Mic",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 42,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    Height = 60,
                    Width = 60,
                    Padding = new Thickness(10),
                    Text = "",
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(m, 20);
                Canvas.SetTop(m, 20);
                c.Children.Add(m);

                TextBlock e = new TextBlock
                {
                    Name = "State",
                    Background = Brushes.White,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Height = 20,
                    Width = 20,
                    Padding = new Thickness(2),
                    Text = EarIcon,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(e, 70);
                Canvas.SetTop(e, 70);
                c.Children.Add(e);

                canvasCache.Add(member.Username, c);
            }

            Rectangle overlay = LogicalTreeHelper.FindLogicalNode(c, "Overlay") as Rectangle;
            if (member.IsMuted || !member.IsHearable)
            {
                if (overlay.Visibility == Visibility.Hidden)
                {
                    overlay.Visibility = Visibility.Visible;
                }
            }
            else
            {
                if (overlay.Visibility == Visibility.Visible)
                {
                    overlay.Visibility = Visibility.Hidden;
                }
            }

            TextBlock mic = LogicalTreeHelper.FindLogicalNode(c, "Mic") as TextBlock;
            if (member.IsMuted)
            {
                if (mic.Text == "")
                {
                    mic.Text = MuteIcon;
                }
            }
            else
            {
                if (mic.Text == MuteIcon)
                {
                    mic.Text = "";
                }
            }

            TextBlock state = LogicalTreeHelper.FindLogicalNode(c, "State") as TextBlock;
            if (member.IsInServer)
            {
                if (state.Text == GlobeIcon)
                {
                    state.Text = EarIcon;
                }
            }
            else
            {
                if (state.Text == EarIcon)
                {
                    state.Text = GlobeIcon;
                }
            }

            return c;
        }

        private async void Mute(object sender, RoutedEventArgs e)
        {
            Muter.Visibility = Visibility.Hidden;
            PlaySound(MuteSound);

            await manager.MuteSelf(true);
        }

        private async void Unmute(object sender, RoutedEventArgs e)
        {
            Muter.Visibility = Visibility.Visible;
            PlaySound(UnmuteSound);

            await manager.MuteSelf(false);
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
