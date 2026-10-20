using System;

namespace ReverseDefense
{
    public enum ActorState { Idle, Move, Attack, Dead }

    // Logical state is independent of Animator and remains unchanged while paused.
    public sealed class ActorStateMachine
    {
        public ActorState Current { get; private set; }
        public event Action<ActorState> Changed;

        public void Refresh(bool alive, bool running, bool fighting)
        {
            ActorState next = !alive ? ActorState.Dead : !running ? ActorState.Idle :
                fighting ? ActorState.Attack : ActorState.Move;
            if (next == Current) return;
            Current = next;
            Changed?.Invoke(next);
        }
    }
}
