namespace FleetCommand
{
    public enum ShipType
    {
        Mothership,
        Miner,
        Interceptor,
        Bomber,
        Corvet,
        Frigate,
        Destroyer,
        Battlecruiser,
        ResourceCollector
    }

    // AI strategy level — affects decision-making, NOT stat multipliers
    public enum AiLevel { Easy, Normal, Hard, Expert }

    public enum GameState { Playing, Paused, GameOver, Victory }

    public static class GameConstants
    {
        public static readonly int[] BuildCosts = {
            0, 50, 150, 200, 300, 500, 800, 1500, 400
        };
        public static readonly int[] BuildTimes = {
            0, 5000, 6000, 10000, 12000, 15000, 30000, 45000, 12000
        };
        public static readonly int[] MaxHP = {
            25000, 100, 200, 220, 260, 500, 3000, 10000, 600
        };
        public static readonly float[] Damage = {
            1.0f, 0, 0.3f, 0.5f, 0.4f, 0.7f, 1.2f, 2.5f, 0
        };
        public static readonly float[] Speeds = {
            0.5f, 1.5f, 3.5f, 2.5f, 3.0f, 1.8f, 1.3f, 1.0f, 1.2f
        };

        public const int MiningRate         = 10;
        public const int AsteroidMinResource = 1500;
        public const int AsteroidMaxResource = 20000;
        public const int MapWidth            = 6000;
        public const int MapHeight           = 4000;
        public const int StartingResources   = 500;
        public const int StartingMiners      = 3;

        public const float CapitalSelfRepairRate = 10.0f;
        public const float DockRepairRate        = 15.0f;
        public const float DockRange             = 60f;

        // ── AI strategy parameters (indexed by AiLevel) ──────────────────────
        // All values affect decision-making only — no stat cheating.
        public static readonly int[]  AiTargetMiners          = { 3,  5,  7, 10 };
        public static readonly int[]  AiBuildQueueMax         = { 2,  3,  4,  5 };
        public static readonly int[]  AiBuildTickMs           = { 12000, 8000, 5000, 3000 };
        public static readonly int[]  AiWaveIntervalMs        = { 90000, 60000, 40000, 25000 };
        public static readonly int[]  AiMinFleetToAttack      = { 8,  5,  4,  3 };
        public static readonly int[]  AiCombatBuildThreshold  = { 600, 400, 300, 200 };
        public static readonly bool[] AiUsesCollectors        = { false, false, true, true };
        public static readonly bool[] AiTargetsEconomy        = { false, false, true, true };
        public static readonly bool[] AiBuildHeavyShips       = { false, true,  true, true };
    }
}
