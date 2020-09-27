﻿using Assets.Scripts.Characters.Titan;
using Assets.Scripts.Characters.Titan.Attacks;
using Assets.Scripts.Gamemode.Options;
using Assets.Scripts.Gamemode.Settings;
using Assets.Scripts.UI.Input;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MonoBehaviour = Photon.MonoBehaviour;
using Random = UnityEngine.Random;
using HUD = Assets.Scripts.UI.InGame.HUD;

namespace Assets.Scripts.Gamemode
{
    public abstract class GamemodeBase : MonoBehaviour
    {
        public HUD.HUD InGameHUD; 

        protected float HUDRefreshTime = 1f;
        protected float RefreshCountdown = 0;
        public bool needChooseSide;
        public float gameEndCD;
        public float gameEndTotalCDtime;

        public bool isWinning;
        public bool isLosing;

        public abstract GamemodeSettings Settings { get; set; }
        private MindlessTitanType GetTitanType()
        {
            if (Settings.CustomTitanRatio)
            {
                var titanTypes = new Dictionary<MindlessTitanType, float>(Settings.TitanTypeRatio);
                foreach (var disabledTitanType in Settings.DisabledTitans)
                {
                    titanTypes.Remove(disabledTitanType);
                }

                var totalRatio = Settings.TitanTypeRatio.Values.Sum();
                var ratioList = new List<KeyValuePair<MindlessTitanType, float>>();
                var ratio = 0f;
                foreach (var titanTypeRatio in titanTypes)
                {
                    ratio += titanTypeRatio.Value / totalRatio;
                    ratioList.Add(new KeyValuePair<MindlessTitanType, float>(titanTypeRatio.Key, ratio));
                }

                var randomNumber = Random.Range(0f, 1f);
                foreach (var titanTypeRatio in ratioList)
                {
                    if (randomNumber < titanTypeRatio.Value)
                    {
                        return titanTypeRatio.Key;
                    }
                }

            }

            var types = Enum.GetValues(typeof(MindlessTitanType));
            return (MindlessTitanType) types.GetValue(Random.Range(0, types.Length));
        }

        private int GetTitanHealth(float titanSize)
        {
            switch (Settings.TitanHealthMode)
            {
                case TitanHealthMode.Fixed:
                    return Random.Range(Settings.TitanHealthMinimum, Settings.TitanHealthMaximum + 1);
                case TitanHealthMode.Scaled:
                    return Mathf.Clamp(Mathf.RoundToInt(titanSize / 4f * Random.Range(Settings.TitanHealthMinimum, Settings.TitanHealthMaximum + 1)), Settings.TitanHealthMinimum, Settings.TitanHealthMaximum);
                case TitanHealthMode.Disabled:
                    return 0;
                default:
                    throw new ArgumentOutOfRangeException($"Invalid TitanHealthMode enum: {Settings.TitanHealthMode}");
            }
        }

        public virtual TitanConfiguration GetPlayerTitanConfiguration()
        {
            var configuration = GetTitanConfiguration();
            if (configuration.Type == MindlessTitanType.Crawler)
            {
                configuration.Attacks = new List<Attack>();
                return configuration;
            }

            configuration.Attacks = new List<Attack>
            {
                new KickAttack(), new SlapAttack(), new SlapFaceAttack(),
                new BiteAttack(), new BodySlamAttack(), new GrabAttack()
            };
            return configuration;
        }

        public virtual TitanConfiguration GetTitanConfiguration()
        {
            return GetTitanConfiguration(GetTitanType());
        }

        public virtual TitanConfiguration GetTitanConfiguration(MindlessTitanType type)
        {
            var size = Settings.TitanCustomSize ? Random.Range(Settings.TitanMinimumSize, Settings.TitanMaximumSize) : Random.Range(0.7f, 3f);
            var health = GetTitanHealth(size);
            return new TitanConfiguration(health, 10, 100, 150f, size, type);
        }

        public virtual void OnPlayerKilled(int id)
        {
            if (IsAllPlayersDead())
                this.GameLose();
        }

