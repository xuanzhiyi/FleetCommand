using System;
using System.Collections.Generic;

namespace FleetCommand
{
    /// <summary>
    /// Tracks research upgrade levels (0-3) for each ship type and manages
    /// the active research queue. Upgrades boost hull (MaxHP), speed, and damage.
    /// </summary>
    public class ResearchManager
    {
        // Current upgrade level per ship type (0 = none, 1 = Mk.I, 2 = Mk.II, 3 = Mk.III)
        public Dictionary<ShipType, int> Levels { get; } = new Dictionary<ShipType, int>();

        // Currently researching (null = idle)
        public ResearchOrder ActiveOrder { get; private set; }

        private const float HullBonusPerLevel   = 0.20f;  // +20% hull per level
        private const float SpeedBonusPerLevel  = 0.12f;  // +12% speed per level (with friction)
        private const float DamageBonusPerLevel = 0.10f;  // +10% damage per level

        // Research costs: [shipTypeIndex][level-1]
        private static readonly int[,] ResearchCosts =
        {
            //  Mk.I   Mk.II   Mk.III
            {   0,     0,      0    },   // Mothership (no research)
            { 200,   500,    800   },   // Miner
            { 300,   700,   1000   },   // Interceptor
            { 300,   700,   1000   },   // Bomber
            { 400,   900,   1200   },   // Corvet
            { 600,  1200,   1500   },   // Frigate
            { 900,  2000,   2500   },   // Destroyer
            {1500,  2500,   5000   },   // Battlecruiser
        };

        // Research duration in ms: [shipTypeIndex][level-1]
        private static readonly int[,] ResearchTimes =
        {
            {     0,      0,      0 },  // Mothership
            { 10000,  20000,  30000 },  // Miner
            { 12000,  25000,  35000 },  // Interceptor
            { 12000,  25000,  35000 },  // Bomber
            { 15000,  30000,  45000 },  // Corvet
            { 20000,  40000,  60000 },  // Frigate
            { 30000,  60000,  90000 },  // Destroyer
            { 40000,  70000,  10000 },  // Battlecruiser
        };

        public ResearchManager()
        {
            foreach (ShipType st in Enum.GetValues(typeof(ShipType)))
                Levels[st] = 0;
        }

        // ── Queries ──────────────────────────────────────────────────────────────

        public bool CanResearch(ShipType type)
        {
            if (type == ShipType.Mothership) return false;
            return Levels[type] < 3;
        }

        public int  NextLevel(ShipType type)   => Levels[type] + 1;
        public int  CostFor(ShipType type)     => ResearchCosts[(int)type, Levels[type]];
        public int  DurationFor(ShipType type) => ResearchTimes[(int)type, Levels[type]];

        public bool IsResearching(ShipType type) =>
            ActiveOrder != null && ActiveOrder.Type == type;

        // ── Start a research order ───────────────────────────────────────────────

        public bool TryStart(ShipType type, ref int playerResources, out string error)
        {
            error = null;
            if (type == ShipType.Mothership) { error = "Mothership cannot be upgraded."; return false; }
            if (!CanResearch(type))          { error = $"{type} already at max upgrade (Mk.III)."; return false; }
            if (ActiveOrder != null)         { error = "Research lab is busy — wait for current research."; return false; }

            int cost = CostFor(type);
            if (playerResources < cost) { error = $"Need {cost} resources (have {playerResources})."; return false; }

            playerResources -= cost;
            ActiveOrder = new ResearchOrder(type, NextLevel(type), DurationFor(type));
            return true;
        }

        // ── Tick (call every Update) ─────────────────────────────────────────────

        public ResearchOrder Tick(int deltaMs)
        {
            if (ActiveOrder == null) return null;
            ActiveOrder.Elapsed += deltaMs;
            if (!ActiveOrder.IsComplete) return null;

            var completed = ActiveOrder;
            Levels[completed.Type] = completed.Level;
            ActiveOrder = null;
            return completed;
        }

        // ── Apply bonuses to a newly spawned ship ────────────────────────────────

        public void ApplyTo(Ship ship)
        {
            int level = Levels[ship.Type];
            if (level == 0) return;

            // Hull: +20% per level of base HP
            float hullMult   = 1f + HullBonusPerLevel * level;
            float damageMult = 1f + DamageBonusPerLevel * level;
            ship.MaxHPValue *= hullMult;
            ship.HP          = ship.MaxHPValue;
            ship.Damage     *= damageMult;

            // Speed: +12% Mk.I, slight friction each subsequent level
            float speedMult;
            if (level == 1)
                speedMult = 1f + SpeedBonusPerLevel;
            else if (level == 2)
                speedMult = (1f + SpeedBonusPerLevel) * (1f + SpeedBonusPerLevel * 0.85f);
            else // level 3
                speedMult = (1f + SpeedBonusPerLevel) * (1f + SpeedBonusPerLevel * 0.85f) * (1f + SpeedBonusPerLevel * 0.70f);

            ship.ApplySpeedMultiplier(speedMult);
        }

        // ── Display helpers ──────────────────────────────────────────────────────

        public string LevelLabel(ShipType type)
        {
            switch (Levels[type])
            {
                case 0: return "Base";
                case 1: return "Mk.I";
                case 2: return "Mk.II";
                case 3: return "Mk.III";
                default: return "?";
            }
        }

        public string NextLevelLabel(ShipType type)
        {
            int next = NextLevel(type);
            return next == 1 ? "Mk.I" : next == 2 ? "Mk.II" : "Mk.III";
        }
    }

    // ── Research Order ───────────────────────────────────────────────────────────

    public class ResearchOrder
    {
        public ShipType Type    { get; }
        public int      Level   { get; }
        public int      TotalMs { get; }
        public int      Elapsed { get; set; }
        public bool     IsComplete => Elapsed >= TotalMs;
        public float    Progress   => TotalMs > 0 ? Math.Min(1f, (float)Elapsed / TotalMs) : 1f;

        public ResearchOrder(ShipType type, int level, int totalMs)
        {
            Type    = type;
            Level   = level;
            TotalMs = totalMs;
        }
    }
}
