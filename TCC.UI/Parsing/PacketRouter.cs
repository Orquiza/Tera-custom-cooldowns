﻿using DamageMeter.Sniffing;
using Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using TCC.Data;
using TCC.Parsing.Messages;
using TCC.ViewModels;
using Tera.Game;

namespace TCC.Parsing
{


    public static class PacketProcessor
    {
        public static uint Version;
        public static string Region;
        public static OpCodeNamer OpCodeNamer;
        public static OpCodeNamer SystemMessageNamer;
        static ConcurrentQueue<Tera.Message> Packets = new ConcurrentQueue<Tera.Message>();

        public static void Init()
        {
            TeraSniffer.Instance.MessageReceived += MessageReceived;
            TeraSniffer.Instance.NewConnection += Instance_NewConnection;
            var analysisThread = new Thread(PacketAnalysisLoop);
            analysisThread.Start();
        }

        private static void Instance_NewConnection(Server obj)
        {
            Region = obj.Region;
            var td = new TeraData(Region);
            var lang = td.GetLanguage(Region);
            EntitiesManager.CurrentDatabase = new MonsterDatabase(lang);
        }

        public static void MessageReceived(global::Tera.Message obj)
        {
            if (obj.Direction == Tera.MessageDirection.ClientToServer && obj.OpCode == 19900)
            {
                var msg = new C_CHECK_VERSION_CUSTOM(new CustomReader(obj));
                Version = msg.Versions[0];
                TeraSniffer.Instance.opn = new OpCodeNamer(System.IO.Path.Combine(BasicTeraData.Instance.ResourceDirectory, $"data/opcodes/{Version}.txt"));
                OpCodeNamer = TeraSniffer.Instance.opn;
                SystemMessageNamer = new OpCodeNamer(System.IO.Path.Combine(BasicTeraData.Instance.ResourceDirectory, $"data/opcodes/smt_{Version}.txt"));
                MessageFactory.Init();

            }
            Packets.Enqueue(obj);
        }
        private static void PacketAnalysisLoop()
        {
            while (true)
            {
                Tera.Message msg;
                var successDequeue = Packets.TryDequeue(out msg);
                if (!successDequeue)
                {
                    Thread.Sleep(1);
                    continue;
                }
                var message = MessageFactory.Create(msg);
                //PacketInspector.InspectPacket(msg);
                if (message.GetType() == typeof(Tera.Game.Messages.UnknownMessage)) continue;

                if (!MessageFactory.Process(message))
                {
                }
            }
        }

        public static void HandleNewSkillCooldown(S_START_COOLTIME_SKILL p)
        {
            SkillManager.AddSkill(p.SkillId, p.Cooldown);
        }
        public static void HandleNewItemCooldown(S_START_COOLTIME_ITEM p)
        {
            SkillManager.AddBrooch(p.ItemId, p.Cooldown);
        }
        public static void HandleDecreaseSkillCooldown(S_DECREASE_COOLTIME_SKILL p)
        {
            SkillManager.ChangeSkillCooldown(p.SkillId, (int)p.Cooldown);
        }

