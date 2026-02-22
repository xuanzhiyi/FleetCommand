using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetCommand
{
    // ── Combat Effect Kinds ───────────────────────────────────────────────────
    public enum CombatEffectKind
    {
        LaserGreen,    // Corvet (player)   — instant thin laser line, green
        LaserRed,      // Corvet (enemy)    — instant thin laser line, red
        PlasmaBlue,    // Mothership/Carrier — wide plasma beam, blue
        Missile,       // Interceptor       — fast-moving projectile bullet + impact flash
        Bomb,          // Bomber            — slow projectile bomb + expanding explosion rings
        IonCannon,     // Destroyer/BC      — wide electric arc beam, blue-white
        FrigateShot,   // Frigate           — fast flak ball + radiating slash shrapnel
    }

    // ── Combat Effect ─────────────────────────────────────────────────────────
    /// <summary>
    /// A short-lived visual effect drawn between attacker and target.
    /// Projectile kinds (Missile, Bomb, FrigateShot) are animated — they travel
    /// from From→To before showing an impact effect.
    /// </summary>
    public class CombatEffect
    {
        // ── Public state ────────────────────────────────────────────────────
        public PointF From { get; }
        public PointF To   { get; }

        /// <summary>Normalised lifetime [0..1].  1 = expired.</summary>
        public float Age { get; private set; }

        public bool            IsExpired => Age >= 1f;
        public CombatEffectKind Kind     { get; }

        // ── Internal tuning ─────────────────────────────────────────────────
        private readonly float _lifetime;       // total seconds this effect lives
        private readonly float[] _ionOffsets;   // precomputed perpendicular offsets (IonCannon only)

        private static int _seed = 0;           // incrementing seed → unique per effect

        // ── Constructor ─────────────────────────────────────────────────────
        public CombatEffect(PointF from, PointF to, CombatEffectKind kind)
        {
            From     = from;
            To       = to;
            Kind     = kind;
            _lifetime = Lifetime(kind);

            // Pre-compute zigzag offsets for ion cannon so they stay stable each frame
            if (kind == CombatEffectKind.IonCannon)
            {
                var rng = new Random(++_seed);
                _ionOffsets = new float[5];
                for (int i = 0; i < 5; i++)
                    _ionOffsets[i] = (float)((rng.NextDouble() - 0.5) * 10);
            }
        }

        private static float Lifetime(CombatEffectKind k)
        {
            switch (k)
            {
                case CombatEffectKind.Missile:     return 0.14f;
                case CombatEffectKind.Bomb:        return 0.38f;
                case CombatEffectKind.IonCannon:   return 0.22f;
                case CombatEffectKind.FrigateShot: return 0.24f;
                case CombatEffectKind.LaserGreen:
                case CombatEffectKind.LaserRed:    return 0.12f;
                default:                           return 0.18f;  // PlasmaBlue
            }
        }

        // ── Tick ────────────────────────────────────────────────────────────
        public void Tick(float dt)
        {
            Age = Math.Min(1f, Age + dt / _lifetime);
        }

        // ── Draw ────────────────────────────────────────────────────────────
        public void Draw(Graphics g, PointF cameraOffset, float zoom)
        {
            float ax = (From.X + cameraOffset.X) * zoom;
            float ay = (From.Y + cameraOffset.Y) * zoom;
            float bx = (To.X   + cameraOffset.X) * zoom;
            float by = (To.Y   + cameraOffset.Y) * zoom;

            switch (Kind)
            {
                case CombatEffectKind.LaserGreen:
                    DrawLaser(g, ax, ay, bx, by, 80, 255, 80);    break;
                case CombatEffectKind.LaserRed:
                    DrawLaser(g, ax, ay, bx, by, 255, 70, 70);    break;
                case CombatEffectKind.PlasmaBlue:
                    DrawPlasma(g, ax, ay, bx, by);                 break;
                case CombatEffectKind.Missile:
                    DrawMissile(g, ax, ay, bx, by);                break;
                case CombatEffectKind.Bomb:
                    DrawBomb(g, ax, ay, bx, by);                   break;
                case CombatEffectKind.IonCannon:
                    DrawIonCannon(g, ax, ay, bx, by);              break;
                case CombatEffectKind.FrigateShot:
                    DrawFrigateShot(g, ax, ay, bx, by);            break;
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static int A(int base_, float fade) =>
            Math.Max(0, Math.Min(255, (int)(base_ * fade)));

        // ── LASER (Corvet) ───────────────────────────────────────────────────
        // Instant thin line — fades quickly.
        private void DrawLaser(Graphics g, float ax, float ay, float bx, float by,
                                int r, int gr, int b)
        {
            float fade = 1f - Age;
            using (var pen = new Pen(Color.FromArgb(A(210, fade), r, gr, b), 1.5f))
                g.DrawLine(pen, ax, ay, bx, by);
            // Hot white core for the first 40 % of life
            if (Age < 0.4f)
            {
                float cf = 1f - Age / 0.4f;
                using (var pen = new Pen(Color.FromArgb(A(160, cf), 255, 255, 255), 0.6f))
                    g.DrawLine(pen, ax, ay, bx, by);
            }
        }

        // ── PLASMA (Mothership / Carrier) ────────────────────────────────────
        // Wide, glowing blue beam.
        private void DrawPlasma(Graphics g, float ax, float ay, float bx, float by)
        {
            float fade = 1f - Age;
            using (var pen = new Pen(Color.FromArgb(A(70, fade), 60, 160, 255), 4f))
                g.DrawLine(pen, ax, ay, bx, by);
            using (var pen = new Pen(Color.FromArgb(A(200, fade), 100, 200, 255), 1.8f))
                g.DrawLine(pen, ax, ay, bx, by);
            if (Age < 0.35f)
            {
                float cf = 1f - Age / 0.35f;
                using (var pen = new Pen(Color.FromArgb(A(150, cf), 255, 255, 255), 0.8f))
                    g.DrawLine(pen, ax, ay, bx, by);
            }
        }

        // ── MISSILE (Interceptor) ─────────────────────────────────────────────
        // Fast bright bullet that travels to the target then flashes on impact.
        private void DrawMissile(Graphics g, float ax, float ay, float bx, float by)
        {
            const float flightEnd = 0.72f;

            if (Age < flightEnd)
            {
                float t  = Age / flightEnd;
                float px = ax + (bx - ax) * t;
                float py = ay + (by - ay) * t;

                // Fading yellow trail (4 ghost dots)
                for (int i = 1; i <= 4; i++)
                {
                    float tt = Math.Max(0f, t - i * 0.07f);
                    float tx = ax + (bx - ax) * tt;
                    float ty = ay + (by - ay) * tt;
                    int ta = A(140, (1f - (float)i / 4f) * (1f - Age));
                    using (var b = new SolidBrush(Color.FromArgb(ta, 255, 240, 100)))
                        g.FillEllipse(b, tx - 1.5f, ty - 1.5f, 3f, 3f);
                }

                // Bright bullet core (white-yellow)
                int pa = A(230, 1f - Age * 0.4f);
                using (var b = new SolidBrush(Color.FromArgb(pa, 255, 255, 180)))
                    g.FillEllipse(b, px - 2.5f, py - 2.5f, 5f, 5f);
                using (var b = new SolidBrush(Color.FromArgb(pa, 255, 255, 80)))
                    g.FillEllipse(b, px - 1.4f, py - 1.4f, 2.8f, 2.8f);
            }
            else
            {
                // Impact flash — expanding yellow ring
                float it = (Age - flightEnd) / (1f - flightEnd);  // 0→1
                float r  = 2f + it * 7f;
                int   ia = A(210, 1f - it);
                using (var b = new SolidBrush(Color.FromArgb(ia / 3, 255, 240, 100)))
                    g.FillEllipse(b, bx - r, by - r, r * 2, r * 2);
                using (var pen = new Pen(Color.FromArgb(ia, 255, 240, 100), 1.2f))
                    g.DrawEllipse(pen, bx - r, by - r, r * 2, r * 2);
                // Bright white flash centre
                if (it < 0.45f)
                {
                    float cf = 1f - it / 0.45f;
                    float cr = cf * 4f;
                    using (var b = new SolidBrush(Color.FromArgb(A(220, cf), 255, 255, 255)))
                        g.FillEllipse(b, bx - cr, by - cr, cr * 2, cr * 2);
                }
            }
        }

        // ── BOMB (Bomber) ─────────────────────────────────────────────────────
        // Slow orange cannonball that detonates in a two-ring explosion.
        private void DrawBomb(Graphics g, float ax, float ay, float bx, float by)
        {
            const float flightEnd = 0.62f;

            if (Age < flightEnd)
            {
                float t   = Age / flightEnd;
                float px  = ax + (bx - ax) * t;
                float py  = ay + (by - ay) * t;
                float sz  = 4.5f + t * 2.5f;       // projectile grows slightly in flight
                int   pa  = A(230, 1f - Age * 0.15f);

                // Outer glow
                using (var pen = new Pen(Color.FromArgb(pa / 3, 255, 120, 0), 1.5f))
                    g.DrawEllipse(pen, px - sz * 1.45f, py - sz * 1.45f, sz * 2.9f, sz * 2.9f);
                // Body (dark orange)
                using (var b = new SolidBrush(Color.FromArgb(pa, 190, 70, 10)))
                    g.FillEllipse(b, px - sz, py - sz, sz * 2, sz * 2);
                // Bright highlight
                using (var b = new SolidBrush(Color.FromArgb(pa, 255, 160, 40)))
                    g.FillEllipse(b, px - sz * 0.55f, py - sz * 0.55f, sz * 1.1f, sz * 1.1f);
            }
            else
            {
                float it = (Age - flightEnd) / (1f - flightEnd);   // 0→1

                // Outer blast ring (large, orange)
                float or_ = it * 24f;
                int oa = A(180, 1f - it);
                using (var pen = new Pen(Color.FromArgb(oa, 255, 100, 0), 2.2f))
                    g.DrawEllipse(pen, bx - or_, by - or_, or_ * 2, or_ * 2);

                // Middle shockwave ring (yellow)
                float mr = it * 15f;
                int ma = A(200, 1f - it);
                using (var pen = new Pen(Color.FromArgb(ma, 255, 210, 50), 2.5f))
                    g.DrawEllipse(pen, bx - mr, by - mr, mr * 2, mr * 2);

                // Inner fireball (fades before rings)
                if (it < 0.48f)
                {
                    float ff = 1f - it / 0.48f;
                    float fr = ff * 11f;
                    using (var b = new SolidBrush(Color.FromArgb(A(210, ff), 255, 200, 80)))
                        g.FillEllipse(b, bx - fr, by - fr, fr * 2, fr * 2);
                }
            }
        }

        // ── ION CANNON (Destroyer / Battlecruiser) ────────────────────────────
        // Wide electric-blue beam with a precomputed zigzag arc overlaid.
        private void DrawIonCannon(Graphics g, float ax, float ay, float bx, float by)
        {
            // Bright in first 25 %, then fades
            float intensity = Age < 0.25f ? 1f : 1f - (Age - 0.25f) / 0.75f;
            int   ia        = A(220, intensity);

            // Wide outer glow
            using (var pen = new Pen(Color.FromArgb(ia / 4, 80, 180, 255), 7f))
                g.DrawLine(pen, ax, ay, bx, by);
            // Mid beam
            using (var pen = new Pen(Color.FromArgb(ia * 2 / 3, 100, 220, 255), 3f))
                g.DrawLine(pen, ax, ay, bx, by);
            // Bright core
            using (var pen = new Pen(Color.FromArgb(ia, 200, 240, 255), 1.3f))
                g.DrawLine(pen, ax, ay, bx, by);
            // White-hot centre (first 35 %)
            if (Age < 0.35f)
            {
                float cf = 1f - Age / 0.35f;
                using (var pen = new Pen(Color.FromArgb(A(170, cf), 255, 255, 255), 0.7f))
                    g.DrawLine(pen, ax, ay, bx, by);
            }

            // Precomputed zigzag electric arc
            if (_ionOffsets != null)
            {
                float dx  = bx - ax, dy = by - ay;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len > 1f)
                {
                    float nx = -dy / len, ny = dx / len;  // perpendicular unit
                    int arcA = A(160, intensity);
                    float prevX = ax, prevY = ay;
                    for (int i = 0; i < 5; i++)
                    {
                        float t  = (float)(i + 1) / 6f;
                        float cx = ax + dx * t + nx * _ionOffsets[i];
                        float cy = ay + dy * t + ny * _ionOffsets[i];
                        using (var pen = new Pen(Color.FromArgb(arcA, 160, 230, 255), 0.9f))
                            g.DrawLine(pen, prevX, prevY, cx, cy);
                        prevX = cx; prevY = cy;
                    }
                    using (var pen = new Pen(Color.FromArgb(A(160, intensity), 160, 230, 255), 0.9f))
                        g.DrawLine(pen, prevX, prevY, bx, by);
                }
            }
        }

        // ── FRIGATE SHOT (Frigate) ────────────────────────────────────────────
        // Fast magenta flak ball that bursts into shrapnel slash lines on impact.
        private void DrawFrigateShot(Graphics g, float ax, float ay, float bx, float by)
        {
            const float flightEnd = 0.52f;

            if (Age < flightEnd)
            {
                float t  = Age / flightEnd;
                float px = ax + (bx - ax) * t;
                float py = ay + (by - ay) * t;
                int   pa = A(230, 1f - Age * 0.3f);

                // Short magenta trail
                for (int i = 1; i <= 3; i++)
                {
                    float tt = Math.Max(0f, t - i * 0.10f);
                    float tx = ax + (bx - ax) * tt;
                    float ty = ay + (by - ay) * tt;
                    int ta = A(120, 1f - (float)i / 3f);
                    using (var b = new SolidBrush(Color.FromArgb(ta, 200, 80, 255)))
                        g.FillEllipse(b, tx - 2f, ty - 2f, 4f, 4f);
                }

                // Main flak ball (magenta/purple)
                float r = 3.8f;
                using (var b = new SolidBrush(Color.FromArgb(pa, 210, 90, 255)))
                    g.FillEllipse(b, px - r, py - r, r * 2, r * 2);
                using (var b = new SolidBrush(Color.FromArgb(pa, 255, 200, 255)))
                    g.FillEllipse(b, px - r * 0.45f, py - r * 0.45f, r * 0.9f, r * 0.9f);
            }
            else
            {
                float it = (Age - flightEnd) / (1f - flightEnd);   // 0→1
                int   sa = A(210, 1f - it);

                // 7 radiating shrapnel lines centred on impact point
                float dx       = bx - ax, dy = by - ay;
                float baseAngle = (float)Math.Atan2(dy, dx);
                float slashLen  = 4f + it * 16f;

                for (int i = 0; i < 7; i++)
                {
                    float angle = baseAngle + (i - 3f) * 0.48f;    // spread ≈ ±70°
                    float ex = bx + (float)Math.Cos(angle) * slashLen;
                    float ey = by + (float)Math.Sin(angle) * slashLen;
                    using (var pen = new Pen(Color.FromArgb(sa, 255, 80, 200), 1.6f))
                        g.DrawLine(pen, bx, by, ex, ey);
                }

                // Central impact flash (fades first half)
                if (it < 0.45f)
                {
                    float cf = 1f - it / 0.45f;
                    float cr = cf * 9f;
                    using (var b = new SolidBrush(Color.FromArgb(A(200, cf), 255, 180, 255)))
                        g.FillEllipse(b, bx - cr, by - cr, cr * 2, cr * 2);
                }
            }
        }
    }
}
