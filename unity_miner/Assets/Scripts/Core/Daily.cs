using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Misiones diarias, pase, premio diario, tienda y extras (boost, cofre gratis, alcancia, cascos).</summary>
    public sealed partial class GameState
    {
        void CheckDailyReset()
        {
            string today = Today();
            if (DailyDate != today)
            {
                DailyDate = today;
                Daily = new Dictionary<string, int>();
                Claimed = new List<string>();
            }
        }

        public void AddDaily(string statName, int n)
        {
            CheckDailyReset();
            int v;
            Daily.TryGetValue(statName, out v);
            Daily[statName] = v + n;
        }

        public int MissionProgress(Mission m)
        {
            int v;
            Daily.TryGetValue(m.Stat, out v);
            return Math.Min(v, m.Goal);
        }

        public bool MissionReady(Mission m) { return MissionProgress(m) >= m.Goal && !Claimed.Contains(m.Id); }

        public int MissionsReadyCount()
        {
            int n = 0;
            foreach (Mission m in Content.Missions)
                if (MissionReady(m)) n++;
            return n;
        }

        public void ClaimMission(Mission m)
        {
            if (!MissionReady(m)) return;
            Claimed.Add(m.Id);
            AddGems(m.Gems);
            AddStat("missions");
            AddPassXp(25);
            SaveGame();
        }

        public int PassLevel() { return PassXp / 100 + 1; }

        public int PassReward(int level) { return 40 + level * 20; }

        public void AddPassXp(int n)
        {
            int before = PassLevel();
            PassXp += n;
            int after = PassLevel();
            for (int l = before + 1; l <= after; l++)
            {
                AddGems(PassReward(l));
                EmitToast("Pase nivel " + l + ": +" + PassReward(l) + " gemas");
            }
            Changed?.Invoke();
        }

        public bool LoginAvailable() { return LoginDate != Today(); }

        /// <summary>Premio diario: devuelve las gemas (0 si ya se reclamo hoy).</summary>
        public int ClaimLogin()
        {
            if (!LoginAvailable()) return 0;
            int g = Content.DailyGems[LoginDay % 7];
            LoginDay += 1;
            LoginDate = Today();
            AddGems(g);
            SaveGame();
            return g;
        }

        // ---------------------------------------------------------------- tienda / extras
        public bool ActivateBoost()
        {
            double now = Now();
            if (now < BoostCdUntil) return false;
            BoostUntil = now + Balance.BoostTime;
            BoostCdUntil = now + Balance.BoostCd;
            AddStat("boosts");
            Changed?.Invoke();
            return true;
        }

        public bool FreeChestReady() { return Now() >= FreeChestAt; }

        public void ClaimFreeChest()
        {
            if (!FreeChestReady()) return;
            FreeChestAt = Now() + 4 * 3600;
            AddGems(30);
            SaveGame();
        }

        public int CollectPiggy()
        {
            int n = Piggy;
            Piggy = 0;
            AddGems(n);
            return n;
        }

        public bool BuySkin(int i)
        {
            if (SkinsOwned.Contains(i))
            {
                Skin = i;
                Changed?.Invoke();
                return true;
            }
            if (i < 0 || i >= Content.Skins.Length) return false;
            int c = Content.Skins[i].Cost;
            if (Gems < c)
            {
                EmitToast("Faltan gemas");
                return false;
            }
            Gems -= c;
            SkinsOwned.Add(i);
            Skin = i;
            Changed?.Invoke();
            SaveGame();
            return true;
        }

        public bool SpendGems(int n)
        {
            if (Gems < n)
            {
                EmitToast("Faltan gemas");
                return false;
            }
            Gems -= n;
            Changed?.Invoke();
            return true;
        }
    }
}
