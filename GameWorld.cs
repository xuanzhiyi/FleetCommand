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

        public GameWorld(List<AiLevel> enemyLevels)
        {
            PlayerResources = GameConstants.StartingResources;
            InitializeMap(enemyLevels);
        }

        private void InitializeMap(List<AiLevel> enemyLevels)
        {
            // ── Player ────────────────────────────────────────────────────────
            PlayerMothership = new Mothership(new PointF(400, GameConstants.MapHeight / 2f), true);
            PlayerMothership.TeamId = 0;
            Ships.Add(PlayerMothership);

            // Starting miners for player
            for (int i = 0; i < GameConstants.StartingMiners; i++)
            {
                var m = (Miner)ShipFactory.Create(ShipType.Miner,
                    ShipFactory.RandomOffset(PlayerMothership.Position, 80), true);
                m.TeamId = 0;
                Ships.Add(m);
            }

            // ── Enemy AIs ─────────────────────────────────────────────────────
            // Place motherships evenly on the right half of the map
            int   count  = enemyLevels.Count;
            float startY = GameConstants.MapHeight * 0.2f;
            float stepY  = GameConstants.MapHeight * 0.6f / Math.Max(1, count - 1);
            if (count == 1) startY = GameConstants.MapHeight / 2f;

            for (int i = 0; i < count; i++)
            {
                float x  = GameConstants.MapWidth - 400;
                float y  = count == 1 ? startY : startY + stepY * i;
                int   tid = i + 1;

                var ms = new Mothership(new PointF(x, y), false);
                ms.TeamId = tid;
                Ships.Add(ms);

                // Starting miners for this AI
                for (int j = 0; j < GameConstants.StartingMiners; j++)
                {
                    var m = (Miner)ShipFactory.Create(ShipType.Miner,
                        ShipFactory.RandomOffset(ms.Position, 80), false);
                    m.TeamId = tid;
                    Ships.Add(m);
                }

                var ctrl = new EnemyController(ms, enemyLevels[i], i, GameConstants.StartingResources);
                Enemies.Add(ctrl);

                LogEvent($"Enemy {i + 1}: {enemyLevels[i]} AI");
            }

            // ── Asteroids ─────────────────────────────────────────────────────
            int asteroidCount = 40 + rng.Next(20);
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

            // ── Player collector receiving flag ───────────────────────────────
            foreach (var rc in Ships.OfType<ResourceCollector>()
                .Where(r => r.IsAlive && r.TeamId == 0))
                rc.IsReceiving = false;

            // ── Build queues ──────────────────────────────────────────────────
            ProcessBuildQueue(PlayerMothership, true, deltaMs);
            foreach (var e in Enemies)
                if (e.IsAlive) ProcessBuildQueue(e.Mothership, false, deltaMs, e);

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
            }

            // ── Enemy AI controllers ──────────────────────────────────────────
            foreach (var e in Enemies)
                if (e.IsAlive) e.Update(deltaMs, Ships, Asteroids, PlayerMothership);

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
            int count = (order.Type == ShipType.Interceptor || order.Type == ShipType.Bomber) ? 5 : 1;
            for (int i = 0; i < count; i++)
            {
                var spawnPos = ShipFactory.RandomOffset(ms.Position, 80);
                var newShip  = ShipFactory.Create(order.Type, spawnPos, isPlayer);
                newShip.TeamId = ms.TeamId;
                if (isPlayer)
                {
                    Research.ApplyTo(newShip);
                    newShip.UpgradeLevel = Research.Levels[order.Type];
                }
                Ships.Add(newShip);
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

            // Player ships fire at enemies
            foreach (var ps in playerShips)
            {
                if (ps.Type == ShipType.Miner || ps.Type == ShipType.ResourceCollector) continue;
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
                foreach (var es in allEnemyShips)
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
                if (es.Type == ShipType.Miner || es.Type == ShipType.ResourceCollector) continue;
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
                foreach (var ps in playerShips)
                {
                    if (ps.Type == ShipType.Miner || ps.Type == ShipType.ResourceCollector) continue;
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
            if (attackerType == ShipType.Bomber)
                kind = CombatEffectKind.TorpedoOrange;
            else if (!isPlayer)
                kind = CombatEffectKind.LaserRed;
            else if (attackerType == ShipType.Interceptor || attackerType == ShipType.Corvet)
                kind = CombatEffectKind.LaserGreen;
            else
                kind = CombatEffectKind.PlasmaBlue;
            var jitter = new PointF(
                to.X + (float)(rng.NextDouble() - 0.5) * 6,
                to.Y + (float)(rng.NextDouble() - 0.5) * 6);
            CombatEffects.Add(new CombatEffect(from, jitter, kind));
        }

        private void ResolveRepairs(int deltaMs)
        {
            float dt = deltaMs / 1000f;
            var dockStations = Ships.Where(s => s.IsAlive &&
                (s.Type == ShipType.Mothership || s.Type == ShipType.Battlecruiser)).ToList();
            var lightShips = Ships.Where(s => s.IsAlive &&
                (s.Type == ShipType.Interceptor || s.Type == ShipType.Bomber ||
                 s.Type == ShipType.Miner       || s.Type == ShipType.Corvet)).ToList();

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
            var capitalTypes = new[] { ShipType.Mothership, ShipType.Destroyer, ShipType.Battlecruiser, ShipType.Frigate, ShipType.ResourceCollector };
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

        public bool TryBuildShip(ShipType type)
        {
            int cost = GameConstants.BuildCosts[(int)type];
            if (PlayerResources < cost) return false;
            if (PlayerMothership.BuildQueue.Count >= 5) return false;

            // Fleet cap: count live ships + queued spawns for this type
            int cap         = GameConstants.FleetCaps[(int)type];
            int spawnCount  = (type == ShipType.Interceptor || type == ShipType.Bomber) ? 5 : 1;
            int live        = Ships.Count(s => s.IsAlive && s.TeamId == 0 && s.Type == type);
            int queued      = PlayerMothership.BuildQueue.Count(q => q.Type == type) * spawnCount;
            if (live + queued + spawnCount > cap) return false;

            PlayerResources -= cost;
            PlayerMothership.BuildQueue.Add(new BuildOrder(type, 1.0f));
            LogEvent($"Building {type}... Cost: {cost} resources");
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
                a.IsMining            = false;
                a.ReturningToMothership = false;
            }
            target.IsTargeted = true;
            LogEvent($"{attackers.Count} ship(s) ordered to attack {target.Type}!");
        }

        private float Dist(PointF a, PointF b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void LogEvent(string msg)
        {
            EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
            if (EventLog.Count > 50) EventLog.RemoveAt(EventLog.Count - 1);
        }
    }
}
