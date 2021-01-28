using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Application
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static long lobbyId = -1;
        private static string ip, username;

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler();
        static EventHandler _handler;

        private static bool Handler()
        {
            var task = Task.Run(() => client.DeleteAsync($"http://{ip}:2021/{username}"));
            task.Wait();
            var response = task.Result;
            string responseBody = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine(responseBody);

            lobbyId = -1;

            return false;
        }

        // Starts the proximity voice chat experience
        private static void StartProximity(Discord.Discord discord)
        {
            var lobbyManager = discord.GetLobbyManager();

            // Search for available local proximity chat lobbies
            var search = lobbyManager.GetSearchQuery();
            search.Filter("metadata.type", Discord.LobbySearchComparison.Equal, Discord.LobbySearchCast.String, "minecraft_proximity");
            lobbyManager.Search(search, (result) =>
            {
                if (result == Discord.Result.Ok)
                {
                    var count = lobbyManager.LobbyCount();
                    Console.WriteLine("{0} proximity chat lobbies found.", count);
                    if (count == 0)
                    {
                        // Create a proximity chat lobby
                        var txn = lobbyManager.GetLobbyCreateTransaction();
                        txn.SetType(Discord.LobbyType.Public);
                        txn.SetMetadata("type", "minecraft_proximity");

                        lobbyManager.CreateLobby(txn, (Discord.Result result, ref Discord.Lobby lobby) =>
                        {
                            Console.WriteLine("Proximity chat lobby {0} created with secret {1}.", lobby.Id, lobby.Secret);
                            ConnectToProximityVoiceChat(discord);
                        });
                    }
                    else
                    {
                        // Connect to the proximity chat lobby
                        var id = lobbyManager.GetLobbyId(0);
                        var secret = lobbyManager.GetLobbyActivitySecret(id);
                        discord.GetLobbyManager().ConnectLobbyWithActivitySecret(secret, (Discord.Result result, ref Discord.Lobby lobby) =>
                        {
                            if (result == Discord.Result.Ok)
                            {
                                Console.WriteLine("Connected to proximity chat lobby {0}.", lobby.Id);
                                ConnectToProximityVoiceChat(discord);
                            }
                        });
                    }
                }
            });
        }

        // Connects to the proximity voice chat with the given lobby ID
        private static void ConnectToProximityVoiceChat(Discord.Discord discord)
        {
            discord.GetLobbyManager().ConnectVoice(lobbyId, (Discord.Result result) =>
            {
                if (result == Discord.Result.Ok)
                {
                    Console.WriteLine("Voice connected!");

                    var userManager = discord.GetUserManager();
                    userManager.OnCurrentUserUpdate += async () =>
                    {
                        var user = userManager.GetCurrentUser();
                        var json = new JObject();
                        json.Add("Id", user.Id.ToString());
                        var data = new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json");

                        var response = await client.PostAsync($"http://{ip}:2021/{username}", data);
                        string responseBody = response.Content.ReadAsStringAsync().Result;
                        Console.WriteLine(responseBody);
                    };
                }
            });
        }

        // Disconnects from the proximity lobby with the given lobby ID
        private static void DisconnectFromProximityLobby(Discord.Discord discord)
        {
            discord.GetLobbyManager().DisconnectLobby(lobbyId, async (Discord.Result result) =>
            {
                if (result == Discord.Result.Ok)
                {
                    Console.WriteLine("Voice disconnected.");

                    var response = await client.DeleteAsync($"http://{ip}:2021/{username}");
                    string responseBody = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine(responseBody);

                    lobbyId = -1;
                }
            });
        }

        private static async Task FetchProximityData(Discord.Discord discord)
        {
            try
            {
                var voiceManager = discord.GetVoiceManager();

                var proximityTask = client.GetStringAsync($"http://{ip}:2021/{username}");
                var mapTask = client.GetStringAsync($"http://{ip}:2021/map");

                string proximityBody = await proximityTask;
                string mapBody = await mapTask;
                // Console.WriteLine("Proximities = " + proximityBody);
                // Console.WriteLine("Map = " + mapBody);

                JObject proximities = JObject.Parse(proximityBody);
                JObject map = JObject.Parse(mapBody);

                var lobbyManager = discord.GetLobbyManager();

                var inServer = new HashSet<long>();
                foreach (JProperty property in proximities.Properties())
                {
                    // Console.WriteLine(property.Name + ": " + property.Value);
                    if (map.ContainsKey(property.Name))
                    {
                        long userId = Convert.ToInt64(map.GetValue(property.Name));
                        inServer.Add(userId);

                        double volume = Double.Parse((string)property.Value);
                        byte newVolume = Convert.ToByte(Math.Pow(volume / 100, 3) * 100);
                        // Console.WriteLine(volume + " -> " + newVolume);
                        voiceManager.SetLocalVolume(userId, newVolume);
                    }
                }

                var memberCount = lobbyManager.MemberCount(lobbyId);
                for (int i = 0; i < memberCount; i++)
                {
                    var id = lobbyManager.GetMemberUserId(lobbyId, i);
                    if (!inServer.Contains(id) && voiceManager.GetLocalVolume(id) != 100)
                    {
                        voiceManager.SetLocalVolume(id, 100);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
            }
        }

        static async Task Main(string[] args)
        {
            Console.Write("Enter your Minecraft username: ");
            username = Console.ReadLine();
            ip = ConfigurationManager.AppSettings.Get("IP");

            // Use your client ID from Discord's developer site.
            var clientID = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID");
            if (clientID == null)
            {
                clientID = ConfigurationManager.AppSettings.Get("clientID");
            }
            var discord = new Discord.Discord(Int64.Parse(clientID), (UInt64)Discord.CreateFlags.Default);
            var lobbyManager = discord.GetLobbyManager();

            discord.SetLogHook(Discord.LogLevel.Debug, (level, message) =>
            {
                Console.WriteLine("Log[{0}] {1}", level, message);
            });

            StartProximity(discord);

            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);

            // Pump the event look to ensure all callbacks continue to get fired.
            try
            {
                int counter = 0;
                while (true)
                {
                    discord.RunCallbacks();
                    lobbyManager.FlushNetwork();
                    var task = Task.Delay(100);
                    if (counter >= 1 * 10 && lobbyId != -1)
                    {
                        counter = 0;
                        task = FetchProximityData(discord);
                    }
                    await task;
                    counter++;
                }
            }
            finally
            {
                discord.Dispose();
            }
        }
    }
}
