using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace FleetCommand
{
    /// <summary>
    /// One EnemyController per computer opponent. It manages its own resource
    /// economy (miners, collectors), build decisions, and attack strategy.
    /// Difficulty is expressed purely through smarter decisions, never stat cheats.
    /// </summary>
    public class EnemyController
    {
        public  Mothership  Mothership   { get; }
        public  int         Resources    { get; set; }
        public  AiLevel     Level        { get; }
        public  int         Index        { get; }   // 0,1,2 → used for colour/label
        public  bool        IsAlive      => Mothership.IsAlive;

        private readonly Random rng = new Random();

        // Timers
        private int buildTick  = 0;
        private int waveTick   = 0;

        // AI state
        private int  phase     = 0;   // 0=economy, 1=military, 2=aggression
        private bool sentFirstWave = false;

        private int Idx => (int)Level;

        public EnemyController(Mothership ms, AiLevel level, int index, int startResources)
        {
            Mothership = ms;
            Level      = level;
            Index      = index;
            Resources  = startResources;
        }

        /// <summary>Called every game tick.</summary>
        public void Update(int deltaMs, List<Ship> allShips, List<Asteroid> asteroids,
                           Mothership playerMothership)
        {
            if (!IsAlive) return;

            // ── Economy: process miner deposits ──────────────────────────────
            foreach (var miner in allShips.OfType<Miner>()
                .Where(m => m.IsAlive && !m.IsPlayerOwned && OwnedBy(m) && m.PendingDeposit > 0)
                .ToList())
            {
                Resources           += miner.PendingDeposit;
                miner.PendingDeposit = 0;
                // Re-assign miner to nearest asteroid
                var ast = asteroids.Where(a => a.IsAlive)
                    .OrderBy(a => Dist(miner.Position, a.Position)).FirstOrDefault();
                if (ast != null) { miner.TargetAsteroid = ast; miner.IsMining = true; }
            }

            // ── Economy: process collector deposits ───────────────────────────
            /*foreach (var rc in allShips.OfType<ResourceCollector>()
                .Where(r => r.IsAlive && !r.IsPlayerOwned && OwnedBy(r) && r.PendingDeposit > 0)
                .ToList())
            {
                Resources          += rc.PendingDeposit;
                rc.PendingDeposit   = 0;
            }*/

            // ── Auto-assign idle miners ───────────────────────────────────────
            foreach (var miner in allShips.OfType<Miner>()
                .Where(m => m.IsAlive && !m.IsPlayerOwned && OwnedBy(m)
                         && !m.IsMining && !m.ReturningToMothership && m.AttackTarget == null))
            {
                var ast = asteroids.Where(a => a.IsAlive)
                    .OrderBy(a => Dist(miner.Position, a.Position)).FirstOrDefault();
                if (ast != null) { miner.TargetAsteroid = ast; miner.IsMining = true; }
            }

            // ── Build decisions ───────────────────────────────────────────────
            buildTick += deltaMs;
            if (buildTick >= GameConstants.AiBuildTickMs[Idx])
            {
                buildTick = 0;
                RunBuildLogic(allShips, asteroids);
            }

            // ── Attack waves ──────────────────────────────────────────────────
            waveTick += deltaMs;
            if (waveTick >= GameConstants.AiWaveIntervalMs[Idx])
            {
                waveTick = 0;
                RunWaveLogic(allShips, playerMothership);
            }
        }

        private void RunBuildLogic(List<Ship> allShips, List<Asteroid> asteroids)
        {
            if (Mothership.BuildQueue.Count >= GameConstants.AiBuildQueueMax[Idx]) return;

            var myShips  = allShips.Where(s => s.IsAlive && OwnedBy(s)).ToList();
            int miners   = myShips.Count(s => s.Type == ShipType.Worker);
            int combat   = myShips.Count(s => IsCombatShip(s.Type));
            int colls    = myShips.Count(s => s.Type == ShipType.ResourceCollector);

            int targetMiners = GameConstants.AiTargetMiners[Idx];

            // Phase 0: economy first — build miners until target reached
            if (miners < targetMiners && Resources >= GameConstants.BuildCosts[(int)ShipType.Worker])
            {
                QueueBuild(ShipType.Worker, allShips);
                return;
            }

            // Maintain scout probe fleet (like miners for exploration)
            // Probes are high-priority for reconnaissance
            int probes = myShips.Count(s => s.Type == ShipType.Probe);
            int targetProbes = GameConstants.AiTargetProbes[Idx];

            // Build probes to target count (even before collectors and combat)
            if (probes < targetProbes && miners >= 1 && Resources >= GameConstants.BuildCosts[(int)ShipType.Probe])
            {
                QueueBuild(ShipType.Probe, allShips);
                return;  // Prioritize probe building for reconnaissance
            }

            // Build a collector if AI uses them and doesn't have enough
            if (GameConstants.AiUsesCollectors[Idx] && colls < 1 + miners / 5
                && Resources >= GameConstants.BuildCosts[(int)ShipType.ResourceCollector])
            {
                QueueBuild(ShipType.ResourceCollector, allShips);
                return;
            }

            // Phase 1+: build combat ships once economy is running and resources allow
            if (miners >= 2 && Resources >= GameConstants.AiCombatBuildThreshold[Idx])
            {
                ShipType choice = PickCombatShip(combat, miners);
                if (Resources >= GameConstants.BuildCosts[(int)choice])
                    QueueBuild(choice, allShips);
            }
        }

        private ShipType PickCombatShip(int combatCount, int minerCount)
        {
			// Easy: only light shipst + corvet
			if (Level == AiLevel.Easy)
            {
                var pool = new[] { ShipType.Interceptor, ShipType.Bomber, ShipType.Corvette };
                return pool[rng.Next(pool.Length)];
            }


			// Normal: light + Corvette + occasional frigate
			if (Level == AiLevel.Normal)
            {
                if (combatCount > 6 && rng.Next(3) == 0)
                    return ShipType.Frigate;
                var pool = new[] { ShipType.Interceptor, ShipType.Bomber, ShipType.Corvette };
                return pool[rng.Next(pool.Length)];
            }


			// Hard: mixed fleet, occasional frigate
			if (Level == AiLevel.Hard)
			{
				if (combatCount > 8 && Resources > 800 && rng.Next(3) == 0)
					return ShipType.Destroyer;
				if (combatCount > 6 && rng.Next(2) == 0)
					return ShipType.Frigate;
				var pool = new[] { ShipType.Interceptor, ShipType.Bomber, ShipType.Corvette };
				return pool[rng.Next(pool.Length)];
			}

			// Expert: diversified fleet scaling to Destroyer/Battlecruiser
			if (combatCount > 12 && Resources > 1500 && rng.Next(4) == 0)
                return ShipType.Battlecruiser;
            if (combatCount > 8  && Resources > 800  && rng.Next(3) == 0)
                return ShipType.Destroyer;
            if (combatCount > 4  && Resources > 500  && rng.Next(2) == 0)
                return ShipType.Frigate;
            var light = new[] { ShipType.Interceptor, ShipType.Bomber, ShipType.Corvette };
            return light[rng.Next(light.Length)];
        }

        private void RunWaveLogic(List<Ship> allShips, Mothership playerMothership)
        {
            if (!playerMothership.IsAlive) return;

            var playerShips = allShips.Where(s => s.IsAlive && s.IsPlayerOwned).ToList();
            var visibleCombatShips = playerShips.Where(s => s.Type != ShipType.Mothership).ToList();

            // ── Scout probes independently ──────────────────────────────────────
            // Send probes to explore whenever no player combat ships are visible
            var myProbes = allShips.Where(s => s.IsAlive && OwnedBy(s) && s.Type == ShipType.Probe).ToList();
            if (myProbes.Count > 0 && visibleCombatShips.Count == 0)
            {
                SendScoutMission(myProbes, playerMothership.Position);
            }

            // ── Combat fleet waves ─────────────────────────────────────────────
            var myFleet = allShips.Where(s => s.IsAlive && OwnedBy(s) && IsCombatShip(s.Type)).ToList();
            if (myFleet.Count < GameConstants.AiMinFleetToAttack[Idx]) return;

            // If no visible player ships except mothership, send combat ships to explore
            if (visibleCombatShips.Count == 0)
            {
                SendScoutMission(myFleet, playerMothership.Position);
                return;
            }

            foreach (var ship in myFleet)
            {
                Ship target;

                // Expert/Hard: sometimes target economy ships to disrupt player income
                if (GameConstants.AiTargetsEconomy[Idx] && rng.Next(3) == 0)
                {
                    var econTargets = playerShips
                        .Where(p => p.Type == ShipType.Worker || p.Type == ShipType.ResourceCollector)
                        .ToList();
                    if (econTargets.Count > 0)
                    {
                        target = econTargets[rng.Next(econTargets.Count)];
                        ship.AttackTarget = target;
                        ship.Destination  = null;
                        continue;
                    }
                }

                // Default: attack combat ships, fall back to mothership
                var combatTargets = playerShips
                    .Where(p => p.Type != ShipType.Worker && p.Type != ShipType.Mothership
                             && p.Type != ShipType.ResourceCollector)
                    .ToList();

                if (combatTargets.Count > 0)
                    target = combatTargets[rng.Next(combatTargets.Count)];
                else
                    target = playerMothership;

                ship.AttackTarget = target;
                ship.Destination  = null;
            }

            string label = Level == AiLevel.Easy   ? "Easy AI" :
                           Level == AiLevel.Normal  ? "Normal AI" :
                           Level == AiLevel.Hard    ? "Hard AI" : "Expert AI";
        }

        // Send scout ships to search for the player
        private void SendScoutMission(List<Ship> myFleet, PointF playerMothershipPos)
        {
            // Probes use their large vision radius efficiently with mid-map sweeping
            // Combat fighters explore edge areas where player likely is
            bool isProbeFleet = myFleet.Count > 0 && myFleet[0].Type == ShipType.Probe;

            for (int i = 0; i < myFleet.Count; i++)
            {
                var ship = myFleet[i];
                float searchX, searchY;

                if (isProbeFleet)
                {
                    // Probes: position in mid-map grid for efficient radar coverage (600 unit range)
                    // Create sweep pattern across map using probe vision radius
                    // X positions: 1500, 3000, 4500 (1500 apart, centered)
                    // Y positions: 1000, 2000, 3000 (distributed vertically)
                    int[] probeXPositions = { 1500, 3000, 4500 };
                    int[] probeYPositions = { 1000, 2000, 3000 };

                    searchX = probeXPositions[i % probeXPositions.Length];
                    searchY = probeYPositions[(i / probeXPositions.Length) % probeYPositions.Length];

                    // Small randomness to prevent exact overlap
                    searchX += rng.Next(-100, 100);
                    searchY += rng.Next(-100, 100);
                }
                else
                {
                    // Combat ships: explore edge areas toward player territory (left side of map)
                    // X range: 0 to MapWidth/3, Y range: full map height
                    searchX = (i % 3) * (GameConstants.MapWidth / 3);
                    searchY = (float)GameConstants.MapHeight * ((i / 3) % 3) / 3;

                    // Add randomness to search pattern
                    searchX += rng.Next(-200, 200);
                    searchY += rng.Next(-200, 200);
                }

                // Clamp to map bounds
                searchX = Math.Max(0, Math.Min(GameConstants.MapWidth, searchX));
                searchY = Math.Max(0, Math.Min(GameConstants.MapHeight, searchY));

                ship.Destination = new PointF(searchX, searchY);
                ship.AttackTarget = null;
            }
        }

        private void QueueBuild(ShipType type, List<Ship> allShips)
        {
            int cost = GameConstants.BuildCosts[(int)type];
            if (Resources < cost) return;
            if (Mothership.BuildQueue.Count >= GameConstants.AiBuildQueueMax[Idx]) return;

            // Fleet cap: count live ships + queued spawns for this type on our team
            int cap        = GameConstants.FleetCaps[(int)type];
            int spawnCount = type == ShipType.Interceptor || type == ShipType.Bomber ? 5
                           : type == ShipType.Corvette ? 3
                           : 1;
            int live       = allShips.Count(s => s.IsAlive && s.TeamId == Mothership.TeamId && s.Type == type);
            int queued     = Mothership.BuildQueue.Count(q => q.Type == type) * spawnCount;
            if (live + queued + spawnCount > cap) return;

            Resources -= cost;
            Mothership.BuildQueue.Add(new BuildOrder(type, 1.0f));
        }

        // Returns true if this ship belongs to this controller (same team, not player)
        private bool OwnedBy(Ship s) => !s.IsPlayerOwned && SameTeam(s);

        // Differentiate multiple AIs by their mothership reference
        private bool SameTeam(Ship s)
        {
            // All non-player ships are considered owned by whoever spawned them.
            // With a single mothership list we track ownership via the TeamId property.
            return s.TeamId == Mothership.TeamId;
        }

        private static bool IsCombatShip(ShipType t) =>
            t == ShipType.Interceptor || t == ShipType.Bomber || t == ShipType.Corvette ||
            t == ShipType.Frigate     || t == ShipType.Destroyer || t == ShipType.Battlecruiser;

        private static float Dist(PointF a, PointF b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
