using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
    // ── Corvet (square) ───────────────────────────────────────────────────────
    public class Corvet : Ship
    {
        public Corvet(PointF position, bool isPlayer) : base(ShipType.Corvet, position, isPlayer) { Label = "Corvet"; }

        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx = (Position.X + offset.X) * zoom;
            float sy = (Position.Y + offset.Y) * zoom;
            float s  = 9 * zoom;
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
}
