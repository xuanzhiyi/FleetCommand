using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
    // ── Mothership (oval / sprite) ────────────────────────────────────────────
    public class Mothership : Ship
    {
        public List<BuildOrder> BuildQueue { get; } = new List<BuildOrder>();

        // ── Sprite support ────────────────────────────────────────────────────
        // The sprite is loaded once per process and shared across all Mothership
        // instances.  Black / near-black pixels are keyed out at draw time via
        // ImageAttributes — the original Bitmap is never modified.
        private static Bitmap _sprite;
        private static readonly object _spriteLock = new object();

        private static Bitmap GetSprite()
        {
            if (_sprite != null) return _sprite;
            lock (_spriteLock)
            {
                if (_sprite != null) return _sprite;
                try { _sprite = new Bitmap(Properties.Resources.mothership); }
                catch { _sprite = null; }   // falls back to ellipse if image missing
            }
            return _sprite;
        }

        // Zoom level at which the sprite is shown instead of the plain ellipse.
        private const float SpriteZoom = 0.8f;

        // ─────────────────────────────────────────────────────────────────────
        public Mothership(PointF position, bool isPlayer) : base(ShipType.Mothership, position, isPlayer)
        {
            Label = "Mothership";
        }

        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx    = (Position.X + offset.X) * zoom;
            float sy    = (Position.Y + offset.Y) * zoom;
            float rx    = 40 * zoom, ry = 25 * zoom;
            var   color = GetShipColor();

            if (zoom >= SpriteZoom)
            {
                // ── Sprite mode ───────────────────────────────────────────────
                var sprite = GetSprite();
                if (sprite != null)
                {
                    // Draw square sprite centred on the ship; scale with zoom.
                    // rx * 2.4 gives a nicely-sized image slightly larger than
                    // the underlying ellipse (80 world-units wide at zoom = 1).
                    float sz      = rx * 2.4f;
                    var   dest    = new Rectangle((int)(sx - sz * 0.5f),
                                                  (int)(sy - sz * 0.35f),
                                                  (int)sz, (int)(sz*0.7f));

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

                // Team-coloured ellipse ring overlaid on the sprite so ownership
                // is always readable; brightens when selected.
                int ringAlpha = IsSelected ? 220 : 90;
                using (var pen = new Pen(Color.FromArgb(ringAlpha, color),
                                         IsSelected ? 2.5f : 1.5f))
                    g.DrawEllipse(pen, sx - rx, sy - ry, rx * 2, ry * 2);
            }
            else
            {
                // ── Ellipse mode (zoomed out) ─────────────────────────────────
                using (var pen   = new Pen(color, IsSelected ? 2.5f : 1.5f))
                using (var brush = new SolidBrush(Color.FromArgb(60, color)))
                {
                    g.FillEllipse(brush, sx - rx, sy - ry, rx * 2, ry * 2);
                    g.DrawEllipse(pen,   sx - rx, sy - ry, rx * 2, ry * 2);
                }
            }

            // Docking ring when fighters are repairing
            if (IsDocking)
            {
                float dr = (GameConstants.DockRange + 8) * zoom;
                using (var pen = new Pen(Color.FromArgb(140, Color.LimeGreen), 1.5f)
                                     { DashStyle = DashStyle.Dot })
                    g.DrawEllipse(pen, sx - dr, sy - dr, dr * 2, dr * 2);
            }

            DrawHealthBar(g, new RectangleF(sx - rx, sy - ry, rx * 2, ry * 2));
            DrawLabel(g, new PointF(sx, sy), ry);
        }
    }
}