        public static void HandlePlayerStatUpdate(S_PLAYER_STAT_UPDATE p)
        {
            SessionManager.CurrentPlayer.MaxHP = p.maxHp;
            SessionManager.CurrentPlayer.MaxMP = p.maxMp;
            SessionManager.CurrentPlayer.MaxST = p.maxRe + p.bonusRe;
            SessionManager.CurrentPlayer.ItemLevel = p.ilvl;
            SessionManager.CurrentPlayer.CurrentST = p.currRe;
            SessionManager.CurrentPlayer.CurrentHP = p.currHp;
            SessionManager.CurrentPlayer.CurrentMP = p.currMp;

            CharacterWindowManager.Instance.Player.MaxHP = p.maxHp;
            CharacterWindowManager.Instance.Player.MaxMP = p.maxMp;
            CharacterWindowManager.Instance.Player.MaxST = p.maxRe + p.bonusRe;
            CharacterWindowManager.Instance.Player.ItemLevel = p.ilvl;
            CharacterWindowManager.Instance.Player.CurrentST = p.currRe;
            CharacterWindowManager.Instance.Player.CurrentHP = p.currHp;
            CharacterWindowManager.Instance.Player.CurrentMP = p.currMp;
        }
        public static void HandleCreatureChangeHP(S_CREATURE_CHANGE_HP p)
        {
            SessionManager.SetPlayerHP(p.target, p.currentHP);
            if(EntitiesManager.TryGetBossById(p.target, out Boss b))
            {
                if(b.Visible == System.Windows.Visibility.Collapsed)
                {
                    b.Visible = System.Windows.Visibility.Visible;
                }
                if (b.MaxHP != p.maxHP)
                {
                    b.MaxHP = p.maxHP;
                }
                //Console.WriteLine("Changing hp from {0} to {1}", b.CurrentHP, p.currentHP);
                b.CurrentHP = p.currentHP;
                //Console.WriteLine("Result: {0}", b.CurrentHP);

            }
        }
        public static void HandlePlayerChangeMP(S_PLAYER_CHANGE_MP p)
        {
            SessionManager.SetPlayerMP(p.target, p.currentMP); 
        }
        public static void HandlePlayerChangeStamina(S_PLAYER_CHANGE_STAMINA p)
        {
            SessionManager.CurrentPlayer.CurrentST = p.currentStamina;
            CharacterWindowManager.Instance.Player.CurrentST = p.currentStamina;
        }
        public static void HandlePlayerChangeFlightEnergy(S_PLAYER_CHANGE_FLIGHT_ENERGY p)
        {
            SessionManager.CurrentPlayer.FlightEnergy = p.energy;
            CharacterWindowManager.Instance.Player.FlightEnergy = p.energy;
        }
        public static void HandleUserStatusChanged(S_USER_STATUS p)
        {
            SessionManager.SetCombatStatus(p.id, p.isInCombat);
        }

        public static void HandleGageReceived(S_BOSS_GAGE_INFO p)
        {
            EntitiesManager.UpdateNPCbyGauge(p.EntityId, p.CurrentHP, p.MaxHP, (ushort)p.HuntingZoneId, (uint)p.TemplateId);
        }
        public static void HandleNpcStatusChanged(S_NPC_STATUS p)
        {
            EntitiesManager.SetNPCStatus(p.EntityId, p.IsEnraged);
            if(EntitiesManager.TryGetBossById(p.EntityId, out Boss b))
            {
                if (p.Target == 0)
                {
                    b.Target = 0;
                    b.CurrentAggroType = AggroCircle.None;
                }
            }
        }
        public static void HandleUserEffect(S_USER_EFFECT p)
        {
            if(EntitiesManager.TryGetBossById(p.Source, out Boss b))
            {
                if (p.Action == AggroAction.Remove)
                {
                    b.Target = p.User;
                    switch (p.Circle)
                    {
                        case AggroCircle.Main:
                            break;
                        case AggroCircle.Secondary:
                            b.CurrentAggroType = AggroCircle.Main;
                            break;
                        default:
                            break;
                    }
                }
                else 
                {
                    b.Target = p.User;
                    b.CurrentAggroType = p.Circle;
                }
            }
        }

        public static void HandleCharList(S_GET_USER_LIST p)
        {
            SessionManager.CurrentAccountCharacters = p.CharacterList;
        }
        public static void HandleCharLogin(S_LOGIN p)
        {
            SessionManager.CurrentPlayer.ClearAbnormalities();
            BuffBarWindowManager.Instance.Player.ClearAbnormalities();

            SessionManager.Logged = true;
            SessionManager.CurrentPlayer.Class = p.CharacterClass;
            SessionManager.CurrentPlayer.EntityId = p.entityId;
            SessionManager.CurrentPlayer.PlayerId = p.PlayerId;
            SessionManager.CurrentPlayer.ServerId = p.ServerId;
            SessionManager.CurrentPlayer.Name = p.Name;
            SessionManager.CurrentPlayer.Level = p.Level;
            SessionManager.SetPlayerLaurel(SessionManager.CurrentPlayer);

            CharacterWindowManager.Instance.Player.Class = p.CharacterClass;
            CharacterWindowManager.Instance.Player.Name = p.Name;
            CharacterWindowManager.Instance.Player.Level = p.Level;
            SessionManager.SetPlayerLaurel(CharacterWindowManager.Instance.Player);

            App.Current.Dispatcher.Invoke(() =>
            {
                WindowManager.ChangeClickThru(WindowManager.ClickThru);
            });


        }
        public static void HandleReturnToLobby(S_RETURN_TO_LOBBY p)
        {
            SessionManager.Logged = false;
            SessionManager.CurrentPlayer.ClearAbnormalities();
            BuffBarWindowManager.Instance.Player.ClearAbnormalities();
            SkillManager.Clear();
            EntitiesManager.ClearNPC();
            GroupWindowManager.Instance.ClearAll();
        }
        
