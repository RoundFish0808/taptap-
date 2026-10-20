using UnityEngine;
using UnityEngine.UI;

namespace ReverseDefense
{
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class RoadActorView : MonoBehaviour
    {
        public Image Artwork;
        public Text HealthText;
        public ActorAnimator Animation;
        private Animator[] animators;
        private float[] animatorSpeeds;
        private Color normalColor;
        private float flash;
        public RectTransform Rect { get { return (RectTransform)transform; } }

        private void Awake()
        {
            if (Artwork == null) Artwork = GetComponentInChildren<Image>(true);
            if (Artwork != null) normalColor = Artwork.color;
            if (Animation == null) Animation = GetComponentInChildren<ActorAnimator>(true);
            animators = GetComponentsInChildren<Animator>(true);
            animatorSpeeds = new float[animators.Length];
            for (int i = 0; i < animators.Length; i++) animatorSpeeds[i] = animators[i].speed;
        }

        public void ShowDamage() { flash = 0.15f; }

        // Keep the prefab's default animation; no state switching is required.
        public void SetPaused(bool paused)
        {
            for (int i = 0; i < animators.Length; i++)
                if (animators[i] != null) animators[i].speed = paused ? 0 : animatorSpeeds[i];
        }

        public void Render(CombatUnit unit, float seconds)
        {
            flash = Mathf.Max(0, flash - seconds);
            if (Artwork != null)
                Artwork.color = !unit.Alive ? new Color(0.4f, 0.4f, 0.4f, normalColor.a) :
                    flash > 0 ? new Color(1, 0.3f, 0.3f, normalColor.a) : normalColor;
            if (HealthText != null) HealthText.text = unit.Health + "/" + unit.MaxHealth;
        }

        public void Animate(ActorState state, bool frozen)
        {
            // Monsters with only a default looping Animator also participate in Pause.
            for (int i = 0; i < animators.Length; i++)
                if (animators[i] != null && (Animation == null || animators[i] != Animation.Animator))
                    animators[i].speed = frozen ? 0 : animatorSpeeds[i];
            if (Animation != null) Animation.Apply(state, frozen);
        }
    }
}
