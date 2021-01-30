using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCProximity
{
    class Manager
    {
        private readonly HttpClient client = new HttpClient();

        private readonly Discord.Discord discord;
        private readonly string ip = ConfigurationManager.AppSettings.Get("IP");
        private string username;
        private long lobbyId = -1;
        private List<string> namesInCall = new List<string>();

        private int callbackCounter = 0;

        public Manager()
        {
            var clientID = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID");
            if (clientID == null)
            {
                clientID = ConfigurationManager.AppSettings.Get("clientID");
            }

            discord = new Discord.Discord(long.Parse(clientID), (ulong)Discord.CreateFlags.Default);

            discord.SetLogHook(Discord.LogLevel.Debug, (level, message) =>
            {
                Trace.WriteLine("Log[" + level + "]: " + message);
            });
        }

        // Starts the proximity voice chat experience
        public void StartProximity(string username, Action<bool> callback)
        {
            Trace.WriteLine("Attemping voice connect with username " + username);
            var lobbyManager = discord.GetLobbyManager();

            // Search for available local proximity chat lobbies
            var search = lobbyManager.GetSearchQuery();
            search.Filter("metadata.type", Discord.LobbySearchComparison.Equal, Discord.LobbySearchCast.String, "minecraft_proximity");
            lobbyManager.Search(search, (result) =>
            {
                if (result == Discord.Result.Ok)
                {
                    var count = lobbyManager.LobbyCount();
                    Trace.WriteLine(count + " proximity chat lobbies found.");
                    if (count == 0)
                    {
                        // Create a proximity chat lobby
                        var txn = lobbyManager.GetLobbyCreateTransaction();
                        txn.SetType(Discord.LobbyType.Public);
                        txn.SetMetadata("type", "minecraft_proximity");

                        lobbyManager.CreateLobby(txn, (Discord.Result result, ref Discord.Lobby lobby) =>
                        {
                            Trace.WriteLine("Proximity chat lobby " + lobby.Id + " created with secret " + lobby.Secret + ".");
                            ConnectToProximityVoiceChat(lobby.Id, username, callback);
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
                                Trace.WriteLine("Connected to proximity chat lobby " + lobby.Id + ".");
                                ConnectToProximityVoiceChat(lobby.Id, username, callback);
                            } 
                            else
                            {
                                callback(false);
                            }
                        });
                    }
                }
                else
                {
                    callback(false);
                }
            });
        }

        // Connects to the proximity voice chat
        private void ConnectToProximityVoiceChat(long lobbyId, string username, Action<bool> callback)
        {
            discord.GetLobbyManager().ConnectVoice(lobbyId, (Discord.Result result) =>
            {
                if (result == Discord.Result.Ok)
                {
                    Trace.WriteLine("Voice connected!");
                    this.lobbyId = lobbyId;
                    this.username = username;

                    var userManager = discord.GetUserManager();
                    userManager.OnCurrentUserUpdate += async () =>
                    {
                        var user = userManager.GetCurrentUser();
                        var json = new JObject
                        {
                            { "Id", user.Id.ToString() }
                        };
                        var data = new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json");

                        var response = await client.PostAsync($"http://{ip}:2021/{username}", data);
                        string responseBody = response.Content.ReadAsStringAsync().Result;
                        Trace.WriteLine(responseBody);
                    };

                    callback(true);
                }
                else
                {
                    Trace.WriteLine("Failed to connect to voice chat.");
                    DisconnectFromProximityLobby(() =>
                    {
                        callback(false);
                    });
                }
            });
        }

        // Disconnects from the proximity lobby
        public void DisconnectFromProximityLobby(Action callback)
        {
            discord.GetLobbyManager().DisconnectLobby(lobbyId, async (Discord.Result result) =>
            {
                if (result == Discord.Result.Ok)
                {
                    Trace.WriteLine("Disconnected from proximity lobby.");

                    var response = await client.DeleteAsync($"http://{ip}:2021/{username}");
                    string responseBody = response.Content.ReadAsStringAsync().Result;
                    Trace.WriteLine(responseBody);

                    if (lobbyId != -1)
                    {
                        lobbyId = -1;
                    }

                    callback();
                }
            });
        }

        public async Task FetchProximityData()
        {
            try
            {
                var voiceManager = discord.GetVoiceManager();

                var proximityTask = client.GetAsync($"http://{ip}:2021/{username}");
                var mapTask = client.GetStringAsync($"http://{ip}:2021/map");

                var proximityResponse = await proximityTask;
                string mapBody = await mapTask;

                if (proximityResponse.StatusCode != HttpStatusCode.NotFound)
                {
                    var proximityBody = proximityResponse.Content.ReadAsStringAsync().Result;
                    // Trace.WriteLine("Proximities = " + proximityBody);
                    // Trace.WriteLine("Map = " + mapBody);

                    JObject proximities = JObject.Parse(proximityBody);
                    JObject map = JObject.Parse(mapBody);

                    var lobbyManager = discord.GetLobbyManager();

                    namesInCall.Clear();
                    foreach (JProperty property in map.Properties())
                    {
                        long userId = Convert.ToInt64(property.Value);
                        string username = property.Name;
                        namesInCall.Add(username);

                        if (proximities.ContainsKey(username))
                        {
                            double volume = double.Parse((string)proximities.GetValue(username));
                            byte newVolume = Convert.ToByte(Math.Pow(volume / 100, 3) * 100);
                            // Trace.WriteLine(volume + " -> " + newVolume);
                            voiceManager.SetLocalVolume(userId, newVolume);
                        }
                        else if (voiceManager.GetLocalVolume(userId) != 100)
                        {
                            voiceManager.SetLocalVolume(userId, 100);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("Exception: " + e.Message);
            }
        }

        public async Task RunCallbacks()
        {
            discord.RunCallbacks();
            discord.GetLobbyManager().FlushNetwork();
            if (callbackCounter >= 1 * 10 && lobbyId != -1)
            {
                callbackCounter = 0;
                await FetchProximityData();
            }
            callbackCounter++;
        }
    }
}
