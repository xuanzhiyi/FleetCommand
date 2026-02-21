using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
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
}
