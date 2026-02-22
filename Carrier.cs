using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
    // ── Carrier (wide flight-deck rectangle) ──────────────────────────────────
    // Mobile battle carrier: builds fighters & workers, collects resources like
    // a ResourceCollector, has a dock ring for fighter repairs, and can fight
    // with moderate weapons. Max 3 per player; upgradeable like all other ships.
    public class Carrier : Ship
    {
        public const int OffloadRange = 80;  // miners offload within this radius

        // Build queue (separate from the mothership)
        public List<BuildOrder> BuildQueue { get; } = new List<BuildOrder>();


		// Zoom level at which the sprite is shown instead of the plain polygon.
		private const float SpriteZoom = 1.2f;

		private static Bitmap _sprite;
		private static readonly object _spriteLock = new object();

		// Visual pulse when a miner is actively offloading nearby
		public bool IsReceiving { get; set; }

        // Ship types this carrier is allowed to build
        public static readonly ShipType[] CanBuild =
        {
            ShipType.Interceptor,
            ShipType.Bomber,
            ShipType.Corvet,
            ShipType.Miner,
        };

        public Carrier(PointF position, bool isPlayer)
            : base(ShipType.Carrier, position, isPlayer)
        {
            Label = "Carrier";
        }

		private static Bitmap GetSprite()
		{
			if (_sprite != null) return _sprite;
			lock (_spriteLock)
			{
				if (_sprite != null) return _sprite;
				try { _sprite = new Bitmap(Properties.Resources.Carrier); }
				catch { _sprite = null; }   // falls back to polygon if image missing
			}
			return _sprite;
		}


		public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx  = (Position.X + offset.X) * zoom;
            float sy  = (Position.Y + offset.Y) * zoom;
            float w   = 30 * zoom;   // half-width of flight deck
            float h   = 30 * zoom;   // half-height of hull
            var   col = GetShipColor();

			// ── Main hull (wide flat flight deck) ─────────────────────────────
			var hullRect = new RectangleF(sx - w, sy - h * 0.7f, w * 2, h);
			if (zoom >= SpriteZoom)
            {
                // ── Sprite mode ───────────────────────────────────────────────
                var sprite = GetSprite();
                if (sprite != null)
                {
                    // Sized slightly wider than the full parallelogram span
                    // (w + skew on each side), with a landscape aspect ratio.
                    float sz = w * 2.1f;
                    var dest = new Rectangle((int)(sx - sz * 0.5f),
                                               (int)(sy - sz * 0.4f),
                                               (int)sz, (int)(sz * 0.8f));

                    using (var ia = new System.Drawing.Imaging.ImageAttributes())
                    {
                        // Key out near-black background (0,0,0) → (20,20,20)
                        ia.SetColorKey(Color.FromArgb(0, 0, 0),
                                       Color.FromArgb(20, 20, 20));
                        g.DrawImage(sprite, dest,
                                    0, 0, sprite.Width, sprite.Height*1.3f,
                                    GraphicsUnit.Pixel, ia);
                    }
                }
				int ringAlpha = IsSelected ? 220 : 90;
				using (var pen = new Pen(Color.FromArgb(ringAlpha, GetShipColor()),
										 IsSelected ? 2.5f : 1.5f))
					g.DrawRectangle(pen, hullRect.X, hullRect.Y, hullRect.Width, hullRect.Height);
			}
            else
            {
                using (var pen = new Pen(col, IsSelected ? 2f : 1.5f))
                using (var brush = new SolidBrush(Color.FromArgb(40, col)))
                {
                    g.FillRectangle(brush, hullRect);
                    g.DrawRectangle(pen, hullRect.X, hullRect.Y, hullRect.Width, hullRect.Height);

                    // Centre deck runway line
                    using (var runwayPen = new Pen(Color.FromArgb(80, col), 1f))
                        g.DrawLine(runwayPen, sx - w + 4, sy, sx + w - 4, sy);
                }
            }

            // ── Dock repair glow (green ring) ─────────────────────────────────
            if (IsDocking && HP < MaxHPValue)
            {
                float rw = w + 4, rh = h + 6;
                using (var pen = new Pen(Color.FromArgb(180, Color.LimeGreen), 1.5f))
                    g.DrawEllipse(pen, sx - rw, sy - rh * 0.5f, rw * 2, rh);
            }

            // ── Gold receiving pulse when miners are offloading ───────────────
            if (IsReceiving)
            {
                float pr = (OffloadRange + 6) * zoom;
                using (var pen = new Pen(Color.FromArgb(140, Color.Gold), 1.5f) { DashStyle = DashStyle.Dot })
                    g.DrawEllipse(pen, sx - pr, sy - pr, pr * 2, pr * 2);
            }

            // ── Build progress arc ────────────────────────────────────────────
            if (BuildQueue.Count > 0)
            {
                var building = BuildQueue[0];
                float pw = w * 2 * building.Progress;
                float by = sy + h * 0.55f + 3;
                g.FillRectangle(Brushes.DarkSlateBlue,
                    new RectangleF(sx - w, by, w * 2, 4 * zoom));
                g.FillRectangle(Brushes.Cyan,
                    new RectangleF(sx - w, by, pw, 4 * zoom));
            }

            DrawHealthBar(g, hullRect);
            DrawLabel(g, new PointF(sx, sy), h);
        }
    }
}
