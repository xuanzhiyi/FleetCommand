using System;
using System.Drawing;
using System.Drawing.Imaging;
using FleetCommand.Properties;

namespace FleetCommand
{
	public class Asteroid
	{
		public PointF Position { get; set; }
		public int Resources { get; set; }
		public int MaxResources { get; private set; }
		public bool IsAlive => Resources > 0;
		public float Radius { get; private set; }

		// Which photo to use: 0=small(asteroids1), 1=medium(asteroids2), 2=large(asteroids3)
		private readonly int imageIndex;

		private static readonly Random rng = new Random();

		// Cached ImageAttributes for black-blending — created once per alpha level
		// We re-create per draw since alpha changes as resources deplete.
		// Three source bitmaps, loaded once.
		private static Bitmap[] _bitmaps = null;

		private static Bitmap[] GetBitmaps()
		{
			if (_bitmaps == null)
			{
				_bitmaps = new Bitmap[]
				{
					Properties.Resources.asteroids1,  // small
                    Properties.Resources.asteroids2,  // medium
                    Properties.Resources.asteroids3,  // large
                };
			}
			return _bitmaps;
		}

		public Asteroid(PointF position)
		{
			Position = position;
			Resources = rng.Next(GameConstants.AsteroidMinResource, GameConstants.AsteroidMaxResource);
			MaxResources = Resources;

			// Radius scales with resources
			Radius = 14 + Resources / 2000f;
			if (Radius > 45) Radius = 45;

			// Pick photo based on resource tier
			float pct = (float)Resources / GameConstants.AsteroidMaxResource;
			imageIndex = pct < 0.4f ? 0 : pct < 0.75f ? 1 : 2;
		}

		public void Draw(Graphics g, PointF offset, float zoom)
		{
			if (!IsAlive) return;

			float sx = (Position.X + offset.X) * zoom;
			float sy = (Position.Y + offset.Y) * zoom;
			float r = Radius * zoom;

			// Quick cull — skip if off screen
			var clip = g.ClipBounds;
			if (sx + r < clip.Left || sx - r > clip.Right ||
				sy + r < clip.Top || sy - r > clip.Bottom)
				return;

			float pct = (float)Resources / MaxResources;

			// When the asteroid is smaller than 18px on screen, draw a cheap circle.
			// Photo rendering with ImageAttributes is expensive and invisible at small sizes.
			if (r < 15f)
			{
				DrawCircle(g, sx, sy, r, pct);

				return;
			}

			DrawPhoto(g, sx, sy, r, pct);
		}

		private void DrawCircle(Graphics g, float sx, float sy, float r, float pct)
		{
			int gray = (int)(80 + 100 * pct);
			using (var brush = new SolidBrush(Color.FromArgb(gray, gray, gray)))
			using (var brush2 = new SolidBrush(Color.Gold))
			using (var pen = new Pen(Color.FromArgb(160, 160, 160), 1f))
			{
				g.FillEllipse(brush, sx - r, sy - r, r * 2, r * 2);
				g.DrawEllipse(pen, sx - r, sy - r, r * 2, r * 2);
				g.DrawEllipse(pen, sx - 0.5f * r, sy - 0.5f * r, r , r );
				g.FillEllipse(brush2, sx - 0.5f * r, sy - 0.5f * r, r, r);
			}
			// Resource bar
			if (r > 6)
			{
				float bw = r * 1.8f, bx = sx - bw / 2f, by = sy + r + 2;
				g.FillRectangle(Brushes.DarkGray, bx, by, bw, 3);
				using (var gold = new SolidBrush(pct > 0.5f ? Color.Gold : pct > 0.25f ? Color.Orange : Color.OrangeRed))
					g.FillRectangle(gold, bx, by, bw * pct, 3);
			}
		}

		private void DrawPhoto(Graphics g, float sx, float sy, float r, float pct)
		{
			float brightness = 0.35f + 0.65f * pct;

			var bitmaps = GetBitmaps();
			var bmp = bitmaps[imageIndex];

			var cm = new ColorMatrix(new float[][]
			{
				new float[] { brightness, 0,          0, 0, 0 },
				new float[] { 0, brightness,          0, 0, 0 },
				new float[] { 0,          0, brightness, 0, 0 },
				new float[] { 0,          0,          0, 1, 0 },
				new float[] { 0,          0,          0, 0, 1 },
			});

			using (var ia = new ImageAttributes())
			{
				ia.SetColorMatrix(cm);
				ia.SetColorKey(Color.FromArgb(0, 0, 0), Color.FromArgb(40, 40, 40));

				var state = g.Save();
				try
				{
					var destRect = new Rectangle(
						(int)(sx - r), (int)(sy - r),
						(int)(r * 2), (int)(r * 2));
					g.DrawImage(bmp, destRect, 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, ia);
				}
				finally
				{
					g.Restore(state);
				}
			}

			// Glow ring when rich in resources
			if (pct > 0.6f)
			{
				int glowA = (int)(60 * (pct - 0.6f) / 0.4f);
				using (var pen = new Pen(Color.FromArgb(glowA, Color.Gold), 1.5f))
					g.DrawEllipse(pen, sx - r, sy - r, r * 2, r * 2);
			}

			// Resource bar
			if (r > 10)
			{
				float bw = r * 1.8f, bx = sx - bw / 2f, by = sy + r * 0.85f + 3;
				g.FillRectangle(Brushes.DarkGray, bx, by, bw, 3);
				using (var gold = new SolidBrush(pct > 0.5f ? Color.Gold : pct > 0.25f ? Color.Orange : Color.OrangeRed))
					g.FillRectangle(gold, bx, by, bw * pct, 3);
			}
		}

		public bool HitTest(PointF worldPoint)
		{
			float dx = worldPoint.X - Position.X;
			float dy = worldPoint.Y - Position.Y;
			return Math.Sqrt(dx * dx + dy * dy) <= Radius;
		}
	}
}