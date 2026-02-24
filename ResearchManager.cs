using System;
using System.Collections.Generic;

namespace FleetCommand
{
    /// <summary>
    /// Tracks research upgrade levels (0-3) for each ship type and manages
    /// the active research order plus a pending queue of up to 2 items.
    /// Upgrades boost hull (MaxHP), speed, and damage.
    /// Resources for queued items are deducted immediately on enqueue.
    /// </summary>
    public class ResearchManager
    {
        // Current upgrade level per ship type (0 = none, 1 = Mk.I, 2 = Mk.II, 3 = Mk.III)
        public Dictionary<ShipType, int> Levels { get; } = new Dictionary<ShipType, int>();

        // Currently researching (null = idle)
        public ResearchOrder ActiveOrder { get; private set; }

        // Pending queue — resources are deducted on enqueue; max 2 items
        private readonly List<ShipType> _queue = new List<ShipType>(2);
        public IReadOnlyList<ShipType> ResearchQueue => _queue;
        public int QueueCount => _queue.Count;

        private const float HullBonusPerLevel   = 0.20f;  // +20% hull per level
        private const float SpeedBonusPerLevel  = 0.12f;  // +12% speed per level (with friction)
        private const float DamageBonusPerLevel = 0.10f;  // +10% damage per level

        // Research costs: [shipTypeIndex][level-1]
        private static readonly int[,] ResearchCosts =
        {
            //  Mk.I   Mk.II   Mk.III
            {   0,     0,      0    },   // Mothership        (no research)
            { 200,   500,    800   },   // Miner
            { 300,   700,   1000   },   // Interceptor
            { 300,   700,   1000   },   // Bomber
            { 400,   900,   1200   },   // Corvet
            { 600,  1200,   1500   },   // Frigate
            { 900,  2000,   2500   },   // Destroyer
            {1500,  2500,   5000   },   // Battlecruiser
            {   0,     0,      0   },   // ResourceCollector  (no research)
            {1000,  2000,   3500   },   // Carrier
        };

        // Research duration in ms: [shipTypeIndex][level-1]
        private static readonly int[,] ResearchTimes =
        {
            {     0,      0,       0 },  // Mothership
            { 10000,  20000,   30000 },  // Miner
            { 12000,  25000,   35000 },  // Interceptor
            { 12000,  25000,   35000 },  // Bomber
            { 15000,  30000,   45000 },  // Corvet
            { 20000,  40000,   60000 },  // Frigate
            { 30000,  60000,   90000 },  // Destroyer
            { 40000,  70000,  100000 },  // Battlecruiser
            {     0,      0,       0 },  // ResourceCollector (no research)
            { 25000,  50000,   80000 },  // Carrier
        };

        public ResearchManager()
        {
            foreach (ShipType st in Enum.GetValues(typeof(ShipType)))
                Levels[st] = 0;
        }

        // ── Queries ──────────────────────────────────────────────────────────────

        public bool CanResearch(ShipType type)
        {
            if (type == ShipType.Mothership || type == ShipType.ResourceCollector) return false;
            return Levels[type] < 3;
        }

        public int  NextLevel(ShipType type)   => Levels[type] + 1;
        public int  CostFor(ShipType type)     => ResearchCosts[(int)type, Levels[type]];
        public int  DurationFor(ShipType type) => ResearchTimes[(int)type, Levels[type]];

        public bool IsResearching(ShipType type) =>
            ActiveOrder != null && ActiveOrder.Type == type;

        public bool IsQueued(ShipType type) => _queue.Contains(type);

        // ── Start a research order (lab must be idle) ────────────────────────────

        public bool TryStart(ShipType type, ref int playerResources, out string error)
        {
            error = null;
            if (type == ShipType.Mothership || type == ShipType.ResourceCollector)
                { error = $"{type} cannot be upgraded."; return false; }
            if (!CanResearch(type))          { error = $"{type} already at max upgrade (Mk.III)."; return false; }
            if (ActiveOrder != null)         { error = "Research lab is busy — wait for current research."; return false; }

            int cost = CostFor(type);
            if (playerResources < cost) { error = $"Need {cost} resources (have {playerResources})."; return false; }

            playerResources -= cost;
            ActiveOrder = new ResearchOrder(type, NextLevel(type), DurationFor(type));
            return true;
        }

        // ── Add to the research queue (max 2; resources charged immediately) ─────

