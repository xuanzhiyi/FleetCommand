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
}
