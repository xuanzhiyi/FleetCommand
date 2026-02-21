using System;
using System.Drawing;

namespace FleetCommand
{
    /// <summary>
    /// A short-lived visual stroke drawn between attacker and target during combat.
    /// </summary>
    public class CombatEffect
    {
        public PointF From { get; }
        public PointF To   { get; }

        // 0.0 = fresh, 1.0 = fully faded
        public float Age { get; private set; }

        // Lifetime in seconds
        private const float Lifetime = 0.18f;

        public bool IsExpired => Age >= 1f;

        // What kind of shot — affects colour
        public CombatEffectKind Kind { get; }

        public CombatEffect(PointF from, PointF to, CombatEffectKind kind)
        {
            From = from;
            To   = to;
            Kind = kind;
        }

        public void Tick(float dt)
        {
            Age = Math.Min(1f, Age + dt / Lifetime);
        }

        // Returns the stroke colour with alpha faded by Age
        public Color GetColor()
        {
            int alpha = (int)(220 * (1f - Age));
            switch (Kind)
            {
                case CombatEffectKind.LaserGreen:    return Color.FromArgb(alpha, 80, 255, 80);
                case CombatEffectKind.LaserRed:      return Color.FromArgb(alpha, 255, 60, 60);
                case CombatEffectKind.PlasmaBlue:    return Color.FromArgb(alpha, 60, 180, 255);
                case CombatEffectKind.TorpedoOrange: return Color.FromArgb(alpha, 255, 160, 40);
                default:                             return Color.FromArgb(alpha, 200, 200, 200);
            }
        }

        // Stroke width — thicker for heavier weapons, fades with age
        public float GetWidth()
        {
            float baseW = Kind == CombatEffectKind.TorpedoOrange ? 2.5f
                        : Kind == CombatEffectKind.PlasmaBlue    ? 2.0f
                        : 1.2f;
            return baseW * (1f - Age * 0.7f);
        }

        public void Draw(Graphics g, PointF cameraOffset, float zoom)
        {
            float ax = (From.X + cameraOffset.X) * zoom;
            float ay = (From.Y + cameraOffset.Y) * zoom;
            float bx = (To.X   + cameraOffset.X) * zoom;
            float by = (To.Y   + cameraOffset.Y) * zoom;

            using (var pen = new System.Drawing.Pen(GetColor(), GetWidth()))
                g.DrawLine(pen, ax, ay, bx, by);

            // For heavier shots add a small bright core while fresh
            if (Age < 0.3f && (Kind == CombatEffectKind.TorpedoOrange || Kind == CombatEffectKind.PlasmaBlue))
            {
                int coreAlpha = (int)(180 * (1f - Age / 0.3f));
                using (var corePen = new System.Drawing.Pen(Color.FromArgb(coreAlpha, 255, 255, 255), GetWidth() * 0.4f))
                    g.DrawLine(corePen, ax, ay, bx, by);
            }
        }
    }

    public enum CombatEffectKind
    {
        LaserGreen,      // player light ships (interceptor, corvet)
        LaserRed,        // enemy light ships
        PlasmaBlue,      // player heavy ships (frigate, destroyer, battlecruiser, mothership)
        TorpedoOrange,   // bombers (both sides)
    }
}
