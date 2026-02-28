using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
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

            // Update heading toward movement target
            if (Destination.HasValue)
            {
                float targetHeading = GetHeadingToward(Destination.Value);
                Heading = RotateToward(Heading, targetHeading, GetRotationSpeed(), DeltaMs);
            }

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
}
