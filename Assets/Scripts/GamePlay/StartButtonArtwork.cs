using UnityEngine;
using UnityEngine.UI;

namespace ReverseDefense
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class StartButtonArtwork : MonoBehaviour
    {
        public FixedLoopGamePlay GamePlay;
        [Tooltip("需要换贴图的 Image，留空则使用按钮的 Target Graphic。")]
        public Image Artwork;
        public Sprite NormalSprite;
        public Sprite RunningSprite;

        private void Awake()
        {
            if (Artwork == null) Artwork = GetComponent<Button>().targetGraphic as Image;
            if (Artwork == null || GamePlay == null || RunningSprite == null)
            {
                Debug.LogError("开始按钮贴图：请绑定 Game Play、Image 和 Running Sprite。", this);
                enabled = false;
                return;
            }
            if (NormalSprite == null) NormalSprite = Artwork.sprite;
        }

        private void LateUpdate()
        {
            var session = GamePlay.Session;
            bool running = session != null && session.Rounds.Running && !session.Paused;
            var sprite = running ? RunningSprite : NormalSprite;
            if (Artwork.sprite != sprite) Artwork.sprite = sprite;
        }
    }
}
