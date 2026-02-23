using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
    // ── Miner (circle) ────────────────────────────────────────────────────────
    public class Miner : Ship
    {
        public override int CargoCapacity => 100;

        // Delay timers in milliseconds
        private const int MiningDelayMs  = 4000;
        private const int OffloadDelayMs = 4000;

        private int miningTimer  = 0;   // counts up while stationary at asteroid
        private int offloadTimer = 0;   // counts up while stationary at mothership
        public  int  PendingDeposit  { get; set; }  // set when offload completes, GameWorld reads & clears
        public float MiningProgress  => IsMining && miningTimer  > 0 ? (float)miningTimer  / MiningDelayMs  : 0f;
        public float OffloadProgress => ReturningToMothership && IsOffloading ? (float)offloadTimer / OffloadDelayMs : 0f;
        public bool  IsOffloading    { get; private set; }

		private const float SpriteZoom = 1.8f;

		private static Bitmap _sprite;
		private static readonly object _spriteLock = new object();

		private static Bitmap GetSprite()
		{
			if (_sprite != null) return _sprite;
			lock (_spriteLock)
			{
				if (_sprite != null) return _sprite;
				try { _sprite = new Bitmap(Properties.Resources.Worker); }
				catch { _sprite = null; }   // falls back to polygon if image missing
			}
			return _sprite;
		}

		public Miner(PointF position, bool isPlayer) : base(ShipType.Worker, position, isPlayer)
        {
            Label = "worker";
        }

        public override void Update(List<Ship> allShips, List<Asteroid> asteroids, Ship playerMothership)
        {
            if (!IsAlive) return;

            if (AttackTarget != null)
            {
                base.Update(allShips, asteroids, playerMothership);
                return;
            }

            // Each miner returns to its own team's mothership, not always the player's.
            Ship myMothership = IsPlayerOwned ? playerMothership : FindOwnMothership(allShips);

            if (IsMining && TargetAsteroid != null)
            {
                if (!TargetAsteroid.IsAlive) { TargetAsteroid = null; IsMining = false; miningTimer = 0; return; }

                float dist = Distance(Position, TargetAsteroid.Position);
                if (dist > 30)
                {
                    miningTimer = 0; // reset if not yet at asteroid
                    Destination = TargetAsteroid.Position;
                    MoveTowardDestination();
                }
                else
                {
                    // At asteroid — count up mining delay
                    miningTimer += DeltaMs;
                    if (miningTimer >= MiningDelayMs)
                    {
                        miningTimer = 0;
                        int mined = Math.Min(CargoCapacity,
                            Math.Min(TargetAsteroid.Resources, CargoCapacity - CargoHeld));
                        CargoHeld                += mined;
                        TargetAsteroid.Resources -= mined;
                        if (CargoHeld >= CargoCapacity || TargetAsteroid.Resources <= 0)
                        {
                            ReturningToMothership = true;
                            IsOffloading          = false;
                            IsMining              = false;
                            offloadTimer          = 0;
                            if (myMothership != null)
                                Destination = myMothership.Position;
                        }
                    }
                }
            }
            else if (ReturningToMothership && myMothership != null)
            {
                // Go to nearest collector (if one exists nearby) or mothership
                Ship offloadAt = FindOffloadTarget(allShips, myMothership);
                Destination = offloadAt.Position;
                MoveTowardDestination();

                float offloadRange = offloadAt is ResourceCollector ? ResourceCollector.OffloadRange
                                   : offloadAt is Carrier          ? Carrier.OffloadRange
                                   : 50;

                if (Distance(Position, offloadAt.Position) < offloadRange)
                {
                    IsOffloading  = true;
                    offloadTimer += DeltaMs;
                    if (offloadTimer >= OffloadDelayMs)
                    {
                        offloadTimer          = 0;
                        IsOffloading          = false;
                        ReturningToMothership = false;
                        PendingDeposit        = CargoHeld;  // GameWorld credits instantly
                        CargoHeld             = 0;
                        if      (offloadAt is ResourceCollector rc) rc.IsReceiving = true;
                        else if (offloadAt is Carrier           cv) cv.IsReceiving = true;
                    }
                }
                else
                {
                    offloadTimer = 0;
                    IsOffloading = false;
                }
            }
            else
            {
                MoveTowardDestination();
            }
        }

        public override void Draw(Graphics g, PointF offset, float zoom)
        {
            float sx = (Position.X + offset.X) * zoom;
            float sy = (Position.Y + offset.Y) * zoom;
            float r  = 10 * zoom;
            var   color = GetShipColor();

            if (zoom >= SpriteZoom)
            {
                // ── Sprite mode ───────────────────────────────────────────────
                var sprite = GetSprite();
                if (sprite != null)
                {
                    // Sized slightly wider than the full parallelogram span
                    // (w + skew on each side), with a landscape aspect ratio.
                    var dest = new Rectangle((int)(sx - r),
                                               (int)(sy - r),
                                               (int)(r * 2f), (int)(r * 2f));

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
                    g.DrawEllipse(pen, sx - r - 3, sy - r - 3, (r + 3) * 2, (r + 3) * 2);
			}
            else
            {

                using (var pen = new Pen(color, IsSelected ? 2f : 1f))
                using (var brush = new SolidBrush(Color.FromArgb(50, color)))
                {
                    g.FillEllipse(brush, sx - r, sy - r, r * 2, r * 2);
                    g.DrawEllipse(pen, sx - r, sy - r, r * 2, r * 2);
                    if (IsMining)
                        g.DrawEllipse(new Pen(Color.Yellow, 1), sx - r - 3, sy - r - 3, (r + 3) * 2, (r + 3) * 2);
                }
            }

            // Mining progress arc (yellow) — sweeps around as the 4s delay counts up
            float mp = MiningProgress;
            if (mp > 0f)
            {
                float ar = r + 5;
                using (var pen = new Pen(Color.Yellow, 2f))
                    g.DrawArc(pen, sx - ar, sy - ar, ar * 2, ar * 2, -90, 360f * mp);
            }

            // Offload progress arc (cyan) — sweeps around while docked at mothership
            float op = OffloadProgress;
            if (op > 0f)
            {
                float ar = r + 5;
                using (var pen = new Pen(Color.Cyan, 2f))
                    g.DrawArc(pen, sx - ar, sy - ar, ar * 2, ar * 2, -90, 360f * op);
            }

            // Green repair glow when docking
            if (IsDocking && HP < MaxHPValue)
            {
                using (var pen = new Pen(Color.FromArgb(180, Color.LimeGreen), 1.5f))
                    g.DrawEllipse(pen, sx - r - 3, sy - r - 3, (r + 3) * 2, (r + 3) * 2);
            }
            DrawHealthBar(g, new RectangleF(sx - r, sy - r, r * 2, r * 2));
            DrawLabel(g, new PointF(sx, sy), r);
        }

        private Ship FindOwnMothership(List<Ship> allShips)
        {
            foreach (var ship in allShips)
                if (ship is Mothership && ship.TeamId == TeamId && ship.IsAlive)
                    return ship;
            return null;
        }

        private Ship FindOffloadTarget(List<Ship> allShips, Ship myMothership)
        {
            Ship  best     = myMothership;
            float bestDist = Distance(Position, myMothership.Position);
            foreach (var ship in allShips)
            {
                if (!ship.IsAlive || ship.TeamId != TeamId) continue;
                if (ship is ResourceCollector || ship is Carrier)
                {
                    float d = Distance(Position, ship.Position);
                    if (d < bestDist) { bestDist = d; best = ship; }
                }
            }
            return best;
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
