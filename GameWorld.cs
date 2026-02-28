using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace FleetCommand
{
    public class GameWorld
    {
        public List<Ship>           Ships         { get; } = new List<Ship>();
        public List<Asteroid>       Asteroids     { get; } = new List<Asteroid>();
        public List<CombatEffect>   CombatEffects { get; } = new List<CombatEffect>();
        public int                  PlayerResources { get; set; }
        public GameState            State         { get; set; } = GameState.Playing;
        public Mothership           PlayerMothership { get; private set; }
        public List<EnemyController> Enemies      { get; } = new List<EnemyController>();
        public ResearchManager      Research      { get; } = new ResearchManager();
        public List<string>         EventLog      { get; } = new List<string>();

        // Legacy single-enemy accessor for GameForm compatibility
        public Mothership EnemyMothership => Enemies.FirstOrDefault()?.Mothership;

        private readonly Random rng = new Random();
        private int repairLogTimer;

        // Visibility tracking: which ships are visible to each team
        // visibleShips[teamId] = set of Ship objects visible to that team (recalculated each frame)
        private Dictionary<int, HashSet<Ship>> _visibleShips = new Dictionary<int, HashSet<Ship>>()
        {
            { 0, new HashSet<Ship>() },  // Team 0 (player) visible ships
            { 1, new HashSet<Ship>() },  // Team 1 (enemy) visible ships
            { 2, new HashSet<Ship>() },
            { 3, new HashSet<Ship>() }
        };

        public GameWorld(List<AiLevel> enemyLevels)
        {
            PlayerResources = GameConstants.StartingResources;
            InitializeMap(enemyLevels);
        }

        private void InitializeMap(List<AiLevel> enemyLevels)
        {
            // ── Player ────────────────────────────────────────────────────────

            // Pick a random spawn on the left third of the map, tracked for min-distance checks.
            var spawnedPositions = new List<PointF>();
            PointF playerPos = RandomSpawnPos(
                200, (int)(GameConstants.MapWidth * 0.3f),
                200, (int)(GameConstants.MapHeight - 200),
                spawnedPositions);

            PlayerMothership = new Mothership(playerPos, true);
            PlayerMothership.TeamId = 0;
            Ships.Add(PlayerMothership);
            spawnedPositions.Add(playerPos);

            // Starting miners for player
            for (int i = 0; i < GameConstants.StartingMiners; i++)
            {
                var m = (Miner)ShipFactory.Create(ShipType.Worker,
                    ShipFactory.RandomOffset(PlayerMothership.Position, 80), true);
                m.TeamId = 0;
                Ships.Add(m);
            }

			//debug
			//         var carrier = new Carrier(new PointF(450, GameConstants.MapHeight / 2f-100), true);
			//         carrier.TeamId = 0;
			//         Ships.Add(carrier);

			//         var battle = new Battlecruiser(new PointF(500, GameConstants.MapHeight / 2f + 100), true);
			//         battle.TeamId = 0;
			//         Ships.Add(battle);

			//         var fighter = new Interceptor(new PointF(600, GameConstants.MapHeight / 2f + 200), true);
			//         fighter.TeamId = 0;
			//         Ships.Add(fighter);

			//var destroyer = new Destroyer(new PointF(600, GameConstants.MapHeight / 2f + 100), true);
			//destroyer.TeamId = 0;
			//Ships.Add(destroyer);

			//var frigate = new Frigate(new PointF(600, GameConstants.MapHeight / 2f), true);
			//frigate.TeamId = 0;
			//Ships.Add(frigate);

			// ── Enemy AIs ─────────────────────────────────────────────────────
			int count  = enemyLevels.Count;

            for (int i = 0; i < count; i++)
            {
                // Enemies spawn on the right third of the map, spread across vertical space.
                float baseY   = count == 1
                    ? GameConstants.MapHeight / 2f
                    : GameConstants.MapHeight * 0.1f + GameConstants.MapHeight * 0.8f * i / (count - 1);
                float jitterY = (float)(rng.NextDouble() * 2 - 1) * GameConstants.MapHeight * 0.15f;
                int   yMin    = (int)Math.Max(200, baseY + jitterY - 100);
                int   yMax    = (int)Math.Min(GameConstants.MapHeight - 200, baseY + jitterY + 100);
                if (yMin >= yMax) yMax = yMin + 1;

                PointF enemyPos = RandomSpawnPos(
                    (int)(GameConstants.MapWidth * 0.7f), (int)(GameConstants.MapWidth - 200),
                    yMin, yMax,
                    spawnedPositions);
                spawnedPositions.Add(enemyPos);

                int   tid = i + 1;

                var ms = new Mothership(enemyPos, false);
                ms.TeamId = tid;
                Ships.Add(ms);

                // Starting miners for this AI
                for (int j = 0; j < GameConstants.StartingMiners; j++)
                {
                    var m = (Miner)ShipFactory.Create(ShipType.Worker,
                        ShipFactory.RandomOffset(ms.Position, 80), false);
                    m.TeamId = tid;
                    Ships.Add(m);
                }

                var ctrl = new EnemyController(ms, enemyLevels[i], i, GameConstants.StartingResources);
                Enemies.Add(ctrl);

                LogEvent($"Enemy {i + 1}: {enemyLevels[i]} AI");
            }

			// ── Asteroids ─────────────────────────────────────────────────────

            // Starter asteroids — a cluster near each mothership for early-game economy.
            var allMotherships = new List<PointF> { PlayerMothership.Position };
            foreach (var e in Enemies) allMotherships.Add(e.Mothership.Position);

            foreach (var msPos in allMotherships)
            {
                for (int k = 0; k < 3; k++)
                {
                    double angle  = rng.NextDouble() * 2 * Math.PI;
                    float  radius = 180f + (float)(rng.NextDouble() * 170f);  // 180–350 wu
                    var rawPos = new PointF(
                        msPos.X + (float)Math.Cos(angle) * radius,
                        msPos.Y + (float)Math.Sin(angle) * radius);
                    // Clamp to map bounds [0, MapWidth] × [0, MapHeight]
                    var pos = new PointF(
                        Math.Max(0f, Math.Min(GameConstants.MapWidth, rawPos.X)),
                        Math.Max(0f, Math.Min(GameConstants.MapHeight, rawPos.Y)));
                    Asteroids.Add(new Asteroid(pos));
                }
            }

            // Random asteroids across the rest of the map.
            int asteroidCount = 24 + rng.Next(12);
            for (int i = 0; i < asteroidCount; i++)
            {
                var pos = new PointF(
                    rng.Next(200, GameConstants.MapWidth - 200),
                    rng.Next(100, GameConstants.MapHeight - 100));

                bool tooClose = false;
                if (Dist(pos, PlayerMothership.Position) < 150) tooClose = true;
                foreach (var e in Enemies)
                    if (Dist(pos, e.Mothership.Position) < 150) tooClose = true;

                if (!tooClose) Asteroids.Add(new Asteroid(pos));
            }

            LogEvent($"Game started — {count} opponent(s). Mine asteroids!");
            LogEvent($"Starting resources: {GameConstants.StartingResources}");
        }

        public void Update(int deltaMs)
        {
            if (State != GameState.Playing) return;

            // ── Calculate visibility (before anything else) ────────────────────
            UpdateVisibility();

            // ── Ship physics & miner AI ───────────────────────────────────────
            foreach (var ship in Ships.Where(s => s.IsAlive).ToList())
            {
                ship.DeltaMs = deltaMs;
                ship.Update(Ships, Asteroids, PlayerMothership);
            }

            // ── Player miner deposits ─────────────────────────────────────────
            foreach (var miner in Ships.OfType<Miner>()
                .Where(m => m.IsAlive && m.TeamId == 0 && m.PendingDeposit > 0).ToList())
            {
                PlayerResources     += miner.PendingDeposit;
                miner.PendingDeposit = 0;
                var nearest = Asteroids.Where(a => a.IsAlive)
                    .OrderBy(a => Dist(miner.Position, a.Position)).FirstOrDefault();
                if (nearest != null) { miner.TargetAsteroid = nearest; miner.IsMining = true; }
            }

            // ── Player collector / carrier receiving flag ─────────────────────
            foreach (var rc in Ships.OfType<ResourceCollector>()
                .Where(r => r.IsAlive && r.TeamId == 0))
                rc.IsReceiving = false;
            foreach (var cv in Ships.OfType<Carrier>()
                .Where(c => c.IsAlive && c.TeamId == 0))
                cv.IsReceiving = false;

            // ── Build queues ──────────────────────────────────────────────────
            ProcessBuildQueue(PlayerMothership, true, deltaMs);
            foreach (var e in Enemies)
                if (e.IsAlive) ProcessBuildQueue(e.Mothership, false, deltaMs, e);

            // ── Carrier build queues (player only) ────────────────────────────
            foreach (var cv in Ships.OfType<Carrier>()
                .Where(c => c.IsAlive && c.TeamId == 0).ToList())
                ProcessCarrierBuildQueue(cv, deltaMs);

            // ── Research ──────────────────────────────────────────────────────
            var completed = Research.Tick(deltaMs);
            if (completed != null)
            {
                // Retrofit every existing ship of the same type that belongs to
                // the player (TeamId == 0). Each ship tracks its own UpgradeLevel
                // so ships built before an earlier upgrade tier are handled correctly.
                foreach (var ship in Ships.Where(
                    s => s.IsAlive && s.TeamId == 0 && s.Type == completed.Type))
                {
                    Research.ApplyUpgradeDelta(ship, ship.UpgradeLevel, completed.Level);
                }

                int retrofitCount = Ships.Count(
                    s => s.IsAlive && s.TeamId == 0 && s.Type == completed.Type);
                LogEvent($"✦ Research complete! {completed.Type} → Mk.{completed.Level}" +
                         (retrofitCount > 0 ? $"  ({retrofitCount} ship{(retrofitCount > 1 ? "s" : "")} upgraded)" : ""));

                // Log if a queued item auto-started immediately after completion
                if (Research.ActiveOrder != null)
                    LogEvent($"⚗ Researching {Research.ActiveOrder.Type} → Mk.{Research.ActiveOrder.Level}... (from queue)");
            }

            // ── Enemy AI controllers ──────────────────────────────────────────
            foreach (var e in Enemies)
            {
                if (!e.IsAlive) continue;

                // Determine which team this enemy controls
                int enemyTeamId = e.Mothership != null ? e.Mothership.TeamId : 1;

                // Pass only visible ships to enemy AI (fog of war)
                // AI can see: its own ships + visible enemy ships + player mothership (always detectable)
                var visibleShips = Ships.Where(s => s.IsAlive &&
                    (s.TeamId == enemyTeamId ||
                     IsShipVisible(s, enemyTeamId) ||
                     s == PlayerMothership))  // Mothership always visible to enemy AI
                    .ToList();

                e.Update(deltaMs, visibleShips, Asteroids, PlayerMothership);
            }

            // ── Combat ────────────────────────────────────────────────────────
            ResolveCombat();

            float dt = deltaMs / 1000f;
            CombatEffects.ForEach(fx => fx.Tick(dt));
            CombatEffects.RemoveAll(fx => fx.IsExpired);

            repairLogTimer += deltaMs;
            ResolveRepairs(deltaMs);

            foreach (var s in Ships)
                if (s.AttackTarget != null && !s.AttackTarget.IsAlive)
                    s.AttackTarget = null;

            Ships.RemoveAll(s => !s.IsAlive && s.Type != ShipType.Mothership);

            // ── Win/lose check ────────────────────────────────────────────────
            if (!PlayerMothership.IsAlive)
            {
                State = GameState.GameOver;
                LogEvent("DEFEAT! Your mothership has been destroyed!");
            }
            else if (Enemies.All(e => !e.IsAlive))
            {
                State = GameState.Victory;
                LogEvent("VICTORY! All enemy motherships destroyed!");
            }
        }

        private void ProcessBuildQueue(Mothership ms, bool isPlayer, int deltaMs,
                                       EnemyController ai = null)
        {
            if (ms.BuildQueue.Count == 0) return;
            var order = ms.BuildQueue[0];
            order.Elapsed += deltaMs;
            if (!order.IsComplete) return;

            ms.BuildQueue.RemoveAt(0);
            int count = order.Type == ShipType.Interceptor || order.Type == ShipType.Bomber ? 5
                      : order.Type == ShipType.Corvette ? 3
                      : 1;

            // Precompute spawn positions — fighter squadrons appear in a delta.
            PointF[] spawnPositions = new PointF[count];
            if (count > 1)
            {
                // Default orientation: tip pointing north (up on screen).
                // fwd=(0,-1), right=(-1,0) → rearward = +Y, lateral flipped via right.
                PointF fwd   = new PointF(0f, -1f);
                PointF right = new PointF(fwd.Y, -fwd.X);   // (-1, 0)

                // Centre the delta 160 units above the mothership so all ships
                // spawn clear of the mothership sprite (radius ≈ 45 wu).
                PointF center = new PointF(ms.Position.X, ms.Position.Y - 160f);

                const float rowSpacing = 15f;
                const float colSpacing = 15f;

                int idx = 0;
                for (int row = 0; idx < count; row++)
                {
                    int   shipsInRow = Math.Min(row + 1, count - idx);
                    float rear       = row * rowSpacing;

                    for (int i = 0; i < shipsInRow; i++, idx++)
                    {
                        float lat = shipsInRow == 1
                            ? 0f
                            : (-row / 2.0f + (float)i * row / (shipsInRow - 1)) * colSpacing;

                        spawnPositions[idx] = new PointF(
                            center.X + lat * right.X - rear * fwd.X,
                            center.Y + lat * right.Y - rear * fwd.Y);
                    }
                }
            }
            else
            {
                spawnPositions[0] = ShipFactory.RandomOffset(ms.Position, 80);
            }

            for (int i = 0; i < count; i++)
            {
                var newShip  = ShipFactory.Create(order.Type, spawnPositions[i], isPlayer);
                newShip.TeamId = ms.TeamId;
                if (isPlayer)
                {
                    Research.ApplyTo(newShip);
                    newShip.UpgradeLevel = Research.Levels[order.Type];
                }
                Ships.Add(newShip);

                // New miners automatically head to the nearest live asteroid.
                if (order.Type == ShipType.Worker)
                {
                    var miner   = (Miner)newShip;
                    var nearest = Asteroids.Where(a => a.IsAlive)
                                           .OrderBy(a => Dist(miner.Position, a.Position))
                                           .FirstOrDefault();
                    if (nearest != null) { miner.TargetAsteroid = nearest; miner.IsMining = true; }
                }
            }
            if (isPlayer)
                LogEvent($"{order.Type} built!{(count > 1 ? $" Squadron of {count}" : "")}");
            else
            {
                string label = ai != null ? $"Enemy {ai.Index + 1} ({ai.Level})" : "Enemy";
                LogEvent($"{label} built {order.Type}{(count > 1 ? $" ×{count}" : "")}");
            }
        }

        private void ResolveCombat()
        {
            var playerShips = Ships.Where(s => s.IsAlive && s.TeamId == 0).ToList();
            var allEnemyShips = Ships.Where(s => s.IsAlive && s.TeamId != 0).ToList();

            // Clear targets that are no longer visible
            foreach (var ship in playerShips.Concat(allEnemyShips))
            {
                if (ship.AttackTarget != null && !IsShipVisible(ship.AttackTarget, ship.TeamId))
                {
                    ship.AttackTarget = null;  // Can't see target anymore
                }
            }

            // Player ships fire at enemies
            foreach (var ps in playerShips)
            {
                if (ps.Type == ShipType.Worker || ps.Type == ShipType.ResourceCollector) continue;
                float range = ps.GetAttackRange();
                float dmg   = ps.GetDamage();

                if (ps.AttackTarget != null && ps.AttackTarget.IsAlive)
                {
                    if (Dist(ps.Position, ps.AttackTarget.Position) <= range)
                    {
                        float mult = GameConstants.GetCombatMultiplier(ps.Type, ps.AttackTarget.Type);
                        ps.AttackTarget.HP -= dmg * mult;
                        float retMult = GameConstants.GetCombatMultiplier(ps.AttackTarget.Type, ps.Type);
                        ps.HP -= ps.AttackTarget.GetDamage() * retMult * 0.5f;
                        SpawnEffect(ps.Position, ps.AttackTarget.Position, ps.Type, true);
                        if (!ps.AttackTarget.IsAlive)
                        {
                            LogEvent($"Enemy {ps.AttackTarget.Type} destroyed!");
                            ps.AttackTarget = null;
                        }
                    }
                    continue;
                }
                // Only attack visible enemy ships
                foreach (var es in allEnemyShips.Where(s => IsShipVisible(s, 0)))
                {
                    if (Dist(ps.Position, es.Position) <= range)
                    {
                        float mult    = GameConstants.GetCombatMultiplier(ps.Type, es.Type);
                        float retMult = GameConstants.GetCombatMultiplier(es.Type, ps.Type);
                        es.HP -= dmg * mult * 0.5f;
                        ps.HP -= es.GetDamage() * retMult * 0.3f;
                        SpawnEffect(ps.Position, es.Position, ps.Type, true);
                    }
                }
            }

            // Enemy ships fire at player
            foreach (var es in allEnemyShips)
            {
                if (es.Type == ShipType.Worker || es.Type == ShipType.ResourceCollector) continue;
                float range = es.GetAttackRange();
                float dmg   = es.GetDamage();

                if (es.AttackTarget != null && es.AttackTarget.IsAlive)
                {
                    if (Dist(es.Position, es.AttackTarget.Position) <= range)
                    {
                        float mult = GameConstants.GetCombatMultiplier(es.Type, es.AttackTarget.Type);
                        es.AttackTarget.HP -= dmg * mult;
                        SpawnEffect(es.Position, es.AttackTarget.Position, es.Type, false);
                    }
                    continue;
                }
                // Only attack visible player ships
                foreach (var ps in playerShips.Where(s => IsShipVisible(s, es.TeamId)))
                {
                    if (ps.Type == ShipType.Worker || ps.Type == ShipType.ResourceCollector) continue;
                    if (Dist(es.Position, ps.Position) <= range)
                    {
                        float mult = GameConstants.GetCombatMultiplier(es.Type, ps.Type);
                        ps.HP -= dmg * mult * 0.4f;
                        SpawnEffect(es.Position, ps.Position, es.Type, false);
                    }
                }
            }
        }

        private void SpawnEffect(PointF from, PointF to, ShipType attackerType, bool isPlayer)
        {
            if (CombatEffects.Count > 120) return;

            CombatEffectKind kind;
            switch (attackerType)
            {
                // ── Fighters ──────────────────────────────────────────────────
                case ShipType.Interceptor:
                    kind = CombatEffectKind.Missile;      break;   // fast projectile bullet
                case ShipType.Bomber:
                    kind = CombatEffectKind.Bomb;         break;   // slow bomb + explosion rings
                case ShipType.Corvette:
                    kind = isPlayer ? CombatEffectKind.LaserGreen
                                    : CombatEffectKind.LaserRed;   break;   // instant laser
                // ── Capitals ──────────────────────────────────────────────────
                case ShipType.Frigate:
                    kind = CombatEffectKind.FrigateShot;  break;   // flak ball + slash shrapnel
                case ShipType.Destroyer:
                case ShipType.Battlecruiser:
                    kind = CombatEffectKind.IonCannon;    break;   // wide electric beam
                // ── Mothership / Carrier / unknown ────────────────────────────
                default:
                    kind = isPlayer ? CombatEffectKind.PlasmaBlue
                                    : CombatEffectKind.LaserRed;   break;
            }

            // Small target jitter (not applied for missile/bomb — they travel to exact To)
            PointF dest = (kind == CombatEffectKind.Missile || kind == CombatEffectKind.Bomb
                           || kind == CombatEffectKind.FrigateShot)
                ? to
                : new PointF(to.X + (float)(rng.NextDouble() - 0.5) * 5,
                             to.Y + (float)(rng.NextDouble() - 0.5) * 5);

            CombatEffects.Add(new CombatEffect(from, dest, kind));
        }

        private void ResolveRepairs(int deltaMs)
        {
            float dt = deltaMs / 1000f;
            var dockStations = Ships.Where(s => s.IsAlive &&
                (s.Type == ShipType.Mothership || s.Type == ShipType.Battlecruiser ||
                 s.Type == ShipType.Carrier)).ToList();
            var lightShips = Ships.Where(s => s.IsAlive &&
                (s.Type == ShipType.Interceptor || s.Type == ShipType.Bomber ||
                 s.Type == ShipType.Worker       || s.Type == ShipType.Corvette)).ToList();

            foreach (var ds in dockStations) ds.IsDocking = false;
            foreach (var f  in lightShips)   f.IsDocking  = false;

            foreach (var fighter in lightShips)
            {
                if (fighter.HP >= fighter.MaxHPValue) continue;
                Ship nearest = null; float nearestDist = float.MaxValue;
                foreach (var ds in dockStations)
                {
                    if (ds.TeamId != fighter.TeamId) continue;
                    float d = Dist(fighter.Position, ds.Position);
                    if (d < nearestDist) { nearestDist = d; nearest = ds; }
                }
                if (nearest != null && nearestDist <= GameConstants.DockRange)
                {
                    fighter.HP = Math.Min(fighter.MaxHPValue, fighter.HP + GameConstants.DockRepairRate * dt);
                    nearest.IsDocking = true;
                    fighter.IsDocking = true;
                    if (fighter.TeamId == 0 && repairLogTimer >= 10000)
                    {
                        LogEvent($"{fighter.Type} docking for repairs");
                        repairLogTimer = 0;
                    }
                }
            }
            var capitalTypes = new[] { ShipType.Mothership, ShipType.Destroyer, ShipType.Battlecruiser,
                                       ShipType.Frigate, ShipType.ResourceCollector, ShipType.Carrier };
            foreach (var cap in Ships.Where(s => s.IsAlive && capitalTypes.Contains(s.Type)))
            {
                if (cap.HP >= cap.MaxHPValue) continue;
                cap.HP = Math.Min(cap.MaxHPValue, cap.HP + GameConstants.CapitalSelfRepairRate * dt);
            }
        }

        public bool TryStartResearch(ShipType type, out string error)
        {
            string nextLabel = Research.NextLevelLabel(type);
            int    cost      = Research.CostFor(type);
            int    resource  = PlayerResources;
            bool   ok        = Research.TryStart(type, ref resource, out error);
            PlayerResources  = resource;
            if (ok) LogEvent($"⚗ Researching {type} → {nextLabel}... Cost: {cost} res");
            return ok;
        }

        public bool TryEnqueueResearch(ShipType type, out string error)
        {
            int    cost      = Research.CostFor(type);
            string nextLabel = Research.NextLevelLabel(type);
            int    resource  = PlayerResources;
            bool   ok        = Research.TryEnqueue(type, ref resource, out error);
            PlayerResources  = resource;
            if (ok) LogEvent($"⚗ Queued {type} → {nextLabel}... Cost: {cost} res");
            return ok;
        }

        public bool TryDequeueResearch(ShipType type, out string error)
        {
            error = null;
            int resource = PlayerResources;
            bool ok = Research.TryDequeue(type, ref resource);
            PlayerResources = resource;
            if (ok) LogEvent($"⚗ Removed {type} from research queue (refunded)");
            else error = $"{type} is not in the research queue.";
            return ok;
        }

        public bool TryBuildShip(ShipType type)
        {
            int cost = GameConstants.BuildCosts[(int)type];
            if (PlayerResources < cost) return false;
            if (PlayerMothership.BuildQueue.Count >= 5) return false;

            // Fleet cap: count live ships + queued spawns for this type
            int cap         = GameConstants.FleetCaps[(int)type];
            int spawnCount  = type == ShipType.Interceptor || type == ShipType.Bomber ? 5
                            : type == ShipType.Corvette ? 3
                            : 1;
            int live        = Ships.Count(s => s.IsAlive && s.TeamId == 0 && s.Type == type);
            int queued      = PlayerMothership.BuildQueue.Count(q => q.Type == type) * spawnCount;
            if (live + queued + spawnCount > cap) return false;

            PlayerResources -= cost;
            PlayerMothership.BuildQueue.Add(new BuildOrder(type, 1.0f));
            LogEvent($"Building {type}... Cost: {cost} resources");
            return true;
        }

        // ── Carrier-specific build queue ──────────────────────────────────────

        private void ProcessCarrierBuildQueue(Carrier carrier, int deltaMs)
        {
            if (carrier.BuildQueue.Count == 0) return;
            var order = carrier.BuildQueue[0];
            order.Elapsed += deltaMs;
            if (!order.IsComplete) return;

            carrier.BuildQueue.RemoveAt(0);
            int count = order.Type == ShipType.Interceptor || order.Type == ShipType.Bomber ? 5
                      : order.Type == ShipType.Corvette ? 3
                      : 1;

            // Delta spawn formation above the carrier
            PointF[] spawnPositions = new PointF[count];
            if (count > 1)
            {
                PointF fwd    = new PointF(0f, -1f);
                PointF right  = new PointF(fwd.Y, -fwd.X);
                PointF center = new PointF(carrier.Position.X, carrier.Position.Y - 80f);

                const float rowSpacing = 15f;
                const float colSpacing = 15f;

                int idx = 0;
                for (int row = 0; idx < count; row++)
                {
                    int   shipsInRow = Math.Min(row + 1, count - idx);
                    float rear       = row * rowSpacing;
                    for (int i = 0; i < shipsInRow; i++, idx++)
                    {
                        float lat = shipsInRow == 1
                            ? 0f
                            : (-row / 2.0f + (float)i * row / (shipsInRow - 1)) * colSpacing;
                        spawnPositions[idx] = new PointF(
                            center.X + lat * right.X - rear * fwd.X,
                            center.Y + lat * right.Y - rear * fwd.Y);
                    }
                }
            }
            else
            {
                spawnPositions[0] = ShipFactory.RandomOffset(carrier.Position, 60);
            }

            for (int i = 0; i < count; i++)
            {
                var newShip = ShipFactory.Create(order.Type, spawnPositions[i], true);
                newShip.TeamId = carrier.TeamId;
                Research.ApplyTo(newShip);
                newShip.UpgradeLevel = Research.Levels[order.Type];
                Ships.Add(newShip);

                if (order.Type == ShipType.Worker)
                {
                    var miner   = (Miner)newShip;
                    var nearest = Asteroids.Where(a => a.IsAlive)
                                           .OrderBy(a => Dist(miner.Position, a.Position))
                                           .FirstOrDefault();
                    if (nearest != null) { miner.TargetAsteroid = nearest; miner.IsMining = true; }
                }
            }
            LogEvent($"Carrier built {order.Type}!{(count > 1 ? $" ×{count}" : "")}");
        }

        public bool TryBuildFromCarrier(Carrier carrier, ShipType type)
        {
            if (carrier == null || !carrier.IsAlive) return false;
            if (!System.Array.Exists(Carrier.CanBuild, t => t == type)) return false;

            int cost = GameConstants.BuildCosts[(int)type];
            if (PlayerResources < cost) return false;
            if (carrier.BuildQueue.Count >= 5) return false;

            int cap        = GameConstants.FleetCaps[(int)type];
            int spawnCount = type == ShipType.Interceptor || type == ShipType.Bomber ? 5
                           : type == ShipType.Corvette ? 3
                           : 1;
            int live   = Ships.Count(s => s.IsAlive && s.TeamId == 0 && s.Type == type);
            // Count queued from mothership + all carriers
            int queued = PlayerMothership.BuildQueue.Count(q => q.Type == type) * spawnCount;
            foreach (var cv in Ships.OfType<Carrier>().Where(c => c.IsAlive && c.TeamId == 0))
                queued += cv.BuildQueue.Count(q => q.Type == type) * spawnCount;
            if (live + queued + spawnCount > cap) return false;

            PlayerResources -= cost;
            carrier.BuildQueue.Add(new BuildOrder(type, 1.0f));
            LogEvent($"Carrier building {type}...  Cost: {cost} resources");
            return true;
        }

        public void AssignMiners(List<Miner> miners, Asteroid asteroid)
        {
            foreach (var m in miners)
            {
                m.TargetAsteroid      = asteroid;
                m.IsMining            = true;
                m.ReturningToMothership = false;
            }
        }

        public void AssignAttackTarget(List<Ship> attackers, Ship target)
        {
            foreach (var a in attackers)
            {
                a.AttackTarget        = target;
                a.Destination         = null;
                a.FormationWaypoint   = null;
                a.IsMining            = false;
                a.ReturningToMothership = false;
            }
            target.IsTargeted = true;
            LogEvent($"{attackers.Count} ship(s) ordered to attack {target.Type}!");
        }

        private PointF RandomSpawnPos(int xMin, int xMax, int yMin, int yMax,
                                       List<PointF> existing, float minSep = 600f)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var candidate = new PointF(rng.Next(xMin, xMax), rng.Next(yMin, yMax));
                bool ok = true;
                foreach (var p in existing)
                    if (Dist(candidate, p) < minSep) { ok = false; break; }
                if (ok) return candidate;
            }
            // Fallback: centre of the zone
            return new PointF((xMin + xMax) / 2f, (yMin + yMax) / 2f);
        }

        private float Dist(PointF a, PointF b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Calculate which ships each team can see based on vision radius.
        /// Called once per frame at start of GameWorld.Update().
        /// Ships always see their own team's ships; enemy ships visible only if within radar.
        /// </summary>
        private void UpdateVisibility()
        {
            // Clear previous visibility state
            foreach (var set in _visibleShips.Values)
                set.Clear();

            // For each ship, determine what it can see
            foreach (var observerShip in Ships.Where(s => s.IsAlive))
            {
                int observerTeam = observerShip.TeamId;
                float visionRadius = GameConstants.VisionRadius[(int)observerShip.Type];

                // Every ship sees all ships on its own team
                foreach (var allyShip in Ships.Where(s => s.IsAlive && s.TeamId == observerTeam))
                {
                    _visibleShips[observerTeam].Add(allyShip);
                }

                // Check what enemy ships are within vision radius
                foreach (var potentialTarget in Ships.Where(s => s.IsAlive && s.TeamId != observerTeam))
                {
                    float dx = potentialTarget.Position.X - observerShip.Position.X;
                    float dy = potentialTarget.Position.Y - observerShip.Position.Y;
                    float distSquared = dx * dx + dy * dy;
                    float radiusSquared = visionRadius * visionRadius;

                    if (distSquared <= radiusSquared)
                    {
                        // Enemy ship is within vision radius
                        _visibleShips[observerTeam].Add(potentialTarget);
                    }
                }
            }
        }

        /// <summary>
        /// Check if a ship is visible to a specific team.
        /// </summary>
        public bool IsShipVisible(Ship ship, int teamId)
        {
            if (ship == null || !ship.IsAlive) return false;
            return _visibleShips[teamId].Contains(ship);
        }

        /// <summary>
        /// Get the set of visible ships for a team.
        /// </summary>
        public HashSet<Ship> GetVisibleShips(int teamId) => _visibleShips[teamId];

        public void LogEvent(string msg)
        {
            EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
            if (EventLog.Count > 50) EventLog.RemoveAt(EventLog.Count - 1);
        }
    }
}
