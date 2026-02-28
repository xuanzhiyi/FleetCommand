using System;
using System.Collections.Generic;
using System.Drawing;

namespace FleetCommand
{
    /// <summary>
    /// Probe: Fast, lightly-armored scout with excellent vision.
    /// Limited to 3 total movements before self-destructing.
    /// Can pinpoint enemy locations for coordinated attacks.
    /// </summary>
    public class Probe : Ship
    {
        private int _movementCount = 0;  // Tracks how many times destination has been set
        private const int MaxMovements = 3;

        public int MovementCount => _movementCount;
        public bool HasMovementsRemaining => _movementCount < MaxMovements;

        public Probe(PointF position, bool isPlayer) : base(ShipType.Probe, position, isPlayer)
        {
            Label = "probe";
        }

        public override void Update(List<Ship> allShips, List<Asteroid> asteroids, Ship playerMothership)
        {
            if (!IsAlive) return;

            // Self-destruct if max movements exceeded
            if (_movementCount >= MaxMovements)
            {
                Speed = 0;
                return;
            }

            // Simple movement logic: just move toward destination
            MoveTowardDestination();

            // Standard attack behavior (if probe encounters enemy within attack range)
            if (AttackTarget?.IsAlive == true)
            {
                float targetHeading = GetHeadingToward(AttackTarget.Position);
                Heading = RotateToward(Heading, targetHeading, GetRotationSpeed(), DeltaMs);
            }
        }

        /// <summary>
        /// Override destination setter to track movement count.
        /// Each time a new destination is set, increment movement counter.
        /// </summary>
        public new PointF? Destination
        {
            get => base.Destination;
            set
            {
                // Only increment if setting a NEW destination (not null/same destination)
                if (value.HasValue && (!base.Destination.HasValue ||
                    Distance(value.Value, base.Destination.Value) > 1f))
                {
                    _movementCount++;

                    // Self-destruct if exceeded max movements
                    if (_movementCount > MaxMovements)
                    {
                        Speed = 0;
                    }
                }
                base.Destination = value;
            }
        }

        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            if (!IsAlive) return;

            float sx = (Position.X + offset.X) * zoom;
            float sy = (Position.Y + offset.Y) * zoom;
            float r = 6 * zoom;  // Small radius
            var color = GetShipColor();

            // Draw as a small diamond (probe shape)
            var pts = new PointF[]
            {
                new PointF(sx, sy - r),           // Top
                new PointF(sx + r, sy),           // Right
                new PointF(sx, sy + r),           // Bottom
                new PointF(sx - r, sy)            // Left
            };

            using (var brush = new SolidBrush(Color.FromArgb(120, color)))
                g.FillPolygon(brush, pts);

            using (var pen = new Pen(color, IsSelected ? 2f : 1f))
                g.DrawPolygon(pen, pts);

            // Draw movement counter indicator
            if (_movementCount > 0)
            {
                using (var pen = new Pen(Color.Orange, 1f))
                {
                    // Draw small dots to indicate remaining movements
                    float dotR = 3 * zoom;
                    for (int i = 0; i < MaxMovements; i++)
                    {
                        float dx = (i - 1) * 8 * zoom;
                        Color dotColor = i < _movementCount ? Color.Orange : Color.Gray;
                        using (var dotPen = new Pen(dotColor, 1f))
                            g.DrawEllipse(dotPen, sx + dx - dotR, sy + r + 4 * zoom, dotR * 2, dotR * 2);
                    }
                }
            }

            DrawHealthBar(g, new RectangleF(sx - r, sy - r, r * 2, r * 2));
            DrawLabel(g, new PointF(sx, sy), r);
        }

        private static float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