        public virtual void OnRestart()
        {
            if (Settings.PointMode > 0)
            {
                var propertiesToSet = new ExitGames.Client.Photon.Hashtable() {
                        { PhotonPlayerProperty.kills, 0},
                        { PhotonPlayerProperty.deaths, 0},
                        { PhotonPlayerProperty.max_dmg, 0},
                        { PhotonPlayerProperty.total_dmg, 0}
                    };
                foreach (var player in PhotonNetwork.playerList)
                    player.SetCustomProperties(propertiesToSet);
            }
            this.gameEndCD = 0f;
            this.isWinning = false;
            this.isLosing = false;
        }

        public virtual void OnUpdate(float interval) { }

        public static GamemodeSettings ConvertToGamemode(string json, GamemodeType type)
        {
            switch (type)
            {
                case GamemodeType.Racing:
                    return JsonConvert.DeserializeObject<RacingSettings>(json);
                case GamemodeType.Capture:
                    return JsonConvert.DeserializeObject<CaptureGamemodeSettings>(json);
                case GamemodeType.Titans:
                    return JsonConvert.DeserializeObject<KillTitansSettings>(json);
                case GamemodeType.Endless:
                    return JsonConvert.DeserializeObject<EndlessSettings>(json);
                case GamemodeType.Wave:
                    return JsonConvert.DeserializeObject<WaveGamemodeSettings>(json);
                case GamemodeType.Trost:
                    return JsonConvert.DeserializeObject<TrostSettings>(json);
                case GamemodeType.TitanRush:
                    return JsonConvert.DeserializeObject<RushSettings>(json);
                case GamemodeType.PvpAhss:
                    return JsonConvert.DeserializeObject<PvPAhssSettings>(json);
                case GamemodeType.Infection:
                    return JsonConvert.DeserializeObject<InfectionGamemodeSettings>(json);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public virtual void OnPlayerSpawned(GameObject player)
        {
        }

        protected void SpawnTitans(int amount)
        {
            SpawnTitans(amount, GetTitanConfiguration);
        }

        protected void SpawnTitans(int amount, Func<TitanConfiguration> titanConfiguration)
        {
            StartCoroutine(SpawnTitan(amount, titanConfiguration));
        }

        private IEnumerator SpawnTitan(int amount, Func<TitanConfiguration> titanConfiguration)
        {
            var spawns = GameObject.FindGameObjectsWithTag("titanRespawn");
            for (var i = 0; i < amount; i++)
            {
                if (FengGameManagerMKII.instance.getTitans().Count >= Settings.TitanLimit) break;
                var randomSpawn = spawns[Random.Range(0, spawns.Length)];
                FengGameManagerMKII.instance.SpawnTitan(randomSpawn.transform.position, randomSpawn.transform.rotation, titanConfiguration.Invoke());
                yield return new WaitForEndOfFrame();
            }
        }

        public virtual void OnTitanSpawned(MindlessTitan titan)
        {
        }

        public virtual GameObject GetPlayerSpawnLocation(string tag = "playerRespawn")
        {
            var objArray = GameObject.FindGameObjectsWithTag(tag);
            return objArray[Random.Range(0, objArray.Length)];
        }

        public virtual string GetVictoryMessage(float timeUntilRestart, float totalServerTime = 0f)
        {
            if (PhotonNetwork.offlineMode)
            {
                return $"Humanity Win!\n Press {InputManager.GetKey(InputUi.Restart)} to Restart.\n\n\n";
            }
            return "Humanity Win!\nGame Restart in " + ((int)timeUntilRestart) + "s\n\n";
        }

        public virtual void OnTitanKilled(string titanName)
        {
            if (Settings.RestartOnTitansKilled && IsAllTitansDead())
            {
                OnAllTitansDead();
            }
        }

        //would be better to get float value for time and totalRoomTime instead as those are float value to begin with so this method won't have any loss of infos
        public virtual string GetGamemodeStatusTop()
        {
            var content = new System.Text.StringBuilder("Titan Left: ",100);
            content.Append(GameObject.FindGameObjectsWithTag("titan").Length);
            content.Append("  Time : ");
            content.Append((PhotonNetwork.offlineMode ? FengGameManagerMKII.instance.timeTotalServer : FengGameManagerMKII.instance.deltaRoomTime).ToString("f0"));
            return content.ToString();
        }

        public virtual string GetGamemodeStatusTopRight()
        {
            return string.Concat("Humanity ", Settings.HumanScore, " : Titan ", Settings.TitanScore, " ");

        }

        public virtual string GetRoundEndedMessage()
        {
            return $"Humanity {Settings.HumanScore} : Titan {Settings.TitanScore}";
        }

        public virtual void OnAllTitansDead() { }

        public virtual void OnLevelLoaded(Level level, bool isMasterClient = false)
        {
            if (!Settings.Supply)
            {
                UnityEngine.Object.Destroy(GameObject.Find("aot_supply"));
            }

            if (Settings.LavaMode)
            {
                UnityEngine.Object.Instantiate(Resources.Load("levelBottom"), new Vector3(0f, -29.5f, 0f), Quaternion.Euler(0f, 0f, 0f));
                var lavaSupplyStation = GameObject.Find("aot_supply_lava_position");
                var supplyStation = GameObject.Find("aot_supply");
                if (lavaSupplyStation == null || supplyStation == null) return;
                supplyStation.transform.position = lavaSupplyStation.transform.position;
                supplyStation.transform.rotation = lavaSupplyStation.transform.rotation;
            }
        }

        public virtual void OnGameWon()
        {
            Settings.HumanScore++;
            FengGameManagerMKII.instance.gameEndCD = FengGameManagerMKII.instance.gameEndTotalCDtime;
            var parameters = new object[] { Settings.HumanScore };
            FengGameManagerMKII.instance.photonView.RPC("netGameWin", PhotonTargets.Others, parameters);
            if (Settings.ChatFeed)
            {
                //this.chatRoom.addLINE("<color=#FFC000>(" + this.roundTime.ToString("F2") + ")</color> Round ended (game win).");
            }
        }

        public virtual void OnGameLost()
        {
            Settings.TitanScore++;
            var parameters = new object[] { Settings.TitanScore };
            FengGameManagerMKII.instance.photonView.RPC("netGameLose", PhotonTargets.Others, parameters);
            if ((int) FengGameManagerMKII.settings[0xf4] == 1)
            {
                //FengGameManagerMKII.instance.chatRoom.addLINE("<color=#FFC000>(" + this.roundTime.ToString("F2") + ")</color> Round ended (game lose).");
            }
        }
        
        public virtual void OnNetGameLost(int score)
        {
            Settings.TitanScore = score;
        }

        public virtual void OnNetGameWon(int score)
        {
            Settings.HumanScore = score;
        }

        internal bool IsAllPlayersDead()
        {
            var num = 0;
            var num2 = 0;
            foreach (var player in PhotonNetwork.playerList)
            {
                if (RCextensions.returnIntFromObject(player.CustomProperties[PhotonPlayerProperty.isTitan]) != 1) continue;
                num++;
                if (RCextensions.returnBoolFromObject(player.CustomProperties[PhotonPlayerProperty.dead]))
                {
                    num2++;
                }
            }
            return (num == num2);
        }

        internal bool IsAllTitansDead()
        {
            foreach (GameObject obj2 in GameObject.FindGameObjectsWithTag("titan"))
            {
                if ((obj2.GetComponent<MindlessTitan>() != null) && obj2.GetComponent<MindlessTitan>().TitanState != MindlessTitanState.Dead)
                {
                    return false;
                }
                if (obj2.GetComponent<FEMALE_TITAN>() != null)
                {
                    return false;
                }
            }
            return true;
        }

        public virtual string GetDefeatMessage(float gameEndCd)
        {
            if (PhotonNetwork.offlineMode)
            {
                return $"Humanity Fail!\n Press {InputManager.GetKey(InputUi.Restart)} to Restart.\n\n\n";
            }
            return "Humanity Fail!\nAgain!\nGame Restart in " + ((int) gameEndCd) + "s\n\n";
        }
    
        public virtual void CoreUpdate()
        {
            RefreshCountdown -= Time.deltaTime;
            if (RefreshCountdown < 0)
                RefreshCountdown = this.HUDRefreshTime;
            else
                return;

            InGameHUD.ShowHUDInfo(HUD.LabelPosition.TopCenter, GetGamemodeStatusTop() + (Settings.TeamMode != TeamMode.Disabled ? $"\n<color=#00ffff>Cyan: {FengGameManagerMKII.instance.cyanKills}</color><color=#ff00ff>       Magenta: {FengGameManagerMKII.instance.magentaKills}</color>" : ""));
            InGameHUD.ShowHUDInfo(HUD.LabelPosition.TopRight, GetGamemodeStatusTopRight());
            string str4 = (IN_GAME_MAIN_CAMERA.difficulty >= 0)
                ? ((IN_GAME_MAIN_CAMERA.difficulty != 0)
                    ? ((IN_GAME_MAIN_CAMERA.difficulty != 1) ? "Abnormal" : "Hard")
                    : "Normal")
                : "Trainning";
            //this.ShowHUDInfoTopRightMAPNAME("\n" + Level.Name + " : " + str4);
            //not used yet though i think will be added later so i don't remove it
            //if (!PhotonNetwork.offlineMode)
            //{
            //    string roomName = PhotonNetwork.room.Name.Substring(0, Math.Min(20, PhotonNetwork.room.Name.IndexOf('`')));
            //}
            //this.ShowHUDInfoTopRightMAPNAME("\n" + str5 + " [FFC000](" +
            //                                Convert.ToString(PhotonNetwork.room.playerCount) + "/" +
            //                                Convert.ToString(PhotonNetwork.room.maxPlayers) + ")");

            if (this.needChooseSide)
            {
                InGameHUD.ShowHUDInfo(HUD.LabelPosition.TopCenter, "\n\nPRESS 1 TO ENTER GAME", true);
            }
        }

        private void NetGameEnd(PhotonPlayer sender)
        {
            this.gameEndCD = this.gameEndTotalCDtime;
            if (Settings.ChatFeed)
            {
                this.InGameHUD.Chat.AddMessage("<color=#FFC000>(" + FengGameManagerMKII.instance.roundTime.ToString("F2") + ")</color> Round ended (game win).");
            }
            if (!(sender.IsMasterClient || sender.isLocal))
            {
                this.InGameHUD.Chat.AddMessage("<color=#FFC000>Round end sent from Player " + sender.ID + "</color>");
            }
        }

        [PunRPC]
        private void netGameWin(int score, PhotonMessageInfo info)
        {
            this.isWinning = true;
            this.OnNetGameWon(score);
            this.NetGameEnd(info.sender);
        }

        [PunRPC]
        private void netGameLose(int score, PhotonMessageInfo info)
        {
            this.isLosing = true;
            this.OnNetGameLost(score);
            this.NetGameEnd(info.sender);
        }

        protected void RestartGameCD()
        {
            if (this.gameEndCD <= 0f)
            {
                this.gameEndCD = 0f;
                if (PhotonNetwork.isMasterClient)
                {
                    FengGameManagerMKII.instance.RestartGame();
                }
                InGameHUD.ShowHUDInfo(HUD.LabelPosition.Center, string.Empty);
            }
            else
            {
                this.gameEndCD -= Time.deltaTime;
            }
        }

        [Obsolete("will have to be a restart class on its own, as it doesn't require to run check all the time but can be event triggered to run just once and than update the timer once active")]
        public virtual void CoreRestartCheck()
        {
            if (this.isWinning)
                InGameHUD.ShowHUDInfo(HUD.LabelPosition.Center, this.GetVictoryMessage(gameEndCD, FengGameManagerMKII.instance.timeTotalServer));
            else if (this.isLosing)
                InGameHUD.ShowHUDInfo(HUD.LabelPosition.Center, GetDefeatMessage(gameEndCD));
            else
                return;

            RestartGameCD();
        }

        public void GameLose()
        {
            if (!(this.isWinning || this.isLosing))
            {
                EventManager.OnGameLost.Invoke();
                this.isLosing = true;
                this.gameEndCD = this.gameEndTotalCDtime;
            }
        }

        public void GameWin()
        {
            if (!this.isLosing && !this.isWinning)
            {
                EventManager.OnGameWon.Invoke();
                this.isWinning = true;
            }
        }
    }
}
