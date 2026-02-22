using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
    // ── Battlecruiser (hexagon-ish / sprite) ─────────────────────────────────
    public class Battlecruiser : Ship
    {
        public Battlecruiser(PointF position, bool isPlayer) : base(ShipType.Battlecruiser, position, isPlayer) { Label = "Battlecruiser"; }

        // ── Sprite support ────────────────────────────────────────────────────
        private static Bitmap _sprite;
        private static readonly object _spriteLock = new object();

        private static Bitmap GetSprite()
        {
            if (_sprite != null) return _sprite;
            lock (_spriteLock)
            {
                if (_sprite != null) return _sprite;
                try { _sprite = new Bitmap(Properties.Resources.Battlecruiser2); }
                catch { _sprite = null; }   // falls back to polygon if image missing
            }
            return _sprite;
        }

        private const float SpriteZoom = 0.9f;

        // ─────────────────────────────────────────────────────────────────────
        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx    = (Position.X + offset.X) * zoom;
            float sy    = (Position.Y + offset.Y) * zoom;
            float w     = 35 * zoom, h = 18 * zoom, skew = 12 * zoom;
            var   color = GetShipColor();

            // Polygon points — used for the outline overlay and the fallback shape
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

            if (zoom >= SpriteZoom)
            {
                // ── Sprite mode ───────────────────────────────────────────────
                var sprite = GetSprite();
                if (sprite != null)
                {
                    // Sprite sized to the full polygon width (w + skew on each side)
                    float sz   = (w + skew) * 1.9f;
                    var   dest = new Rectangle((int)(sx - sz * 0.5f),
                                               (int)(sy - sz * 0.4f),
                                               (int)sz, (int)(sz * 0.8f));

                    using (var ia = new System.Drawing.Imaging.ImageAttributes())
                    {
                        ia.SetColorKey(Color.FromArgb(0, 0, 0),
                                       Color.FromArgb(20, 20, 20));
                        g.DrawImage(sprite, dest,
                                    0, 0, sprite.Width, sprite.Height,
                                    GraphicsUnit.Pixel, ia);
                    }
                }

                // Team-coloured polygon outline over the sprite
                int ringAlpha = IsSelected ? 220 : 90;
                using (var pen = new Pen(Color.FromArgb(ringAlpha, color),
                                         IsSelected ? 2.5f : 1.5f))
                    g.DrawPolygon(pen, pts);
            }
            else
            {
                // ── Polygon mode (zoomed out) ─────────────────────────────────
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
}
