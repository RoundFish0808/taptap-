using System;
using ReverseDefense;

internal static class PatrolChecks
{
    public static void Run(Action<bool, string> check)
    {
        var movement = new MovementSession(4, 1);
        movement.Tick(10);
        check(movement.Walker.Index == 0 && movement.Walker.Progress == 0,
            "Movement waits for Start");
        check(movement.StartRound() && !movement.StartRound(), "Start cannot skip a running round");
        movement.Tick(1.5);
        check(movement.Walker.Index == 1 && movement.Walker.Progress == 0.5,
            "Movement interpolates and visits node boundaries");
        movement.Pause(); movement.Pause(); movement.Tick(100);
        check(movement.Paused && movement.Walker.Progress == 0.5, "Repeated Pause preserves progress");
        check(movement.StartRound() && movement.Rounds.Current == 1, "Start resumes the same round");
        movement.Tick(100);
        check(movement.Walker.Laps == 1 && movement.Walker.Index == 0 &&
            movement.Walker.Progress == 0 && !movement.Rounds.Running,
            "Large delta stops at one completed lap");
        for (int i = 2; i <= 10; i++) { movement.StartRound(); movement.Tick(100); }
        check(movement.Rounds.Finished && movement.Walker.Laps == 10 && !movement.StartRound(),
            "Movement stops after ten manually started rounds");
        var invalidTime = new MovementSession(3, 0.3);
        invalidTime.StartRound();
        invalidTime.Tick(double.NaN); invalidTime.Tick(double.PositiveInfinity); invalidTime.Tick(-1);
        check(invalidTime.Walker.Progress == 0, "Invalid delta cannot corrupt movement");
        var randomRoad = new PatrolSession(6, 100, 100, 4, 1, 4, 10, 42);
        randomRoad.AddSpawner(new MonsterSpawnClock(0, "main", 1, 8, 2, true));
        randomRoad.StartRound(); randomRoad.Tick(10);
        var occupied = new System.Collections.Generic.HashSet<int>();
        bool validNodes = true;
        foreach (var monster in randomRoad.Monsters)
            validNodes &= monster.RoadIndex > 0 && monster.RoadIndex < 6 && occupied.Add(monster.RoadIndex);
        check(randomRoad.Monsters.Count == 4 && validNodes,
            "Random spawning uses distinct road nodes, excludes the hero and obeys capacity");
        var shortRoad = new PatrolSession(2, 100, 100, 4, 1, 4, 10, 42);
        shortRoad.AddSpawner(new MonsterSpawnClock(0, "main", 1, 8, 2, true));
        shortRoad.StartRound(); shortRoad.Tick(10);
        check(shortRoad.Monsters.Count == 1 && shortRoad.Monsters[0].RoadIndex == 1,
            "Two-node roads safely skip spawns when no unoccupied node remains");
        var choices = new System.Collections.Generic.HashSet<int>();
        for (int seed = 0; seed < 20; seed++)
        {
            var sample = new PatrolSession(6, 100, 100, 4, 1, 4, 10, seed);
            sample.AddSpawner(new MonsterSpawnClock(0, "main", 1, 8, 2, true));
            sample.StartRound(); sample.Tick(1);
            choices.Add(sample.Monsters[0].RoadIndex);
        }
        check(choices.Count > 1, "Random spawns are not locked to one road node");
        var randomPaused = new PatrolSession(6, 100, 100, 4, 1, 4, 10, 42);
        randomPaused.AddSpawner(new MonsterSpawnClock(0, "main", 1, 8, 2, true));
        randomPaused.StartRound(); randomPaused.Tick(0.5); randomPaused.Pause(); randomPaused.Tick(50);
        check(randomPaused.Monsters.Count == 0, "Random spawn clock freezes during pause");
        randomPaused.StartRound(); randomPaused.Tick(0.5);
        check(randomPaused.Monsters.Count == 1 && randomPaused.Rounds.Current == 1,
            "Resuming preserves the random spawn timer without adding a round");

        var rounds = NewSession();
        rounds.Tick(100);
        check(rounds.Walker.Index == 0 && rounds.Walker.Progress == 0 && rounds.Rounds.Current == 0,
            "Patrol waits for the start button");
        check(rounds.StartRound() && !rounds.StartRound(), "Repeated start cannot skip a running round");
        rounds.Tick(100);
        check(rounds.Rounds.Current == 1 && !rounds.Rounds.Running && rounds.Walker.Laps == 1,
            "A long frame stops at one completed lap, never auto-starts another round");
        for (int i = 1; i < 10; i++) { rounds.StartRound(); rounds.Tick(100); }
        check(rounds.Rounds.Finished && rounds.Walker.Laps == 10 && !rounds.StartRound(),
            "Exactly ten manually started rounds, no eleventh round");

        var spawn = new PatrolSession(100, 1, 100, 4, 1, 4);
        spawn.AddSpawner(new MonsterSpawnClock(50, "main", 2, 8, 2));
        spawn.StartRound(); spawn.Tick(1.9);
        check(spawn.Monsters.Count == 0, "No monsters before the first building interval");
        spawn.Tick(0.1);
        check(spawn.Monsters.Count == 1 && spawn.Monsters[0].Unit.Health == 8 && spawn.Monsters[0].RoadIndex == 50,
            "Building spawns the configured monster at its road node on the interval boundary");
        spawn.Tick(8);
        check(spawn.Monsters.Count == 4, "Full road discards periodic spawns at the capacity limit");

        var shared = new PatrolSession(100, 1, 100, 4, 1, 4);
        shared.AddSpawner(new MonsterSpawnClock(50, "main", 1, 8, 2));
        shared.AddSpawner(new MonsterSpawnClock(51, "main", 1, 8, 2));
        shared.AddSpawner(new MonsterSpawnClock(52, "other", 1, 8, 2));
        shared.StartRound(); shared.Tick(3);
        check(shared.Monsters.Count == 7, "Buildings on one segment share capacity, other segments are independent");

        var combat = NewSession();
        combat.AddSpawner(new MonsterSpawnClock(1, "main", 1, 8, 2));
        combat.StartRound(); combat.Tick(1);
        RoadMonster first = combat.Battle.Target;
        check(first != null && combat.Walker.Index == 1 && combat.Walker.Progress == 0,
            "Hero stops exactly on an occupied road node");
        combat.Tick(0.25);
        check(first.Unit.Health == 4 && combat.Hero.Health == 98 && combat.Walker.Progress == 0,
            "Attack interval damages monster then lets a surviving monster retaliate");
        combat.Tick(0.25);
        check(combat.Kills == 1 && combat.Monsters.Count == 0 && combat.Hero.Health == 98,
            "Dead monster is removed once and cannot retaliate");
        combat.Tick(0.5);
        check(combat.Monsters.Count == 1 && combat.Battle.Target == null && Math.Abs(combat.Walker.Progress - 0.5) < 1e-8,
            "A monster spawned behind an already moving hero waits for a future visit");

        var ending = new PatrolSession(4, 1, 100, 4, 1, 4);
        ending.AddSpawner(new MonsterSpawnClock(0, "main", 3.5, 8, 2));
        ending.StartRound(); ending.Tick(4);
        check(ending.Walker.Laps == 1 && ending.Rounds.Running && ending.Battle.Target != null,
            "Returning to start does not finish the round while its monster is alive");
        ending.Tick(2);
        check(!ending.Rounds.Running && ending.Kills == 1 && ending.Walker.Index == 0,
            "Finishing the start-node battle completes the round without moving again");
        ending.Tick(100);
        ending.StartRound(); ending.Tick(1);
        check(ending.Monsters.Count == 1, "Spawn timers preserve partial elapsed time across waiting rounds");

        var paused = NewSession();
        paused.AddSpawner(new MonsterSpawnClock(1, "main", 1, 8, 2));
        paused.StartRound(); paused.Tick(1.1); paused.Pause();
        paused.Tick(50);
        check(paused.Hero.Health == 100 && paused.Battle.Target.Unit.Health == 8 && paused.Monsters.Count == 1,
            "Pause freezes battle, movement and spawn clocks together");
        paused.Pause();
        check(paused.Paused && paused.CanStartOrResume, "Repeated Pause stays paused and enables Start to resume");
        check(paused.StartRound() && paused.Rounds.Current == 1 && !paused.Paused && !paused.CanStartOrResume,
            "Start resumes the current battle without consuming a new round");
        paused.Tick(0.15);
        check(paused.Hero.Health == 98, "Resume preserves the unfinished attack interval");

        var movingPause = NewSession();
        movingPause.StartRound(); movingPause.Tick(0.4); movingPause.Pause(); movingPause.Tick(10);
        check(movingPause.Walker.Index == 0 && Math.Abs(movingPause.Walker.Progress - 0.4) < 1e-8,
            "Pause preserves the exact position between road nodes");
        movingPause.StartRound(); movingPause.Tick(0.6);
        check(movingPause.Walker.Index == 1 && movingPause.Rounds.Current == 1,
            "Start continues the unfinished move in the same round");

        var death = new PatrolSession(4, 1, 1, 1, 0.25, 4);
        death.AddSpawner(new MonsterSpawnClock(1, "main", 1, 8, 2));
        death.StartRound(); death.Tick(100);
        check(death.Rounds.Failed && death.Hero.Health == 0 && !death.StartRound() && death.Monsters.Count == 1,
            "Hero death stops all progression and disables further rounds");

        var capacity = new PatrolSession(4, 1, 100, 8, 0.25, 1);
        capacity.AddSpawner(new MonsterSpawnClock(1, "main", 0.5, 8, 1));
        capacity.StartRound(); capacity.Tick(1.25);
        check(capacity.Kills == 1 && capacity.Monsters.Count == 0,
            "Killing a monster releases capacity without replaying skipped spawns");
        capacity.Tick(0.25);
        check(capacity.Monsters.Count == 1, "Released capacity is reused at the next scheduled spawn");

        var disabled = NewSession();
        var disabledClock = new MonsterSpawnClock(2, "main", 1, 8, 2) { Enabled = false };
        disabled.AddSpawner(disabledClock); disabled.StartRound(); disabled.Tick(1);
        check(disabled.Monsters.Count == 0, "Disabled building does not advance its clock");
        disabledClock.Enabled = true; disabled.Tick(1);
        check(disabled.Monsters.Count == 1, "Reenabled building resumes its own spawn clock");

        var largeFrame = NewSession(); var smallFrames = NewSession();
        largeFrame.AddSpawner(new MonsterSpawnClock(1, "main", 1.3, 8, 2));
        smallFrames.AddSpawner(new MonsterSpawnClock(1, "main", 1.3, 8, 2));
        largeFrame.StartRound(); smallFrames.StartRound(); largeFrame.Tick(8);
        for (int i = 0; i < 800; i++) smallFrames.Tick(0.01);
        check(largeFrame.Kills == smallFrames.Kills && largeFrame.Hero.Health == smallFrames.Hero.Health &&
            largeFrame.Monsters.Count == smallFrames.Monsters.Count && largeFrame.Walker.Laps == smallFrames.Walker.Laps,
            "Long and short frames produce identical movement, spawning and combat outcomes");

        bool rejected = false;
        try { NewSession().AddSpawner(new MonsterSpawnClock(4, "main", 1, 8, 2)); }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "Invalid spawn node is rejected during setup");
    }

    private static PatrolSession NewSession() { return new PatrolSession(4, 1, 100, 4, 0.25, 4); }
}
