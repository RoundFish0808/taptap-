using System;

namespace ReverseDefense
{
    // One button press runs one lap. Pausing preserves the current segment progress.
    public sealed class MovementSession
    {
        public LoopWalker Walker { get; private set; }
        public RoundFlow Rounds { get; private set; }
        public bool Paused { get; private set; }
        public bool CanStartOrResume { get { return Rounds.CanStart || (Rounds.Running && Paused); } }

        public MovementSession(int nodeCount, double stepSeconds, int rounds = 10)
        {
            Walker = new LoopWalker(nodeCount, stepSeconds);
            Rounds = new RoundFlow(rounds);
        }

        public bool StartRound()
        {
            if (!CanStartOrResume) return false;
            if (!Rounds.Running) Rounds.TryStart();
            Paused = false;
            return true;
        }

        public void Pause()
        {
            if (Rounds.Running) Paused = true;
        }

        public void Tick(double seconds)
        {
            if (Paused || !Rounds.Running || !(seconds > 0) || double.IsInfinity(seconds)) return;
            while (seconds > 0 && Rounds.Running)
            {
                double step = Math.Min(seconds, Walker.TimeToArrival);
                Walker.Advance(step);
                seconds -= step;
                if (Walker.Laps >= Rounds.Current) Rounds.Complete();
            }
        }
    }
}
