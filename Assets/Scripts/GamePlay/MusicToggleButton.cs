using UnityEngine;
using UnityEngine.UI;

namespace ReverseDefense
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class MusicToggleButton : MonoBehaviour
    {
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(ToggleMusic);
        }

        public void ToggleMusic()
        {
            if (isActiveAndEnabled) SceneMusic.ToggleMute();
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(ToggleMusic);
        }
    }
}
