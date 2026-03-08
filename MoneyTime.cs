/*
Fixed by SeesAll, version incremented by 1 to indicate a change. The readme notes can be found at https://github.com/SeesAll/MoneyTime-Fixed
I take no credit for this plugin, I merely fixed a few subtle issues.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Money Time", "Wulf", "2.3.1")]
    [Description("Pays players with Economics money for playing")]
    public class MoneyTime : CovalencePlugin
    {
        #region Configuration

        private const string Perm = "moneytime.";
        private Configuration _config;

        public class Configuration
        {
            [JsonProperty("Enable Economics as default currency")]
            public bool Economics = true;

            [JsonProperty("Enable Server Rewards as default currency")]
            public bool ServerRewards = false;

            [JsonProperty("Enable AFK API plugin support")]
            public bool AfkApi = false;

            [JsonProperty("Base payout amount")]
            public int BasePayout = 100;

            [JsonProperty("Payout interval (seconds)")]
            public int PayoutInterval = 600;

            [JsonProperty("Time alive bonus")]
            public bool TimeAliveBonus = false;

            [JsonProperty("Time alive multiplier")]
            public float TimeAliveMultiplier = 0f;

            [JsonProperty("Allow Permission-based Multipliers to stack")]
            public bool StackMultipliers = false;

            [JsonProperty("New player welcome bonus")]
            public float WelcomeBonus = 500f;

            [JsonProperty("Permission-based mulitipliers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public SortedDictionary<string, float> PermissionMulitipliers = new SortedDictionary<string, float>
            {
                ["vip"] = 5f,
                ["donor"] = 2.5f
            };

            public string ToJson() => JsonConvert.SerializeObject(this);
            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MissedPayout"] = "You have been inactive and have missed this payout",
                ["ReceivedForPlaying"] = "You have received $payout.amount for actively playing",
                ["ReceivedForTimeAlive"] = "You have received $payout.amount for staying alive for $time.alive",
                ["ReceivedWelcomeBonus"] = "You have received $payout.amount as a welcome bonus"
            }, this);
        }

        #endregion

        #region Data Storage

        private StoredData _storedData;

        private class StoredData
        {
            public Dictionary<string, PlayerInfo> Players = new Dictionary<string, PlayerInfo>();
        }

        private class PlayerInfo
        {
            public DateTime LastTimeAlive;
            public bool WelcomeBonus;

            public PlayerInfo()
            {
                LastTimeAlive = DateTime.Now;
                WelcomeBonus = false;
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);

        private void OnServerSave() => SaveData();

        #endregion

        #region Initialization

        [PluginReference]
        private Plugin Economics, ServerRewards, AFKAPI;

        private Dictionary<string, Values> _payOut = new Dictionary<string, Values>();
        private Dictionary<string, double> _perms = new Dictionary<string, double>();

        private class Values
        {
            public double amount;
            public float time;
        }

        private void Init()
        {
            foreach (KeyValuePair<string, float> perm in _config.PermissionMulitipliers)
            {
                string p = Perm + perm.Key;
                _perms[p] = perm.Value;
                permission.RegisterPermission(p, this);
                Log($"Registered permission '{p}'; multiplier {perm.Value}");
            }

            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

            if (!_config.TimeAliveBonus)
                Unsubscribe(nameof(OnUserRespawn));
        }

        private void InitializePlayer(IPlayer player, float current)
        {
            string id = player.Id;
            if (!_storedData.Players.ContainsKey(id))
                _storedData.Players.Add(id, new PlayerInfo());

            double amt = _config.BasePayout;
            double multi = 0;

            if (_config.StackMultipliers)
            {
                foreach (var perm in _perms)
                    if (player.HasPermission(perm.Key))
                        multi += amt * perm.Value;

                if (multi != 0)
                    amt = multi;
            }
            else
            {
                foreach (var perm in _perms)
                    if (player.HasPermission(perm.Key) && perm.Value > multi)
                        multi = perm.Value;

                if (multi != 0)
                    amt *= multi;
            }

            if (!_payOut.ContainsKey(id))
                _payOut.Add(id, new Values { amount = amt, time = current });
            else
                _payOut[id].amount = amt;
        }

        private void OnServerInitialized()
        {
            var current = Time.realtimeSinceStartup + _config.PayoutInterval;
            foreach (IPlayer player in players.Connected)
                if (player.Id.IsSteamId())
                    InitializePlayer(player, current);

            timer.Every(_config.PayoutInterval, () =>
            {
                var newTime = Time.realtimeSinceStartup;
                foreach (IPlayer player in players.Connected)
                {
                    string id = player.Id;
                    if (!_payOut.ContainsKey(id)) continue;

                    var payout = _payOut[id];
                    if (payout.time <= newTime)
                    {
                        Payout(player, payout.amount, GetLang("ReceivedForPlaying", id));
                        payout.time = newTime + _config.PayoutInterval;
                    }
                }
            });
        }

        private void Unload()
        {
            SaveData();
            _perms.Clear();
            _payOut.Clear();
        }

        #endregion

        #region On Perms Updated

        private void OnGroupPermissionGranted(string name, string perm) => Edit(true, _perms.ContainsKey(perm));

        private void OnGroupPermissionRevoked(string name, string perm) => Edit(true, _perms.ContainsKey(perm));

        private void OnUserPermissionGranted(string id, string perm) => Edit(false, _perms.ContainsKey(perm), id);

        private void OnUserPermissionRevoked(string id, string perm) => Edit(false, _perms.ContainsKey(perm), id);

        private void Edit(bool all, bool mine, string user = "")
        {
            if (!mine) return;

            var current = Time.realtimeSinceStartup + _config.PayoutInterval;
            if (all)
            {
                foreach (IPlayer player in players.Connected)
                    if (player.Id.IsSteamId())
                        InitializePlayer(player, current);
            }
            else
            {
                IPlayer target = players.FindPlayerById(user);
                if (target != null)
                    InitializePlayer(target, current);
            }
        }

        #endregion

        #region Payout Handling

        private void Payout(IPlayer player, double amount, string message)
        {
            if (_config.AfkApi && AFKAPI != null && AFKAPI.IsLoaded)
            {
                bool isAfk = AFKAPI.Call<bool>("IsPlayerAFK", ulong.Parse(player.Id));
                if (isAfk)
                {
                    Message(player, "MissedPayout");
                    return;
                }
            }

            if (_config.Economics && Economics != null && Economics.IsLoaded)
            {
                Economics.Call("Deposit", player.Id, amount);
                Message(player, message.Replace("$payout.amount", amount.ToString()));
            }
            else if (_config.ServerRewards && ServerRewards != null && ServerRewards.IsLoaded)
            {
                ServerRewards.Call("AddPoints", player, (int)amount);
                Message(player, message.Replace("$payout.amount", amount.ToString()));
            }
        }

        private void OnUserConnected(IPlayer player)
        {
            var current = Time.realtimeSinceStartup + _config.PayoutInterval;
            InitializePlayer(player, current);

            if (_config.WelcomeBonus > 0f && !_storedData.Players[player.Id].WelcomeBonus)
            {
                Payout(player, _config.WelcomeBonus, GetLang("ReceivedWelcomeBonus", player.Id));
                _storedData.Players[player.Id].WelcomeBonus = true;
                SaveData();
            }
        }

        private void OnUserDisconnected(IPlayer player) => _payOut.Remove(player.Id);

        private void OnUserRespawn(IPlayer player)
        {
            if (!player.Id.IsSteamId()) return;

            if (!_storedData.Players.ContainsKey(player.Id))
                InitializePlayer(player, Time.realtimeSinceStartup + _config.PayoutInterval);

            double secondsAlive = (DateTime.Now - _storedData.Players[player.Id].LastTimeAlive).TotalSeconds;
            TimeSpan timeSpan = TimeSpan.FromSeconds(secondsAlive);

            double amount = (_config.BasePayout > 0)
                ? (secondsAlive / _config.BasePayout) * _config.TimeAliveMultiplier
                : 0d;

            string timeAlive = $"{timeSpan.TotalHours:00}h {timeSpan.Minutes:00}m {timeSpan.Seconds:00}s".TrimStart(' ', 'd', 'h', 'm', 's', '0');

            Payout(player, amount, GetLang("ReceivedForTimeAlive", player.Id).Replace("$time.alive", timeAlive));
            _storedData.Players[player.Id].LastTimeAlive = DateTime.Now;
        }

        #endregion

        #region Helpers

        private string GetLang(string langKey, string playerId = null) => lang.GetMessage(langKey, this, playerId);

        private void Message(IPlayer player, string textOrLang, params object[] args)
        {
            if (!player.IsConnected) return;
            string message = GetLang(textOrLang, player.Id);
            player.Reply(message != textOrLang ? message : textOrLang);
        }

        #endregion
    }
}
