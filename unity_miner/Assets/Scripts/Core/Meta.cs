using System;
using System.Collections.Generic;

namespace Mineros.Core
{
    /// <summary>Meta-progresion: renacer + Esencia, desbloqueos, rastreador de metas y logros.</summary>
    public sealed partial class GameState
    {
        // ---------------------------------------------------------------- renacer + esencia
        public bool CanRebirth() { return RunMax >= 2 * Balance.SubsPerWorld; }

        /// <summary>Esencia que daria renacer ahora (segun la etapa maxima de esta partida).</summary>
        public int EssenceGainPreview()
        {
            return (int)Math.Floor(Balance.EssenceK * Math.Pow((double)Math.Max(RunMax, 0), Balance.EssenceP) + 1e-6);
        }

        /// <summary>Compat. con la UI vieja: la Esencia que se ganaria (como double).</summary>
        public double RebirthGain() { return (double)EssenceGainPreview(); }

        /// <summary>Etapa (indice) en la que se empieza tras renacer.</summary>
        public int StartStage() { return EssenceLevel("start"); }

        public void DoRebirth()
        {
            if (!CanRebirth()) return;
            int gain = EssenceGainPreview();
            Essence += gain;
            Rebirths += 1;
            Gold = 0.0;
            Lv = NewLevels();
            int st = StartStage();
            World = st / Balance.SubsPerWorld + 1;
            Sub = st % Balance.SubsPerWorld + 1;
            Progress = 0.0;
            NeedBoss = false;
            RunMax = st;
            AddDaily("rebirths", 1);
            Rebirthed?.Invoke(gain);
            CheckUnlocks();
            Changed?.Invoke();
            StatsChanged?.Invoke();
            StageCleared?.Invoke(0, 0);
            SaveGame();
        }

        public bool EssenceMaxed(string id)
        {
            EssenceNode n = Content.FindNode(id);
            return n == null || EssenceLevel(id) >= n.Max;
        }

        /// <summary>Costo del proximo nivel del nodo (0 si ya esta al maximo o no existe).</summary>
        public int EssenceCost(string id)
        {
            EssenceNode n = Content.FindNode(id);
            if (n == null || EssenceMaxed(id)) return 0;
            return (int)Math.Ceiling((double)n.BaseCost * Math.Pow(n.CostMult, EssenceLevel(id)) - 1e-9);
        }

        public bool BuyEssence(string id)
        {
            if (EssenceMaxed(id)) return false;
            int c = EssenceCost(id);
            if (Essence < c)
            {
                EmitToast("Falta Esencia");
                return false;
            }
            Essence -= c;
            EssenceLv[id] = EssenceLevel(id) + 1;
            Changed?.Invoke();
            StatsChanged?.Invoke();
            SaveGame();
            return true;
        }

        public double OfflineCap() { return Balance.OfflineCap + 3600.0 * EssenceLevel("night"); }

        public double OfflineEff() { return Balance.OfflineEff + 0.1 * EssenceLevel("night"); }

        public void CollectOffline(double mult = 1.0)
        {
            AddGold(PendingOffline * mult);
            PendingOffline = 0.0;
            PendingOfflineTime = 0.0;
        }

        // ---------------------------------------------------------------- desbloqueos
        void SyncBest()
        {
            foreach (string k in Content.UpgradeKinds)
                if (Lv[k] > LvBestOf(k)) LvBest[k] = Lv[k];
        }

        bool FeatureReached(string f)
        {
            switch (f)
            {
                case "speed": return LvBest["power"] >= 3 || LvBest["speed"] > 1;
                case "money": return MaxStage >= 1;
                case "boost": return MaxStage >= 2;
                case "tools": return MaxStage >= 3;
                case "missions": return MaxStage >= 4;
                case "play": return MaxStage >= Balance.SubsPerWorld;
                case "shop":
                case "skin":
                case "piggy": return MaxStage >= Balance.SubsPerWorld + 1;
                case "auto": return LvBest["power"] >= 10 && LvBest["speed"] >= 10 && LvBest["money"] >= 10;
                case "rebirth": return MaxStage >= 2 * Balance.SubsPerWorld || Rebirths > 0;
                case "achievements": return AchClaimed.Count > 0 || AchievementsReadyCount() > 0;
            }
            return false;
        }

        /// <summary>silent: marca tambien como vista (migracion / debug), sin animaciones.</summary>
        internal void CheckUnlocks(bool silent = false)
        {
            foreach (string f in Content.Features)
            {
                if (FeaturesUnlocked.Contains(f)) continue;
                if (FeatureReached(f))
                {
                    FeaturesUnlocked.Add(f);
                    if (silent) FeaturesSeen.Add(f);
                    else FeatureUnlocked?.Invoke(f);
                }
            }
        }

        public bool IsUnlocked(string feature)
        {
            if (Array.IndexOf(Content.Features, feature) < 0) return true;   // nucleo: power, world, goals...
            return FeaturesUnlocked.Contains(feature);
        }

        /// <summary>Funciones desbloqueadas aun no vistas por la UI.</summary>
        public List<string> NewlyUnlocked()
        {
            List<string> o = new List<string>();
            foreach (string f in FeaturesUnlocked)
                if (!FeaturesSeen.Contains(f)) o.Add(f);
            return o;
        }

        public void MarkFeatureSeen(string feature)
        {
            if (!FeaturesSeen.Contains(feature)) FeaturesSeen.Add(feature);
        }

