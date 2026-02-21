using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
    public abstract class Ship
    {
        public ShipType  Type          { get; protected set; }
        public PointF    Position      { get; set; }
        public PointF?   Destination   { get; set; }
        public float     HP            { get; set; }
        public float     MaxHPValue    { get; set; }
        public int       UpgradeLevel  { get; set; }
        public void      ApplySpeedMultiplier(float mult) { Speed *= mult; }
        public bool      IsSelected    { get; set; }
        public bool      IsPlayerOwned { get; set; }
        public string    Label         { get; protected set; }
        public float     Speed         { get; protected set; }
        public bool      IsAlive       => HP > 0;
        public float     Damage        { get; set; }

        // For miners
        public virtual bool     IsMining              { get; set; }
        public virtual Asteroid TargetAsteroid        { get; set; }
        public virtual bool     ReturningToMothership { get; set; }
        public virtual int      CargoHeld             { get; set; }
        public virtual int      CargoCapacity         { get; } = 0;

        // Combat targeting
        public Ship AttackTarget { get; set; }

        // Repair state — set by GameWorld each tick
        public bool IsDocking { get; set; }

        // Set by GameWorld before each Update loop so subclasses can use real elapsed time
        public int DeltaMs { get; set; }

        // Team ownership: 0 = player, 1/2/3 = enemy AI index
        public int TeamId { get; set; } = 0;

        protected Ship(ShipType type, PointF position, bool isPlayer)
        {
            Type          = type;
            Position      = position;
            IsPlayerOwned = isPlayer;
            HP            = GameConstants.MaxHP[(int)type];
            Damage        = GameConstants.Damage[(int)type];
            MaxHPValue    = HP;
            Speed         = GameConstants.Speeds[(int)type];
            Label         = type.ToString();
        }

        public virtual void Update(List<Ship> allShips, List<Asteroid> asteroids, Ship playerMothership)
        {
            if (!IsAlive) return;

            if (AttackTarget != null)
            {
                if (!AttackTarget.IsAlive)
                {
                    AttackTarget = null;
                }
                else
                {
                    float range = GetAttackRange();
                    float dx    = AttackTarget.Position.X - Position.X;
                    float dy    = AttackTarget.Position.Y - Position.Y;
                    float dist  = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (dist > range * 0.85f)
                    {
                        Position = new PointF(
                            Position.X + dx / dist * Speed,
                            Position.Y + dy / dist * Speed);
                    }
                    return;
                }
            }

            MoveTowardDestination();
        }

        public float GetDamage() => Damage;

        public float GetAttackRange()
        {
            switch (Type)
            {
                case ShipType.Interceptor:   return 40;
                case ShipType.Bomber:        return 60;
                case ShipType.Corvet:        return 55;
                case ShipType.Frigate:       return 70;
                case ShipType.Destroyer:     return 90;
                case ShipType.Battlecruiser: return 120;
                case ShipType.Mothership:    return 80;
                default:                     return 0;
            }
        }

        protected void MoveTowardDestination()
        {
            if (!Destination.HasValue) return;
            float dx   = Destination.Value.X - Position.X;
            float dy   = Destination.Value.Y - Position.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            if (dist < Speed)
            {
                Position    = Destination.Value;
                Destination = null;
            }
            else
            {
                Position = new PointF(Position.X + dx / dist * Speed, Position.Y + dy / dist * Speed);
            }
        }

        public abstract void Draw(Graphics g, PointF offset, float zoom);

        /// <summary>Returns the canonical colour for a given team slot.</summary>
        /// <param name="teamId">0 = player, 1/2/3 = Computer 1/2/3.</param>
        public static Color GetTeamColor(int teamId)
        {
            switch (teamId)
            {
                case 0:  return Color.LimeGreen;                      // Player
                case 1:  return Color.OrangeRed;                      // Computer 1
                case 2:  return Color.FromArgb(175, 105, 255);        // Computer 2 — purple
                case 3:  return Color.FromArgb(255, 200,  40);        // Computer 3 — amber
                default: return Color.Gray;
            }
        }

        protected Color GetShipColor()
        {
            if (!IsAlive)       return Color.Gray;
            if (IsPlayerOwned)  return IsSelected ? Color.Cyan : Color.LimeGreen;
            // Each computer player gets its own distinct colour; yellow flash when targeted
            return IsTargeted ? Color.Yellow : GetTeamColor(TeamId);
        }

        public bool IsTargeted { get; set; }

        protected void DrawHealthBar(Graphics g, RectangleF bounds)
        {
            float pct    = HP / MaxHPValue;
            var   bgRect = new RectangleF(bounds.X, bounds.Bottom + 3, bounds.Width, 4);
            var   hpRect = new RectangleF(bounds.X, bounds.Bottom + 3, bounds.Width * pct, 4);
            g.FillRectangle(Brushes.DarkRed, bgRect);
            g.FillRectangle(pct > 0.5f ? Brushes.LimeGreen : pct > 0.25f ? Brushes.Yellow : Brushes.Red, hpRect);
        }

        protected void DrawLabel(Graphics g, PointF screenPos, float size)
        {
            // Prefix enemy ships with their AI slot so the player can tell Ai1/Ai2/Ai3 apart.
            string owner  = IsPlayerOwned ? "" : $"Ai{TeamId} · ";
            string body   = UpgradeLevel > 0 ? $"{Label} [Mk.{UpgradeLevel}]" : Label;
            string text   = owner + body;

            // Label colour: player stays LightGreen/Cyan; each computer gets its team colour.
            Color labelColor = IsPlayerOwned
                ? (UpgradeLevel > 0 ? Color.Cyan : Color.LightGreen)
                : GetTeamColor(TeamId);

            using (var font  = new Font("Arial", Math.Max(6, 7 / size), FontStyle.Regular))
            using (var brush = new SolidBrush(labelColor))
            {
                SizeF ts = g.MeasureString(text, font);
                g.DrawString(text, font, brush,
                    screenPos.X - ts.Width / 2, screenPos.Y - size - ts.Height - 2);
            }
        }

        public RectangleF GetScreenBounds(PointF offset, float zoom, float radius)
        {
            float sx = (Position.X + offset.X) * zoom;
            float sy = (Position.Y + offset.Y) * zoom;
            return new RectangleF(sx - radius * zoom, sy - radius * zoom, radius * 2 * zoom, radius * 2 * zoom);
        }

        public bool HitTest(PointF worldPoint, float radius)
        {
            float dx = worldPoint.X - Position.X;
            float dy = worldPoint.Y - Position.Y;
            return Math.Sqrt(dx * dx + dy * dy) <= radius;
        }
    }

    // ── Mothership (oval) ──────────────────────────────────────────────────────
    public class Mothership : Ship
    {
        public List<BuildOrder> BuildQueue { get; } = new List<BuildOrder>();

        public Mothership(PointF position, bool isPlayer) : base(ShipType.Mothership, position, isPlayer)
        {
            Label = "Mothership";
        }

        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx = (Position.X + offset.X) * zoom;
            float sy = (Position.Y + offset.Y) * zoom;
            float rx = 40 * zoom, ry = 25 * zoom;
            var   color = GetShipColor();
            using (var pen   = new Pen(color, IsSelected ? 2.5f : 1.5f))
            using (var brush = new SolidBrush(Color.FromArgb(60, color)))
            {
                g.FillEllipse(brush, sx - rx, sy - ry, rx * 2, ry * 2);
                g.DrawEllipse(pen,   sx - rx, sy - ry, rx * 2, ry * 2);
                // Engine glow
                var glowColor = Destination.HasValue
                    ? Color.FromArgb(160, Color.Cyan)
                    : Color.FromArgb(80, Color.Cyan);
                g.FillEllipse(new SolidBrush(glowColor), sx - rx * 0.35f, sy + ry * 0.25f, rx * 0.7f, ry * 0.7f);
            }
            // Docking ring when fighters are repairing
            if (IsDocking)
            {
                float dr = (GameConstants.DockRange + 8) * zoom;
                using (var pen = new Pen(Color.FromArgb(140, Color.LimeGreen), 1.5f) { DashStyle = DashStyle.Dot })
                    g.DrawEllipse(pen, sx - dr, sy - dr, dr * 2, dr * 2);
            }
            DrawHealthBar(g, new RectangleF(sx - rx, sy - ry, rx * 2, ry * 2));
            DrawLabel(g, new PointF(sx, sy), ry);
        }
    }

    // ── Miner (circle) ────────────────────────────────────────────────────────
    public class Miner : Ship
    {
        public override int CargoCapacity => 100;

        // Delay timers in milliseconds
        private const int MiningDelayMs  = 4000;
        private const int OffloadDelayMs = 4000;

        private int miningTimer  = 0;   // counts up while stationary at asteroid
        private int offloadTimer = 0;   // counts up while stationary at mothership
        public  int  PendingDeposit  { get; set; }  // set when offload completes, GameWorld reads & clears
        public float MiningProgress  => IsMining && miningTimer  > 0 ? (float)miningTimer  / MiningDelayMs  : 0f;
        public float OffloadProgress => ReturningToMothership && IsOffloading ? (float)offloadTimer / OffloadDelayMs : 0f;
        public bool  IsOffloading    { get; private set; }

        public Miner(PointF position, bool isPlayer) : base(ShipType.Miner, position, isPlayer)
        {
            Label = "worker";
        }

        public override void Update(List<Ship> allShips, List<Asteroid> asteroids, Ship playerMothership)
        {
            if (!IsAlive) return;

            if (AttackTarget != null)
            {
                base.Update(allShips, asteroids, playerMothership);
                return;
            }

            // Each miner returns to its own team's mothership, not always the player's.
            Ship myMothership = IsPlayerOwned ? playerMothership : FindOwnMothership(allShips);

            if (IsMining && TargetAsteroid != null)
            {
                if (!TargetAsteroid.IsAlive) { TargetAsteroid = null; IsMining = false; miningTimer = 0; return; }

                float dist = Distance(Position, TargetAsteroid.Position);
                if (dist > 30)
                {
                    miningTimer = 0; // reset if not yet at asteroid
                    Destination = TargetAsteroid.Position;
                    MoveTowardDestination();
                }
                else
                {
                    // At asteroid — count up mining delay
                    miningTimer += DeltaMs;
                    if (miningTimer >= MiningDelayMs)
                    {
                        miningTimer = 0;
                        int mined = Math.Min(CargoCapacity,
                            Math.Min(TargetAsteroid.Resources, CargoCapacity - CargoHeld));
                        CargoHeld                += mined;
                        TargetAsteroid.Resources -= mined;
                        if (CargoHeld >= CargoCapacity || TargetAsteroid.Resources <= 0)
                        {
                            ReturningToMothership = true;
                            IsOffloading          = false;
                            IsMining              = false;
                            offloadTimer          = 0;
                            if (myMothership != null)
                                Destination = myMothership.Position;
                        }
                    }
                }
            }
            else if (ReturningToMothership && myMothership != null)
            {
                // Go to nearest collector (if one exists nearby) or mothership
                Ship offloadAt = FindOffloadTarget(allShips, myMothership);
                Destination = offloadAt.Position;
                MoveTowardDestination();

                float offloadRange = offloadAt is ResourceCollector
                    ? ResourceCollector.OffloadRange
                    : 50;

                if (Distance(Position, offloadAt.Position) < offloadRange)
                {
                    IsOffloading  = true;
                    offloadTimer += DeltaMs;
                    if (offloadTimer >= OffloadDelayMs)
                    {
                        offloadTimer          = 0;
                        IsOffloading          = false;
                        ReturningToMothership = false;
                        PendingDeposit        = CargoHeld;  // GameWorld credits instantly
                        CargoHeld             = 0;
                        if (offloadAt is ResourceCollector rc) rc.IsReceiving = true;
                    }
                }
                else
                {
                    offloadTimer = 0;
                    IsOffloading = false;
                }
            }
            else
            {
                MoveTowardDestination();
            }
        }

        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx = (Position.X + offset.X) * zoom;
            float sy = (Position.Y + offset.Y) * zoom;
            float r  = 10 * zoom;
            var   color = GetShipColor();
            using (var pen   = new Pen(color, IsSelected ? 2f : 1f))
            using (var brush = new SolidBrush(Color.FromArgb(50, color)))
            {
                g.FillEllipse(brush, sx - r, sy - r, r * 2, r * 2);
                g.DrawEllipse(pen,   sx - r, sy - r, r * 2, r * 2);
                if (IsMining)
                    g.DrawEllipse(new Pen(Color.Yellow, 1), sx - r - 3, sy - r - 3, (r + 3) * 2, (r + 3) * 2);
            }

            // Mining progress arc (yellow) — sweeps around as the 4s delay counts up
            float mp = MiningProgress;
            if (mp > 0f)
            {
                float ar = r + 5;
                using (var pen = new Pen(Color.Yellow, 2f))
                    g.DrawArc(pen, sx - ar, sy - ar, ar * 2, ar * 2, -90, 360f * mp);
            }

            // Offload progress arc (cyan) — sweeps around while docked at mothership
            float op = OffloadProgress;
            if (op > 0f)
            {
                float ar = r + 5;
                using (var pen = new Pen(Color.Cyan, 2f))
                    g.DrawArc(pen, sx - ar, sy - ar, ar * 2, ar * 2, -90, 360f * op);
            }

            // Green repair glow when docking
            if (IsDocking && HP < MaxHPValue)
            {
                using (var pen = new Pen(Color.FromArgb(180, Color.LimeGreen), 1.5f))
                    g.DrawEllipse(pen, sx - r - 3, sy - r - 3, (r + 3) * 2, (r + 3) * 2);
            }
            DrawHealthBar(g, new RectangleF(sx - r, sy - r, r * 2, r * 2));
            DrawLabel(g, new PointF(sx, sy), r);
        }

        private Ship FindOwnMothership(List<Ship> allShips)
        {
            foreach (var ship in allShips)
                if (ship is Mothership && ship.TeamId == TeamId && ship.IsAlive)
                    return ship;
            return null;
        }

        private Ship FindOffloadTarget(List<Ship> allShips, Ship myMothership)
        {
            Ship  best     = myMothership;
            float bestDist = Distance(Position, myMothership.Position);
            foreach (var ship in allShips)
            {
                if (!(ship is ResourceCollector rc)) continue;
                if (!rc.IsAlive || rc.TeamId != TeamId) continue;
                float d = Distance(Position, rc.Position);
                if (d < bestDist) { bestDist = d; best = rc; }
            }
            return best;
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }

    // ── Fighter base (triangle) ───────────────────────────────────────────────
    public abstract class TriangleShip : Ship
    {
        protected float Size;

        public TriangleShip(ShipType type, PointF position, bool isPlayer, float size)
            : base(type, position, isPlayer)
        {
            Size = size;
        }

        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx = (Position.X + offset.X) * zoom;
            float sy = (Position.Y + offset.Y) * zoom;
            float s  = Size * zoom;
            float w  = 10 * zoom, h = 7 * zoom;
            var   color = GetShipColor();
            var pts = new PointF[]
            {
                new PointF(sx - w, sy + h),
                new PointF(sx + w, sy),
                new PointF(sx - w, sy - h)
            };
            using (var pen   = new Pen(color, IsSelected ? 2f : 1f))
            using (var brush = new SolidBrush(Color.FromArgb(50, color)))
            {
                g.FillPolygon(brush, pts);
                g.DrawPolygon(pen, pts);
            }
            // Green repair glow when docking
            if (IsDocking && HP < MaxHPValue)
            {
                using (var pen = new Pen(Color.FromArgb(180, Color.LimeGreen), 1.5f))
                    g.DrawEllipse(pen, sx - s - 3, sy - s - 3, (s + 3) * 2, (s + 3) * 2);
            }
            DrawHealthBar(g, new RectangleF(sx - s, sy - s, s * 2, s * 2));
            DrawLabel(g, new PointF(sx, sy), s);
        }
    }

    public class Interceptor : TriangleShip
    {
        public Interceptor(PointF position, bool isPlayer) : base(ShipType.Interceptor, position, isPlayer, 8)  { Label = "Interceptor"; }
    }

    public class Bomber : TriangleShip
    {
        public Bomber(PointF position, bool isPlayer)      : base(ShipType.Bomber,      position, isPlayer, 10) { Label = "Bomber"; }
    }

    // ── Corvet (square) ───────────────────────────────────────────────────────
    public class Corvet : Ship
    {
        public Corvet(PointF position, bool isPlayer) : base(ShipType.Corvet, position, isPlayer) { Label = "Corvet"; }

        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx = (Position.X + offset.X) * zoom;
            float sy = (Position.Y + offset.Y) * zoom;
            float s  = 12 * zoom;
            var   color = GetShipColor();
            var   rect  = new RectangleF(sx - s, sy - s, s * 2, s * 2);
            using (var pen   = new Pen(color, IsSelected ? 2f : 1f))
            using (var brush = new SolidBrush(Color.FromArgb(50, color)))
            {
                g.FillRectangle(brush, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }
            // Green repair glow when docking
            if (IsDocking && HP < MaxHPValue)
            {
                using (var pen = new Pen(Color.FromArgb(180, Color.LimeGreen), 1.5f))
                    g.DrawEllipse(pen, sx - s - 3, sy - s - 3, (s + 3) * 2, (s + 3) * 2);
            }
            DrawHealthBar(g, rect);
            DrawLabel(g, new PointF(sx, sy), s);
        }
    }

    // ── Frigate (diamond) ─────────────────────────────────────────────────────
    public class Frigate : Ship
    {
        public Frigate(PointF position, bool isPlayer) : base(ShipType.Frigate, position, isPlayer) { Label = "Frigate"; }

        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx = (Position.X + offset.X) * zoom;
            float sy = (Position.Y + offset.Y) * zoom;
            float w  = 22 * zoom, h = 12 * zoom;
            var   color = GetShipColor();
            var pts = new PointF[]
            {
                new PointF(sx,     sy + h),
                new PointF(sx + w, sy),
                new PointF(sx,     sy - h),
                new PointF(sx - w, sy)
            };
            using (var pen   = new Pen(color, IsSelected ? 2f : 1f))
            using (var brush = new SolidBrush(Color.FromArgb(50, color)))
            {
                g.FillPolygon(brush, pts);
                g.DrawPolygon(pen, pts);
            }
            DrawHealthBar(g, new RectangleF(sx - w, sy - h, w * 2, h * 2));
            DrawLabel(g, new PointF(sx, sy), h);
        }
    }

    // ── Destroyer (parallelogram) ─────────────────────────────────────────────
    public class Destroyer : Ship
    {
        public Destroyer(PointF position, bool isPlayer) : base(ShipType.Destroyer, position, isPlayer) { Label = "Destroyer"; }

        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx   = (Position.X + offset.X) * zoom;
            float sy   = (Position.Y + offset.Y) * zoom;
            float w    = 25 * zoom, h = 14 * zoom, skew = 8 * zoom;
            var   color = GetShipColor();
            var pts = new PointF[]
            {
                new PointF(sx - w + skew, sy - h),
                new PointF(sx + w + skew, sy - h),
                new PointF(sx + w - skew, sy + h),
                new PointF(sx - w - skew, sy + h)
            };
            using (var pen   = new Pen(color, IsSelected ? 2f : 1f))
            using (var brush = new SolidBrush(Color.FromArgb(50, color)))
            {
                g.FillPolygon(brush, pts);
                g.DrawPolygon(pen, pts);
            }
            DrawHealthBar(g, new RectangleF(sx - w, sy - h, w * 2, h * 2));
            DrawLabel(g, new PointF(sx, sy), h);
        }
    }

    // ── Battlecruiser (hexagon-ish) ───────────────────────────────────────────
    public class Battlecruiser : Ship
    {
        public Battlecruiser(PointF position, bool isPlayer) : base(ShipType.Battlecruiser, position, isPlayer) { Label = "Battlecruiser"; }

        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx   = (Position.X + offset.X) * zoom;
            float sy   = (Position.Y + offset.Y) * zoom;
            float w    = 35 * zoom, h = 18 * zoom, skew = 12 * zoom;
            var   color = GetShipColor();
            var pts = new PointF[]
            {
                new PointF(sx - w - skew, sy + h),
                new PointF(sx + w - skew, sy + h),
                new PointF(sx + w + skew, sy),
                new PointF(sx + w - skew, sy - h),
                new PointF(sx - w - skew, sy - h),
                new PointF(sx - w + skew, sy),
                new PointF(sx - w - skew, sy + h),
            };
            using (var pen   = new Pen(color, IsSelected ? 2.5f : 1.5f))
            using (var brush = new SolidBrush(Color.FromArgb(60, color)))
            {
                g.FillPolygon(brush, pts);
                g.DrawPolygon(pen, pts);
                g.DrawLine(new Pen(Color.FromArgb(100, color), 1),
                    new PointF(sx - w * 0.5f + skew * 0.5f, sy - h),
                    new PointF(sx - w * 0.5f - skew * 0.5f, sy + h));
                g.DrawLine(new Pen(Color.FromArgb(100, color), 1),
                    new PointF(sx + w * 0.5f + skew * 0.5f, sy - h),
                    new PointF(sx + w * 0.5f - skew * 0.5f, sy + h));
            }
            // Docking ring when repairing fighters
            if (IsDocking)
            {
                float dr = (GameConstants.DockRange + 8) * zoom;
                using (var pen = new Pen(Color.FromArgb(140, Color.LimeGreen), 1.5f) { DashStyle = DashStyle.Dot })
                    g.DrawEllipse(pen, sx - dr, sy - dr, dr * 2, dr * 2);
            }
            DrawHealthBar(g, new RectangleF(sx - w, sy - h, w * 2, h * 2));
            DrawLabel(g, new PointF(sx, sy), h);
        }
    }

    // ── Resource Collector ────────────────────────────────────────────────────
    // Acts like a second mothership for resource offloading. Miners detect it
    // as a valid offload point (closer than mothership = faster trips).
    // Resources credit to the global pool instantly when a miner offloads here.
    // The collector itself drifts slowly toward the centroid of active asteroids
    // so it stays near the mining field. It cannot attack or be repaired.
    public class ResourceCollector : Ship
    {
        public const int OffloadRange = 60;  // miners offload within this radius

        // Visual pulse when a miner is actively offloading nearby
        public bool IsReceiving { get; set; }  // set by GameWorld each tick

        public ResourceCollector(PointF position, bool isPlayer)
            : base(ShipType.ResourceCollector, position, isPlayer)
        {
            Label = "Collector";
        }

        public override void Update(List<Ship> allShips, List<Asteroid> asteroids, Ship playerMothership)
        {
            if (!IsAlive) return;

            MoveTowardDestination();
        }

        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx = (Position.X + offset.X) * zoom;
            float sy = (Position.Y + offset.Y) * zoom;
            float w  = 20 * zoom, h = 14 * zoom;
            var   col = GetShipColor();

            // Hexagonal tanker hull
            var pts = new PointF[]
            {
                new PointF(sx - w,        sy),
                new PointF(sx - w * 0.5f, sy - h),
                new PointF(sx + w * 0.5f, sy - h),
                new PointF(sx + w,        sy),
                new PointF(sx + w * 0.5f, sy + h),
                new PointF(sx - w * 0.5f, sy + h),
            };

            using (var pen   = new Pen(col, IsSelected ? 2.5f : 1.5f))
            using (var brush = new SolidBrush(Color.FromArgb(40, col)))
            {
                g.FillPolygon(brush, pts);
                g.DrawPolygon(pen, pts);
                // Inner tank ring
                g.DrawEllipse(new Pen(Color.FromArgb(80, col), 1f),
                    sx - w * 0.45f, sy - h * 0.55f, w * 0.9f, h * 1.1f);
            }

            // Gold receiving pulse when miners are offloading
            if (IsReceiving)
            {
                float pr = (OffloadRange + 6) * zoom;
                using (var pen = new Pen(Color.FromArgb(140, Color.Gold), 1.5f) { DashStyle = DashStyle.Dot })
                    g.DrawEllipse(pen, sx - pr, sy - pr, pr * 2, pr * 2);
                using (var pen = new Pen(Color.FromArgb(80, Color.Gold), 1f))
                    g.DrawEllipse(pen, sx - w * 0.5f, sy - h * 0.6f, w, h * 1.2f);
            }

            DrawHealthBar(g, new RectangleF(sx - w, sy - h, w * 2, h * 2));
            DrawLabel(g, new PointF(sx, sy), h);
        }

        private float Dist(PointF a, PointF b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }

    // ── Build Order ───────────────────────────────────────────────────────────
    public class BuildOrder
    {
        public ShipType Type      { get; }
        public int      TotalTime { get; }
        public int      Elapsed   { get; set; }
        public bool     IsComplete => Elapsed >= TotalTime;
        public float    Progress   => (float)Elapsed / TotalTime;

        public BuildOrder(ShipType type, float difficultyMult)
        {
            Type      = type;
            TotalTime = (int)(GameConstants.BuildTimes[(int)type] / difficultyMult);
        }
    }

    // ── Ship Factory ──────────────────────────────────────────────────────────
    public static class ShipFactory
    {
        private static readonly Random rng = new Random();

        public static Ship Create(ShipType type, PointF position, bool isPlayer)
        {
            switch (type)
            {
                case ShipType.Mothership:         return new Mothership(position, isPlayer);
                case ShipType.Miner:              return new Miner(position, isPlayer);
                case ShipType.Interceptor:        return new Interceptor(position, isPlayer);
                case ShipType.Bomber:             return new Bomber(position, isPlayer);
                case ShipType.Corvet:             return new Corvet(position, isPlayer);
                case ShipType.Frigate:            return new Frigate(position, isPlayer);
                case ShipType.Destroyer:          return new Destroyer(position, isPlayer);
                case ShipType.Battlecruiser:      return new Battlecruiser(position, isPlayer);
                case ShipType.ResourceCollector:  return new ResourceCollector(position, isPlayer);
                default:                          return new Miner(position, isPlayer);
            }
        }

        public static PointF RandomOffset(PointF center, float radius)
        {
            double angle = rng.NextDouble() * Math.PI * 2;
            float  dist  = (float)(rng.NextDouble() * radius + radius * 0.5f);
            return new PointF(
                center.X + (float)Math.Cos(angle) * dist,
                center.Y + (float)Math.Sin(angle) * dist);
        }
    }
}