        public static void HandleLoadTopo(S_LOAD_TOPO x)
        {
            SessionManager.LoadingScreen = true;
        }
        public static void HandleLoadTopoFin(C_LOAD_TOPO_FIN x)
        {
            //SessionManager.LoadingScreen = false;
        }

        internal static void HandleStartRoll(S_ASK_BIDDING_RARE_ITEM x)
        {
            GroupWindowManager.Instance.StartRoll();
        }
        internal static void HandleRollResult(S_RESULT_BIDDING_DICE_THROW x)
        {
            GroupWindowManager.Instance.SetRoll(x.EntityId, x.RollResult);
        }
        internal static void HandleEndRoll(S_RESULT_ITEM_BIDDING x)
        {
            GroupWindowManager.Instance.EndRoll();
        }

        public static void HandleSpawnMe(S_SPAWN_ME p)
        {
            //WindowManager.ShowWindow(WindowManager.CharacterWindow);
            EntitiesManager.ClearNPC();
            EntitiesManager.ClearUsers();
            System.Timers.Timer t = new System.Timers.Timer(2000);
            t.Elapsed += (s, ev) =>
            {
                SessionManager.LoadingScreen = false;
                t.Stop();
            };
            t.Enabled = true;
        }
        public static void HandleSpawnNpc(S_SPAWN_NPC p)
        {
            EntitiesManager.SpawnNPC(p.HuntingZoneId, p.TemplateId, p.EntityId, System.Windows.Visibility.Hidden, false);
            EntitiesManager.CheckHarrowholdMode(p.HuntingZoneId, p.TemplateId);
        }
        public static void HandleSpawnUser(S_SPAWN_USER p)
        {
            EntitiesManager.SpawnUser(p.EntityId, p.Name);
        }

        public static void HandlePartyMemberBuffUpdate(S_PARTY_MEMBER_BUFF_UPDATE x)
        {
            foreach (var buff in x.Abnormals)
            {
                AbnormalityManager.BeginOrRefreshPartyMemberAbnormality(x.PlayerId, x.ServerId, buff.Id, buff.Duration, buff.Stacks);
            }
        }
        public static void HandlePartyMemberAbnormalAdd(S_PARTY_MEMBER_ABNORMAL_ADD x)
        {
            AbnormalityManager.BeginOrRefreshPartyMemberAbnormality(x.PlayerId, x.ServerId, x.Id, x.Duration, x.Stacks);
        }
        public static void HandlePartyMemberAbnormalDel(S_PARTY_MEMBER_ABNORMAL_DEL x)
        {
            AbnormalityManager.EndPartyMemberAbnormality(x.PlayerId, x.ServerId, x.Id);
        }

        internal static void HandleChangeLeader(S_CHANGE_PARTY_MANAGER x)
        {
            GroupWindowManager.Instance.SetNewLeader(x.EntityId, x.Name);
        }

        public static void HandlePartyMemberAbnormalClear(S_PARTY_MEMBER_ABNORMAL_CLEAR x)
        {
            GroupWindowManager.Instance.ClearUserAbnormality(x.PlayerId, x.ServerId);
        }
        public static void HandlePartyMemberAbnormalRefresh(S_PARTY_MEMBER_ABNORMAL_REFRESH x)
        {
            AbnormalityManager.BeginOrRefreshPartyMemberAbnormality(x.PlayerId, x.ServerId, x.Id, x.Duration, x.Stacks);
        }

        public static void HandleDespawnNpc(S_DESPAWN_NPC p)
        {
            EntitiesManager.DespawnNPC(p.target);
        }
        public static void HandleDespawnUser(S_DESPAWN_USER p)
        {
            EntitiesManager.DespawnUser(p.EntityId);
        }

