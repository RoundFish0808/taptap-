using System;
using System.Collections.Generic;

namespace ReverseDefense
{
    // Coordinates the clock, movement and combat. No Unity or UI dependencies.
    public sealed class PatrolSession
    {
        private readonly List<MonsterSpawnClock> spawners = new List<MonsterSpawnClock>();
        private readonly List<RoadMonster> monsters = new List<RoadMonster>();
        private readonly int roadCapacity;
        private readonly Random random;
        public LoopWalker Walker { get; private set; }
        public CombatUnit Hero { get; private set; }
        public AutoBattle Battle { get; private set; }
        public RoundFlow Rounds { get; private set; }
        public IReadOnlyList<RoadMonster> Monsters { get { return monsters.AsReadOnly(); } }
        public bool Paused;
        public int Kills { get; private set; }
        public event Action<RoadMonster> Spawned;
        public event Action<RoadMonster> Defeated;
        public event Action<int> LapCompleted;

        public PatrolSession(int nodes, double stepSeconds, int heroHealth, int heroAttack,
            double attackSeconds, int roadCapacity, int totalRounds = 10, int? randomSeed = null)
        {
            if (roadCapacity < 1) throw new ArgumentOutOfRangeException(nameof(roadCapacity));
            Walker = new LoopWalker(nodes, stepSeconds);
            Hero = new CombatUnit(heroHealth, heroAttack);
            Battle = new AutoBattle(Hero, attackSeconds);
            Rounds = new RoundFlow(totalRounds);
            random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
            this.roadCapacity = roadCapacity;
        }

        public bool CanStartOrResume { get { return Hero.Alive && (Rounds.CanStart || (Rounds.Running && Paused)); } }

        // The Start button resumes a paused round before considering a new round.
        public bool StartRound()
        {
            if (!CanStartOrResume) return false;
            if (!Rounds.Running && !Rounds.TryStart()) return false;
            Paused = false;
            FindEncounter();
            return true;
        }

        public void Pause()
        {
            if (Rounds.Running && Hero.Alive) Paused = true;
        }

        public void AddSpawner(MonsterSpawnClock spawner)
        {
            if (spawner == null || spawner.RoadIndex >= Walker.Count)
                throw new ArgumentException("Spawn node must belong to the road.");
            if (!spawners.Contains(spawner)) spawners.Add(spawner);
        }

        public void Tick(double seconds)
        {
            if (Paused || !Rounds.Running || !Hero.Alive || !(seconds > 0) || double.IsInfinity(seconds)) return;
            while (seconds > 1e-9 && Hero.Alive && !Paused && Rounds.Running)
            {
                FindEncounter();
                double step = Math.Min(seconds, Battle.Target == null ? Walker.TimeToArrival : Battle.TimeToAttack);
                foreach (var spawner in spawners) step = Math.Min(step, spawner.TimeToSpawn);
                int oldLap = Walker.Laps;
                if (Battle.Target == null) Walker.Advance(step);
                else Battle.Advance(step);
                seconds -= step;

                if (Battle.Target != null && !Battle.Target.Unit.Alive)
                {
                    RoadMonster defeated = Battle.Target;
                    Battle.End();
                    monsters.Remove(defeated);
                    Kills++;
                    Defeated?.Invoke(defeated);
                }
                if (!Hero.Alive) { Rounds.Fail(); break; }
                foreach (var spawner in spawners)
                    if (spawner.Advance(step)) TrySpawn(spawner);
                if (Walker.Laps != oldLap) LapCompleted?.Invoke(Walker.Laps);
                FindEncounter();
                // Clear the start node before ending the lap's round.
                if (Walker.Laps == Rounds.Current && Walker.Progress == 0 && Battle.Target == null)
                    Rounds.Complete();
            }
        }

        private void FindEncounter()
        {
            if (Battle.Target != null || Walker.Progress > 0) return;
            foreach (var monster in monsters)
                if (monster.RoadIndex == Walker.Index && monster.Unit.Alive)
                {
                    Battle.Begin(monster);
                    return;
                }
        }

        private void TrySpawn(MonsterSpawnClock spawner)
        {
            int count = 0;
            foreach (var monster in monsters)
                if (monster.Segment == spawner.Segment && monster.Unit.Alive) count++;
            if (count >= roadCapacity) return;
            int roadIndex = spawner.RandomRoad ? PickFreeRoadNode() : spawner.RoadIndex;
            if (roadIndex < 0) return;
            var created = new RoadMonster(roadIndex, spawner.Segment, spawner.Health, spawner.Attack);
            monsters.Add(created);
            Spawned?.Invoke(created);
        }

        private int PickFreeRoadNode()
        {
            // Reservoir sampling selects uniformly without allocating a list every spawn.
            int selected = -1, candidates = 0;
            for (int index = 0; index < Walker.Count; index++)
            {
                if (index == Walker.Index) continue;
                bool occupied = false;
                foreach (var monster in monsters)
                    if (monster.RoadIndex == index && monster.Unit.Alive) { occupied = true; break; }
                if (!occupied && random.Next(++candidates) == 0) selected = index;
            }
            return selected;
        }
    }
}

