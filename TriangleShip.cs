using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
    // ── Fighter base (triangle) ───────────────────────────────────────────────
    public abstract class TriangleShip : Ship
	{
		private const float SpriteZoom = 1.8f;

		private static Bitmap _sprite;
		private static readonly object _spriteLock = new object();
		protected float Size;
		private static Bitmap GetSprite()
		{
			if (_sprite != null) return _sprite;
			lock (_spriteLock)
			{
				if (_sprite != null) return _sprite;
				try { _sprite = new Bitmap(Properties.Resources.interceptor); }
				catch { _sprite = null; }   // falls back to polygon if image missing
			}
			return _sprite;
		}


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
			if (zoom >= SpriteZoom)
			{
				// ── Sprite mode ───────────────────────────────────────────────
				var sprite = GetSprite();
				if (sprite != null)
				{
					// Sized slightly wider than the full parallelogram span
					// (w + skew on each side), with a landscape aspect ratio.
					float sz = w * 1.5f;
					var dest = new Rectangle((int)(sx - sz * 0.5f),
											   (int)(sy - sz * 0.4f),
											   (int)sz, (int)(sz));

					using (var ia = new System.Drawing.Imaging.ImageAttributes())
					{
						// Key out near-black background (0,0,0) → (20,20,20)
						ia.SetColorKey(Color.FromArgb(0, 0, 0),
									   Color.FromArgb(20, 20, 20));
						g.DrawImage(sprite, dest,
									30, -10, sprite.Width, sprite.Height * 1.3f,
									GraphicsUnit.Pixel, ia);
					}
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
				// Green repair glow when docking
				if (IsDocking && HP < MaxHPValue)
				{
					using (var pen = new Pen(Color.FromArgb(180, Color.LimeGreen), 1.5f))
						g.DrawEllipse(pen, sx - s - 3, sy - s - 3, (s + 3) * 2, (s + 3) * 2);
				}

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
