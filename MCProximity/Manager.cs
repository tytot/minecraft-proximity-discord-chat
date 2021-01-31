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
    readonly struct VoiceMember
    {
        public VoiceMember(string username, bool inServer, bool hearable, bool muted)
        {
            Username = username;
            IsInServer = inServer;
            IsHearable = hearable;
            IsMuted = muted;
        }

        public string Username { get; }
        public bool IsInServer { get; }
        public bool IsHearable { get; }
        public bool IsMuted { get; }
    }

    class Manager
    {
        private readonly HttpClient client = new HttpClient();

        private readonly Discord.Discord discord;
        private readonly Discord.LobbyManager lobbyManager;
        private readonly Discord.UserManager userManager;
        private readonly Discord.VoiceManager voiceManager;

        private readonly string ip = ConfigurationManager.AppSettings.Get("IP");
        private string username;
        private long lobbyId = -1;

        public event EventHandler Join, Leave;

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

            userManager = discord.GetUserManager();
            userManager.OnCurrentUserUpdate += () =>
            {
                Trace.WriteLine("User Manager instantiated.");
            };

            lobbyManager = discord.GetLobbyManager();
            lobbyManager.OnMemberConnect += (long lobbyId, long userId) =>
            {
                if (lobbyId == this.lobbyId)
                {
                    EventHandler handler = Join;
                    handler?.Invoke(this, EventArgs.Empty);
                }
            };
            lobbyManager.OnMemberDisconnect += (long lobbyId, long userId) =>
            {
                if (lobbyId == this.lobbyId)
                {
                    EventHandler handler = Leave;
                    handler?.Invoke(this, EventArgs.Empty);
                }
            };

            voiceManager = discord.GetVoiceManager();
        }

        // Starts the proximity voice chat experience
        public void StartProximity(string username, Action<bool> callback)
        {
            Trace.WriteLine("Attemping voice connect with username " + username);

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
                        lobbyManager.ConnectLobbyWithActivitySecret(secret, (Discord.Result result, ref Discord.Lobby lobby) =>
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
            lobbyManager.ConnectVoice(lobbyId, async (Discord.Result result) =>
            {
                if (result == Discord.Result.Ok)
                {
                    Trace.WriteLine("Voice connected!");

                    var user = userManager.GetCurrentUser();

                    var json = new JObject
                    {
                        { "Id", user.Id.ToString() }
                    };
                    var data = new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json");

                    var response = await client.PostAsync($"http://{ip}:2021/{username}", data);
                    string responseBody = response.Content.ReadAsStringAsync().Result;
                    Trace.WriteLine(responseBody);

                    this.lobbyId = lobbyId;
                    this.username = username;

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
            lobbyManager.DisconnectLobby(lobbyId, async (Discord.Result result) =>
            {
                if (result == Discord.Result.Ok)
                {
                    Trace.WriteLine("Disconnected from proximity lobby.");

                    await UnmapName();

                    if (lobbyId != -1)
                    {
                        lobbyId = -1;
                    }
                    username = null;

                    callback();
                }
            });
        }

        public async Task UnmapName()
        {
            await MuteSelf(false);
            var response = await client.DeleteAsync($"http://{ip}:2021/{username}");
            Trace.WriteLine(response.Content.ReadAsStringAsync().Result);
        }

        public async Task MuteSelf(bool mute)
        {
            voiceManager.SetSelfMute(mute);

            var json = new JObject
                    {
                        { "name",  username },
                        { "mute", mute }
                    };
            var data = new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"http://{ip}:2021/muted", data);
            Trace.WriteLine(response.Content.ReadAsStringAsync().Result);
        }

        // Updates local volumes of other members in the call
        public async Task<List<VoiceMember>> UpdateProximityData()
        {
            try
            {
                var proximityTask = client.GetAsync($"http://{ip}:2021/{username}");
                var mapTask = client.GetStringAsync($"http://{ip}:2021/map");
                var mutedTask = client.GetStringAsync($"http://{ip}:2021/muted");

                var proximityResponse = await proximityTask;
                string mapBody = await mapTask;
                string mutedBody = await mutedTask;
                JObject map = JObject.Parse(mapBody);
                JArray muted = JArray.Parse(mutedBody);

                var voiceMembers = new List<VoiceMember>();

                if (proximityResponse.StatusCode != HttpStatusCode.NotFound)
                {
                    voiceMembers.Add(new VoiceMember(username, true, true, ContainsName(muted, username)));

                    var proximityBody = proximityResponse.Content.ReadAsStringAsync().Result;
                    // Trace.WriteLine("Proximities = " + proximityBody);
                    // Trace.WriteLine("Map = " + mapBody);

                    JObject proximities = JObject.Parse(proximityBody);

                    foreach (JProperty property in map.Properties())
                    {
                        string username = property.Name;
                        if (username != this.username)
                        {
                            long userId = Convert.ToInt64(property.Value);

                            if (proximities.ContainsKey(username))
                            {
                                double volume = double.Parse((string)proximities.GetValue(username));
                                byte newVolume = Convert.ToByte(Math.Pow(volume / 100, 3) * 100);
                                // Trace.WriteLine(volume + " -> " + newVolume);
                                voiceManager.SetLocalVolume(userId, newVolume);

                                voiceMembers.Add(new VoiceMember(username, true, newVolume > 0, ContainsName(muted, username)));
                            }
                            else 
                            {
                                if (voiceManager.GetLocalVolume(userId) != 100)
                                {
                                    voiceManager.SetLocalVolume(userId, 100);
                                }
                                voiceMembers.Add(new VoiceMember(username, false, true, ContainsName(muted, username)));
                            }
                        }
                    }
                }
                else
                {
                    foreach (JProperty property in map.Properties())
                    {
                        long userId = Convert.ToInt64(property.Value);
                        string username = property.Name;

                        if (voiceManager.GetLocalVolume(userId) != 100)
                        {
                            voiceManager.SetLocalVolume(userId, 100);
                        }
                        voiceMembers.Add(new VoiceMember(username, false, true, ContainsName(muted, username)));
                    }
                }
                return voiceMembers;
            }
            catch (Exception e)
            {
                Trace.WriteLine("Exception: " + e.Message);
            }

            return null;
        }

        private bool ContainsName(JArray arr, String name)
        {
            foreach (var item in arr.Children())
            {
                if (item.ToString().Equals(name))
                {
                    return true;
                }
            }
            return false;
        }

        // Returns true if proximity data was updated
        public bool RunCallbacks()
        {
            bool update = false;

            try
            {
                discord.RunCallbacks();
                lobbyManager.FlushNetwork();
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }
            if (callbackCounter >= 5 && lobbyId != -1)
            {
                callbackCounter = 0;
                update = true;
            }
            callbackCounter++;

            return update;
        }

        public void Dispose()
        {
            discord.Dispose();
        }
    }
}
