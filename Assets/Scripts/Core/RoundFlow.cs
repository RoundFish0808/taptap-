using System;

namespace ReverseDefense
{
    public sealed class RoundFlow
    {
        public int Total { get; private set; }
        public int Current { get; private set; }
        public bool Running { get; private set; }
        public bool Failed { get; private set; }
        public bool Finished { get { return !Running && Current == Total && !Failed; } }
        public bool CanStart { get { return !Running && !Failed && Current < Total; } }

        public RoundFlow(int total)
        {
            if (total < 1) throw new ArgumentOutOfRangeException(nameof(total));
            Total = total;
        }

        public bool TryStart()
        {
            if (!CanStart) return false;
            Current++;
            Running = true;
            return true;
        }

        public void Complete() { Running = false; }
        public void Fail() { Failed = true; Running = false; }
    }
}
