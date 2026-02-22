using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
    // ── Destroyer (parallelogram / sprite) ────────────────────────────────────
    public class Destroyer : Ship
    {
        public Destroyer(PointF position, bool isPlayer) : base(ShipType.Destroyer, position, isPlayer) { Label = "Destroyer"; }

        // ── Sprite support ────────────────────────────────────────────────────
        // Loaded once per process, shared across all Destroyer instances.
        // Black / near-black pixels are keyed out at draw time via ImageAttributes
        // — the original Bitmap is never modified.
        private static Bitmap _sprite;
        private static readonly object _spriteLock = new object();

        private static Bitmap GetSprite()
        {
            if (_sprite != null) return _sprite;
            lock (_spriteLock)
            {
                if (_sprite != null) return _sprite;
                try { _sprite = new Bitmap(Properties.Resources.Destroyer); }
                catch { _sprite = null; }   // falls back to polygon if image missing
            }
            return _sprite;
        }

        // Zoom level at which the sprite is shown instead of the plain polygon.
        private const float SpriteZoom = 1.2f;

        // ─────────────────────────────────────────────────────────────────────
        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx   = (Position.X + offset.X) * zoom;
            float sy   = (Position.Y + offset.Y) * zoom;
            float w    = 25 * zoom, h = 14 * zoom, skew = 8 * zoom;
            var   color = GetShipColor();

            // Parallelogram points — used for the outline overlay and fallback shape
            var pts = new PointF[]
            {
                new PointF(sx - w + skew, sy - h),
                new PointF(sx + w + skew, sy - h),
                new PointF(sx + w - skew, sy + h),
                new PointF(sx - w - skew, sy + h)
            };

            if (zoom >= SpriteZoom)
            {
                // ── Sprite mode ───────────────────────────────────────────────
                var sprite = GetSprite();
                if (sprite != null)
                {
                    // Sized slightly wider than the full parallelogram span
                    // (w + skew on each side), with a landscape aspect ratio.
                    float sz   = (w + skew) * 2.1f;
                    var   dest = new Rectangle((int)(sx - sz * 0.5f),
                                               (int)(sy - sz * 0.4f),
                                               (int)sz, (int)(sz * 0.8f));

                    using (var ia = new System.Drawing.Imaging.ImageAttributes())
                    {
                        // Key out near-black background (0,0,0) → (20,20,20)
                        ia.SetColorKey(Color.FromArgb(0, 0, 0),
                                       Color.FromArgb(20, 20, 20));
                        g.DrawImage(sprite, dest,
                                    0, 0, sprite.Width, sprite.Height,
                                    GraphicsUnit.Pixel, ia);
                    }
                }

                // Team-coloured parallelogram outline overlaid on the sprite
                // so ownership is always readable; brightens when selected.
                int ringAlpha = IsSelected ? 220 : 90;
                using (var pen = new Pen(Color.FromArgb(ringAlpha, color),
                                         IsSelected ? 2.5f : 1.5f))
                    g.DrawPolygon(pen, pts);
            }
            else
            {
                // ── Polygon mode (zoomed out) ─────────────────────────────────
                using (var pen   = new Pen(color, IsSelected ? 2f : 1f))
                using (var brush = new SolidBrush(Color.FromArgb(50, color)))
                {
                    g.FillPolygon(brush, pts);
                    g.DrawPolygon(pen, pts);
                }
            }

            DrawHealthBar(g, new RectangleF(sx - w, sy - h, w * 2, h * 2));
            DrawLabel(g, new PointF(sx, sy), h);
        }
    }
}
