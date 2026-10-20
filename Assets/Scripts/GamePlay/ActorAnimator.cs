using System.Collections.Generic;
using UnityEngine;

namespace ReverseDefense
{
    // State names are configurable: existing Animator Controllers do not need new parameters.
    [DisallowMultipleComponent]
    public sealed class ActorAnimator : MonoBehaviour
    {
        public Animator Animator;
        public string IdleState = "Base Layer.Idle";
        public string MoveState = "Base Layer.Move";
        public string AttackState = "Base Layer.Attack";
        [Tooltip("没有死亡动画可以留空。")]
        public string DeadState = "";
        private ActorState? current;
        private float playbackSpeed = 1;
        private bool hasState;
        private readonly HashSet<string> warned = new HashSet<string>();

        private void Awake()
        {
            if (Animator == null) Animator = GetComponentInChildren<Animator>(true);
            if (Animator != null) playbackSpeed = Animator.speed;
        }

        public void Apply(ActorState state, bool frozen)
        {
            if (Animator == null || Animator.runtimeAnimatorController == null || !Animator.isActiveAndEnabled) return;
            // Freeze the exact frame; do not restart the current animation on Resume.
            if (frozen) { Animator.speed = 0; return; }
            if (current != state)
            {
                current = state;
                string name = state == ActorState.Move ? MoveState : state == ActorState.Attack ? AttackState :
                    state == ActorState.Dead ? DeadState : IdleState;
                int hash = string.IsNullOrEmpty(name) ? 0 : UnityEngine.Animator.StringToHash(name);
                hasState = hash != 0 && Animator.HasState(0, hash);
                if (hasState) Animator.Play(hash, 0, 0);
                else if (!string.IsNullOrEmpty(name) && warned.Add(name))
                    Debug.LogWarning("Animator 中找不到状态 " + name + "，请检查 Actor Animator 的状态名称。", this);
            }
            Animator.speed = hasState ? playbackSpeed : 0;
        }

        private void OnDisable()
        {
            if (Animator != null) Animator.speed = playbackSpeed;
            current = null;
        }
    }
}
