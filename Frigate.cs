using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
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
}
