using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReverseDefense
{
    public sealed class BeginSceneMenu : MonoBehaviour
    {
        [Header("从 Hierarchy 拖入按钮实例")]
        public Button StartButton;
        public Button QuitButton;
        public string GameSceneName = "GameScene";
        private bool loading;

        private void Start()
        {
            if (StartButton == null || QuitButton == null || StartButton == QuitButton ||
                !StartButton.gameObject.scene.IsValid() || !QuitButton.gameObject.scene.IsValid())
            {
                Debug.LogError("开始菜单：请从 Hierarchy 绑定两个不同的开始和退出按钮。", this);
                enabled = false;
                return;
            }
            StartButton.onClick.AddListener(StartGame);
            QuitButton.onClick.AddListener(QuitGame);
        }

        public void StartGame()
        {
            if (!enabled || loading) return;
            if (string.IsNullOrWhiteSpace(GameSceneName) ||
                !Application.CanStreamedLevelBeLoaded(GameSceneName))
            {
                Debug.LogError("无法进入场景：" + GameSceneName + "。请确认该场景已在 Build Settings 中勾选。", this);
                return;
            }
            loading = true;
            StartButton.interactable = false;
            SceneManager.LoadSceneAsync(GameSceneName, LoadSceneMode.Single);
        }

        public void QuitGame()
        {
            if (!enabled) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (StartButton != null) StartButton.onClick.RemoveListener(StartGame);
            if (QuitButton != null) QuitButton.onClick.RemoveListener(QuitGame);
        }
    }
}
