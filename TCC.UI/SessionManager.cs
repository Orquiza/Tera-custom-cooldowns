﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using TCC.Data;
using TCC.ViewModels;

namespace TCC
{
    public delegate void HarrowholdModeEventHandler(bool val);

    public static class SessionManager
    {
        private static bool logged = false;
        public static bool Logged
        {
            get => logged;
            set
            {
                if(logged != value)
                {
                    logged = value;
                    WindowManager.NotifyVisibilityChanged();
                }
            }
        }
        private static bool loadingScreen = true;
        public static bool LoadingScreen
        {
            get => loadingScreen;
            set
            {
                if(loadingScreen != value)
                {
                    loadingScreen = value;
                    WindowManager.NotifyVisibilityChanged();
                }
            }
        }

        static bool harrowHoldMode = false;
        public static bool HarrowholdMode
        {
            get => harrowHoldMode;
            set
            {
                if(harrowHoldMode != value)
                {
                    harrowHoldMode = value;
                    HhModeChanged?.Invoke(harrowHoldMode);
                }
            }
        }
        public static Player CurrentPlayer = new Player();
        public static List<Character> CurrentAccountCharacters;

        public static event HarrowholdModeEventHandler HhModeChanged;


        public static void SetCombatStatus(ulong target, bool combat)
        {
            if (target == CurrentPlayer.EntityId)
            {
                if (combat)
                {
                    CurrentPlayer.IsInCombat = true;
                    CharacterWindowManager.Instance.Player.IsInCombat = true;
                }
                else
                {
                    CurrentPlayer.IsInCombat = false;
                    CharacterWindowManager.Instance.Player.IsInCombat = false;
                }
            }


        }
        public static void SetPlayerHP(ulong target, float hp)
        {
            if (target == CurrentPlayer.EntityId)
            {
                CurrentPlayer.CurrentHP = hp;
                CharacterWindowManager.Instance.Player.CurrentHP = hp;
            }
        }
        public static void SetPlayerMP(ulong target, float mp)
        {
            if (target == CurrentPlayer.EntityId)
            {
                CurrentPlayer.CurrentMP = mp;
                CharacterWindowManager.Instance.Player.CurrentMP = mp;
            }
        }
        public static void SetPlayerLaurel(Player p)
        {
            try
            {
                p.Laurel = CurrentAccountCharacters.First(x => x.Name == p.Name).Laurel;
            }
            catch
            {
                p.Laurel = Laurel.None;
            }
        }
    }

}
