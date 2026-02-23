using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
    // ── Corvette (square) ───────────────────────────────────────────────────────
    public class Corvette : Ship
    {
		private const float SpriteZoom = 1.8f;

		private static Bitmap _sprite;
		private static readonly object _spriteLock = new object();

		private static Bitmap GetSprite()
		{
			if (_sprite != null) return _sprite;
			lock (_spriteLock)
			{
				if (_sprite != null) return _sprite;
				try { _sprite = new Bitmap(Properties.Resources.corvette); }
				catch { _sprite = null; }   // falls back to polygon if image missing
			}
			return _sprite;
		}

		public Corvette(PointF position, bool isPlayer) : base(ShipType.Corvette, position, isPlayer) { Label = "Corvette"; }

        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx = (Position.X + offset.X) * zoom;
            float sy = (Position.Y + offset.Y) * zoom;
            float s  = 8 * zoom;
            var   color = GetShipColor();
            var   rect  = new RectangleF(sx - s, sy - s, s * 2, s * 2);


			if (zoom >= SpriteZoom)
			{
				// ── Sprite mode ───────────────────────────────────────────────
				var sprite = GetSprite();
				if (sprite != null)
				{
					// Sized slightly wider than the full parallelogram span
					// (w + skew on each side), with a landscape aspect ratio.
					var dest = new Rectangle((int)(sx - s),
											   (int)(sy - s),
											   (int)(s * 2f), (int)(s * 2f));

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
				int ringAlpha = IsSelected ? 220 : 90;
				using (var pen = new Pen(Color.FromArgb(ringAlpha, GetShipColor()),
										 IsSelected ? 2.5f : 1.5f))
					g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
			}
			else
			{
				using (var pen = new Pen(color, IsSelected ? 2f : 1f))
				using (var brush = new SolidBrush(Color.FromArgb(50, color)))
				{
					g.FillRectangle(brush, rect);
					g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
				}
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