        public static void HandleAbnormalityBegin(S_ABNORMALITY_BEGIN p)
        {
            AbnormalityManager.BeginAbnormality(p.id, p.targetId, p.duration, p.stacks);

            switch (SessionManager.CurrentPlayer.Class)
            {
                case Class.Elementalist:
                    Mystic.CheckHurricane(p);
                    break;
                default:
                    break;
            }                   
        }
        public static void HandleAbnormalityRefresh(S_ABNORMALITY_REFRESH p)
        {
            AbnormalityManager.BeginAbnormality(p.AbnormalityId, p.TargetId, p.Duration, p.Stacks);
        }
        public static void HandleAbnormalityEnd(S_ABNORMALITY_END p)
        {
            AbnormalityManager.EndAbnormality(p.target, p.id);
        }

        public static void HandlePlayerLocation(C_PLAYER_LOCATION p)
        {
            if (SessionManager.HarrowholdMode)
            {
                EntitiesManager.CheckCurrentDragon(new System.Windows.Point(p.X, p.Y));
            }
        }

        public static void HandlePartyMemberList(S_PARTY_MEMBER_LIST p)
        {
            foreach (var user in p.Members)
            {
                GroupWindowManager.Instance.AddOrUpdateMember(user);
                System.Threading.Tasks.Task.Delay(500).ContinueWith(t => { });
            }
        }
        public static void HandlePartyMemberLeave(S_LEAVE_PARTY_MEMBER p)
        {
            GroupWindowManager.Instance.RemoveMember(p.PlayerId, p.ServerId);
        }
        public static void HandlePartyMemberLogout(S_LOGOUT_PARTY_MEMBER p)
        {
            GroupWindowManager.Instance.LogoutMember(p.PlayerId, p.ServerId);
            GroupWindowManager.Instance.ClearUserAbnormality(p.PlayerId, p.ServerId);

        }
        public static void HandlePartyMemberKick(S_BAN_PARTY_MEMBER p)
        {
            GroupWindowManager.Instance.RemoveMember(p.PlayerId, p.ServerId);
        }
        public static void HandlePartyMemberHP(S_PARTY_MEMBER_CHANGE_HP p)
        {
            GroupWindowManager.Instance.UpdateMemberHP(p.PlayerId, p.ServerId, p.CurrentHP, p.MaxHP);
        }
        public static void HandlePartyMemberMP(S_PARTY_MEMBER_CHANGE_MP p)
        {
            GroupWindowManager.Instance.UpdateMemberMP(p.PlayerId, p.ServerId, p.CurrentMP, p.MaxMP);
        }
        public static void HandlePartyMemberStats(S_PARTY_MEMBER_STAT_UPDATE p)
        {
            GroupWindowManager.Instance.UpdateMember(p);
        }
        public static void HandleLeaveParty(S_LEAVE_PARTY x)
        {
            GroupWindowManager.Instance.ClearAll();
        }

        public static void HandleReadyCheck(S_CHECK_TO_READY_PARTY p)
        {
            foreach (var item in p.Party)
            {
                GroupWindowManager.Instance.SetReadyStatus(item);
            }
        }
        public static void HandleReadyCheckFin(S_CHECK_TO_READY_PARTY_FIN x)
        {
            GroupWindowManager.Instance.EndReadyCheck();
        }

        //for lfg, not used
        public static void HandlePartyMemberInfo(S_PARTY_MEMBER_INFO p)
        {
            foreach (var user in p.Members)
            {
                //GroupWindowManager.Instance.AddOrUpdateMember(user);
                //System.Threading.Tasks.Task.Delay(500).ContinueWith(t => { });
            }
        }

        //public static void Debug(bool x)
        //{
        //    SessionManager.TryGetBossById(10, out Boss b);
        //    if (x)
        //    {
        //        b.CurrentHP = b.MaxHP/2;
        //    }
        //    else
        //    {
        //        b.CurrentHP = b.MaxHP;
        //    }

        //}
        //public static void DebugEnrage(bool e)
        //{
        //    SessionManager.TryGetBossById(10, out Boss b);

        //    b.Enraged = e;
        //    //EnragedChanged?.Invoke(10, e);

        //}

    }

}
