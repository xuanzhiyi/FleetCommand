using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
    // ── Frigate (diamond) ─────────────────────────────────────────────────────
    public class Frigate : Ship
    {
		private const float SpriteZoom = 1.2f;

		private static Bitmap _sprite;
		private static readonly object _spriteLock = new object();

		private static Bitmap GetSprite()
		{
			if (_sprite != null) return _sprite;
			lock (_spriteLock)
			{
				if (_sprite != null) return _sprite;
				try { _sprite = new Bitmap(Properties.Resources.Frigate); }
				catch { _sprite = null; }   // falls back to polygon if image missing
			}
			return _sprite;
		}

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

			if (zoom >= SpriteZoom)
			{
				// ── Sprite mode ───────────────────────────────────────────────
				var sprite = GetSprite();
				if (sprite != null)
				{
					// Create color matrix for team tinting
					// Simple multiplication: preserves black for SetColorKey to work
					var teamColor = GetShipColor();
					float r = teamColor.R / 255f;
					float g_val = teamColor.G / 255f;
					float b = teamColor.B / 255f;

					// Save graphics state for transformation
					var graphicsState = g.Save();

					// Translate to sprite center for rotation
					g.TranslateTransform(sx, sy);

					// Rotate by heading (convert radians to degrees)
					float degreesRotation = (float)(Heading * 180 / Math.PI);
					g.RotateTransform(degreesRotation);

					// Translate back to sprite position
					g.TranslateTransform(-w, -h);

					// Draw rotated, tinted sprite
					var dest = new Rectangle(0, 0, (int)(w * 2f), (int)(h * 2f));

					using (var ia = new System.Drawing.Imaging.ImageAttributes())
					{
						// Key out near-black background (0,0,0) → (20, 20, 20) to handle JPEG artifacts
						ia.SetColorKey(Color.FromArgb(0, 0, 0),
									   Color.FromArgb(20, 20, 20));
						g.DrawImage(sprite, dest,
									0, 0, sprite.Width, sprite.Height,
									GraphicsUnit.Pixel, ia);
					}

					// Restore graphics state
					g.Restore(graphicsState);
				}
				int ringAlpha = IsSelected ? 220 : 90;
				using (var pen = new Pen(Color.FromArgb(ringAlpha, GetShipColor()),
										 IsSelected ? 2.5f : 1.5f))
					g.DrawPolygon(pen, pts);
			}
			else
			{
				using (var pen = new Pen(color, IsSelected ? 2f : 1f))
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
