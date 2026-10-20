using System;

namespace ReverseDefense
{
    public sealed class AutoBattle
    {
        private readonly CombatUnit hero;
        private readonly double interval;
        private double elapsed;
        public RoadMonster Target { get; private set; }
        public double TimeToAttack { get { return interval - elapsed; } }
        public event Action<CombatUnit> Damaged;

        public AutoBattle(CombatUnit hero, double interval)
        {
            if (hero == null || !(interval > 0) || double.IsInfinity(interval))
                throw new ArgumentException("Battle needs a hero and a positive attack interval.");
            this.hero = hero;
            this.interval = interval;
        }

        public void Begin(RoadMonster monster) { Target = monster; elapsed = 0; }
        public void End() { Target = null; elapsed = 0; }

        public void Advance(double seconds)
        {
            if (Target == null || !hero.Alive) return;
            elapsed += seconds;
            if (elapsed + 1e-9 < interval) return;
            elapsed = 0;
            Target.Unit.TakeDamage(hero.Attack);
            Damaged?.Invoke(Target.Unit);
            // A killed monster cannot retaliate in this round.
            if (!Target.Unit.Alive) return;
            hero.TakeDamage(Target.Unit.Attack);
            Damaged?.Invoke(hero);
        }
    }
}