        /// <summary>Depuracion/capturas: desbloquea todo y lo marca visto.</summary>
        public void UnlockAll()
        {
            foreach (string f in Content.Features)
            {
                if (!FeaturesUnlocked.Contains(f)) FeaturesUnlocked.Add(f);
                MarkFeatureSeen(f);
            }
        }

        // ---------------------------------------------------------------- metas
        GoalDef GetGoalDef(int idx)
        {
            if (idx < Content.Goals.Length) return Content.Goals[idx];
            int n = idx - Content.Goals.Length;
            int gm = Math.Min(40 + n * 2, 100);
            if (n % 2 == 0)
            {
                int t = GoalGenTarget;
                return new GoalDef("gen_" + idx, "Llega a la Etapa " + StageLabel(t), "star", GoalType.Stage, "", t, gm);
            }
            return new GoalDef("gen_" + idx, "Mejora " + Content.KindName(GoalGenKind) + " a nivel " + GoalGenTarget,
                GoalGenKind, GoalType.Level, GoalGenKind, GoalGenTarget, gm);
        }

        long GoalValue(GoalDef g)
        {
            switch (g.Type)
            {
                case GoalType.Level:
                    return Math.Max(LvBestOf(g.Arg), Lv.ContainsKey(g.Arg) ? Lv[g.Arg] : 1);
                case GoalType.Stage: return MaxStage;
                case GoalType.Equipped: return EquippedCount();
                case GoalType.Collection: return Collection.Count;
            }
            return NumUtil.ToLongClamped(StatF(g.Arg) - GoalBase);
        }

        /// <summary>Prepara la meta actual: guarda la base de los contadores y fija el objetivo de las generadas.</summary>
        internal void ActivateGoal()
        {
            if (GoalIdx >= Content.Goals.Length)
            {
                int n = GoalIdx - Content.Goals.Length;
                if (n % 2 == 0)
                {
                    GoalGenTarget = MaxStage + 2 + n / 8;
                }
                else
                {
                    string k = Content.UpgradeKinds[(n / 2) % 3];
                    if (k == "speed" && LvBest["speed"] >= Balance.SpeedCap - 5) k = "power";
                    GoalGenKind = k;
                    GoalGenTarget = Math.Min(LvBest[k] + 10, k == "speed" ? Balance.SpeedCap : 100000);
                }
            }
            GoalDef g = GetGoalDef(GoalIdx);
            GoalBase = g.Type == GoalType.Stat ? StatF(g.Arg) : 0.0;
            goalNotified = false;
        }

        public Goal CurrentGoal()
        {
            GoalDef g = GetGoalDef(GoalIdx);
            long v = GoalValue(g);
            long tgt = g.Target;
            Goal cg = new Goal();
            cg.Id = g.Id;
            cg.Text = g.Text;
            cg.Icon = g.Icon;
            cg.Progress = Math.Min(v, tgt);
            cg.Target = tgt;
            cg.Gems = g.Gems;
            cg.Done = v >= tgt;
            return cg;
        }

        internal void PollGoal()
        {
            if (goalNotified) return;
            Goal cg = CurrentGoal();
            if (cg.Done)
            {
                goalNotified = true;
                GoalCompleted?.Invoke(cg);
            }
        }

        /// <summary>Reclama la meta actual si esta cumplida; devuelve las gemas (0 si no).</summary>
        public int ClaimGoal()
        {
            Goal cg = CurrentGoal();
            if (!cg.Done) return 0;
            int g = cg.Gems;
            GoalIdx += 1;
            ActivateGoal();
            Stats["goals"] = StatRaw("goals") + 1;
            AddGems(g);
            CheckUnlocks();
            StatsChanged?.Invoke();
            SaveGame();
            return g;
        }

        /// <summary>Migracion: salta (con sus gemas) las metas de la cadena ya cumplidas por una partida vieja.</summary>
        void MigrateGoals()
        {
            GoalIdx = 0;
            GoalBase = 0.0;
            while (GoalIdx < Content.Goals.Length)
            {
                GoalDef g = Content.Goals[GoalIdx];
                if (GoalValue(g) < g.Target) break;
                Gems += g.Gems;
                GoalIdx += 1;
            }
            ActivateGoal();
            goalNotified = true;
        }

        // ---------------------------------------------------------------- logros
        public long AchievementProgress(Achievement a) { return Math.Min(Stat(a.Stat), a.Goal); }

        public bool AchievementReady(Achievement a) { return Stat(a.Stat) >= a.Goal && !AchClaimed.Contains(a.Id); }

        public int AchievementsReadyCount()
        {
            int n = 0;
            foreach (Achievement a in Content.Achievements)
                if (AchievementReady(a)) n++;
            return n;
        }

        /// <summary>Reclama un logro; devuelve las gemas (0 si no esta listo o no existe).</summary>
        public int ClaimAchievement(string id)
        {
            foreach (Achievement a in Content.Achievements)
            {
                if (a.Id != id) continue;
                if (!AchievementReady(a)) return 0;
                AchClaimed.Add(id);
                int g = a.Gems;
                AddGems(g);
                Stats["ach"] = StatRaw("ach") + 1;
                CheckUnlocks();
                StatsChanged?.Invoke();
                SaveGame();
                return g;
            }
            return 0;
        }
    }
}
