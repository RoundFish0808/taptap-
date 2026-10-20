using System;

namespace ReverseDefense
{
    // One clock per building. A full road discards that spawn instead of storing a backlog.
    public sealed class MonsterSpawnClock
    {
        private double elapsed;
        public int RoadIndex { get; private set; }
        public string Segment { get; private set; }
        public double Interval { get; private set; }
        public int Health { get; private set; }
        public int Attack { get; private set; }
        public bool RandomRoad { get; private set; }
        public bool Enabled = true;
        public double TimeToSpawn { get { return Enabled ? Interval - elapsed : double.PositiveInfinity; } }

        public MonsterSpawnClock(int roadIndex, string segment, double interval, int health, int attack,
            bool randomRoad = false)
        {
            if (roadIndex < 0 || string.IsNullOrWhiteSpace(segment) || !(interval > 0) ||
                double.IsInfinity(interval) || health <= 0 || attack <= 0)
                throw new ArgumentException("Invalid building spawn configuration.");
            RoadIndex = roadIndex;
            Segment = segment;
            Interval = interval;
            Health = health;
            Attack = attack;
            RandomRoad = randomRoad;
        }

        public bool Advance(double seconds)
        {
            if (!Enabled) return false;
            elapsed += seconds;
            if (elapsed + 1e-9 < Interval) return false;
            elapsed = 0;
            return true;
        }
    }
}