        public bool TryEnqueue(ShipType type, ref int playerResources, out string error)
        {
            error = null;
            if (type == ShipType.Mothership || type == ShipType.ResourceCollector)
                { error = $"{type} cannot be upgraded."; return false; }
            if (!CanResearch(type))
                { error = $"{type} is already at max upgrade (Mk.III)."; return false; }
            if (ActiveOrder == null)
                { error = "Research lab is idle — start a research directly."; return false; }
            if (IsResearching(type))
                { error = $"{type} is currently being researched."; return false; }
            if (IsQueued(type))
                { error = $"{type} is already in the research queue."; return false; }
            if (_queue.Count >= 2)
                { error = "Research queue is full (max 2 items)."; return false; }

            int cost = CostFor(type);
            if (playerResources < cost)
                { error = $"Need {cost} resources (have {playerResources})."; return false; }

            playerResources -= cost;
            _queue.Add(type);
            return true;
        }

        // ── Remove from queue and refund resources ────────────────────────────────

        public bool TryDequeue(ShipType type, ref int playerResources)
        {
            int idx = _queue.IndexOf(type);
            if (idx < 0) return false;

            // Refund the cost that was charged when enqueuing
            playerResources += CostFor(type);
            _queue.RemoveAt(idx);
            return true;
        }

        // ── Tick (call every Update) ─────────────────────────────────────────────

        public ResearchOrder Tick(int deltaMs)
        {
            if (ActiveOrder == null)
            {
                // Auto-pop the queue if something is waiting
                if (_queue.Count > 0)
                {
                    var next = _queue[0];
                    _queue.RemoveAt(0);
                    ActiveOrder = new ResearchOrder(next, NextLevel(next), DurationFor(next));
                }
                return null;
            }

            ActiveOrder.Elapsed += deltaMs;
            if (!ActiveOrder.IsComplete) return null;

            var completed = ActiveOrder;
            Levels[completed.Type] = completed.Level;
            ActiveOrder = null;

            // Immediately start the next queued item (if any)
            if (_queue.Count > 0)
            {
                var next = _queue[0];
                _queue.RemoveAt(0);
                ActiveOrder = new ResearchOrder(next, NextLevel(next), DurationFor(next));
            }

            return completed;
        }

        // ── Speed multiplier helper ──────────────────────────────────────────────

        /// <summary>Cumulative speed multiplier for a given upgrade level (0 = base).</summary>
        private float GetSpeedMultiplier(int level)
        {
            if (level <= 0) return 1f;
            float m = 1f + SpeedBonusPerLevel;                    // Mk.I
            if (level == 1) return m;
            m *= 1f + SpeedBonusPerLevel * 0.85f;                 // Mk.II
            if (level == 2) return m;
            return m * (1f + SpeedBonusPerLevel * 0.70f);         // Mk.III
        }

        // ── Apply bonuses to a newly spawned ship ────────────────────────────────

        public void ApplyTo(Ship ship)
        {
            int level = Levels[ship.Type];
            if (level == 0) return;

            ship.MaxHPValue *= 1f + HullBonusPerLevel   * level;
            ship.HP          = ship.MaxHPValue;               // new ship: full HP
            ship.Damage     *= 1f + DamageBonusPerLevel * level;
            ship.ApplySpeedMultiplier(GetSpeedMultiplier(level));
        }

        // ── Retrofit an existing ship from one level to another ───────────────────

        /// <summary>
        /// Applies the incremental stat delta between <paramref name="fromLevel"/> and
        /// <paramref name="toLevel"/> to a ship that is already in the field.
        /// Unlike <see cref="ApplyTo"/>, this preserves the ship's current HP ratio
        /// (a damaged ship stays damaged — it is not healed to full).
        /// Also updates <see cref="Ship.UpgradeLevel"/> to <paramref name="toLevel"/>.
        /// </summary>
        public void ApplyUpgradeDelta(Ship ship, int fromLevel, int toLevel)
        {
            if (fromLevel >= toLevel) return;

            // Hull — scale MaxHP by the ratio of new/old multiplier, keep HP ratio
            float oldH    = 1f + HullBonusPerLevel * fromLevel;
            float newH    = 1f + HullBonusPerLevel * toLevel;
            float hpRatio = ship.MaxHPValue > 0 ? ship.HP / ship.MaxHPValue : 1f;
            ship.MaxHPValue *= newH / oldH;
            ship.HP          = ship.MaxHPValue * hpRatio;

            // Damage
            float oldD  = 1f + DamageBonusPerLevel * fromLevel;
            float newD  = 1f + DamageBonusPerLevel * toLevel;
            ship.Damage *= newD / oldD;

            // Speed — divide out the old multiplier, apply the new one
            ship.ApplySpeedMultiplier(GetSpeedMultiplier(toLevel) / GetSpeedMultiplier(fromLevel));

            ship.UpgradeLevel = toLevel;
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
