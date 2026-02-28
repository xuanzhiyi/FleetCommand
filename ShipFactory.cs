using System;
using System.Drawing;

namespace FleetCommand
{
    // ── Build Order ───────────────────────────────────────────────────────────
    public class BuildOrder
    {
        public ShipType Type      { get; }
        public int      TotalTime { get; }
        public int      Elapsed   { get; set; }
        public bool     IsComplete => Elapsed >= TotalTime;
        public float    Progress   => (float)Elapsed / TotalTime;

        public BuildOrder(ShipType type, float difficultyMult)
        {
            Type      = type;
            TotalTime = (int)(GameConstants.BuildTimes[(int)type] / difficultyMult);
        }
    }

    // ── Ship Factory ──────────────────────────────────────────────────────────
    public static class ShipFactory
    {
        private static readonly Random rng = new Random();

        public static Ship Create(ShipType type, PointF position, bool isPlayer)
        {
            switch (type)
            {
                case ShipType.Mothership:         return new Mothership(position, isPlayer);
                case ShipType.Worker:              return new Miner(position, isPlayer);
                case ShipType.Interceptor:        return new Interceptor(position, isPlayer);
                case ShipType.Bomber:             return new Bomber(position, isPlayer);
                case ShipType.Corvette:           return new Corvette(position, isPlayer);
                case ShipType.Frigate:            return new Frigate(position, isPlayer);
                case ShipType.Destroyer:          return new Destroyer(position, isPlayer);
                case ShipType.Battlecruiser:      return new Battlecruiser(position, isPlayer);
                case ShipType.ResourceCollector:  return new ResourceCollector(position, isPlayer);
                case ShipType.Carrier:            return new Carrier(position, isPlayer);
                case ShipType.Probe:              return new Probe(position, isPlayer);
                default:                          return new Miner(position, isPlayer);
            }
        }

        public static PointF RandomOffset(PointF center, float radius)
        {
            double angle = rng.NextDouble() * Math.PI * 2;
            float  dist  = (float)(rng.NextDouble() * radius + radius * 0.5f);
            return new PointF(
                center.X + (float)Math.Cos(angle) * dist,
                center.Y + (float)Math.Sin(angle) * dist);
        }
    }
}
