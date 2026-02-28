using System;
using System.Collections.Generic;
using System.Drawing;

namespace FleetCommand
{
    /// <summary>
    /// Probe: Fast, lightly-armored scout with excellent vision.
    /// Can move freely like other ships - no movement restrictions.
    /// </summary>
    public class Probe : Ship
    {
        public Probe(PointF position, bool isPlayer) : base(ShipType.Probe, position, isPlayer)
        {
            Label = "probe";
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

            DrawHealthBar(g, new RectangleF(sx - r, sy - r, r * 2, r * 2));
            DrawLabel(g, new PointF(sx, sy), r);
        }
    }
}
