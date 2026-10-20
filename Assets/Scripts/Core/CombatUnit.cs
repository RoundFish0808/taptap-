using System;

namespace ReverseDefense
{
    public sealed class CombatUnit
    {
        public int MaxHealth { get; private set; }
        public int Health { get; private set; }
        public int Attack { get; private set; }
        public bool Alive { get { return Health > 0; } }

        public CombatUnit(int health, int attack)
        {
            if (health <= 0 || attack <= 0) throw new ArgumentException("Health and attack must be positive.");
            MaxHealth = Health = health;
            Attack = attack;
        }

        public void TakeDamage(int damage) { Health = Math.Max(0, Health - Math.Max(0, damage)); }
    }

    public sealed class RoadMonster
    {
        public int RoadIndex { get; private set; }
        public string Segment { get; private set; }
        public CombatUnit Unit { get; private set; }

        public RoadMonster(int roadIndex, string segment, int health, int attack)
        {
            RoadIndex = roadIndex;
            Segment = segment;
            Unit = new CombatUnit(health, attack);
        }
    }
}
