using System;

namespace ReverseDefense
{
    // Only advances between logical road nodes; the view interpolates their positions.
    public sealed class LoopWalker
    {
        private const double Epsilon = 1e-9;
        private double elapsed;
        public int Count { get; private set; }
        public int Index { get; private set; }
        public int NextIndex { get { return (Index + 1) % Count; } }
        public int Laps { get; private set; }
        public double StepSeconds { get; private set; }
        public double Progress { get { return elapsed / StepSeconds; } }
        public double TimeToArrival { get { return StepSeconds - elapsed; } }

        public LoopWalker(int count, double stepSeconds)
        {
            if (count < 2 || !(stepSeconds > 0) || double.IsInfinity(stepSeconds))
                throw new ArgumentException("Route needs at least two nodes and a positive step duration.");
            Count = count;
            StepSeconds = stepSeconds;
        }

        // The session slices time at arrival boundaries, so no road node is skipped.
        public void Advance(double seconds)
        {
            elapsed += seconds;
            if (elapsed + Epsilon < StepSeconds) return;
            elapsed = 0;
            Index = NextIndex;
            if (Index == 0) Laps++;
        }
    }
}
