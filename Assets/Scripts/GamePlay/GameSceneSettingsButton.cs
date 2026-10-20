using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReverseDefense
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class GameSceneSettingsButton : MonoBehaviour
    {
        public string BeginSceneName = "BeginScene";
        private Button button;
        private bool loading;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(ReturnToBeginScene);
        }

        public void ReturnToBeginScene()
        {
            if (!isActiveAndEnabled || loading) return;
            if (string.IsNullOrWhiteSpace(BeginSceneName) ||
                !Application.CanStreamedLevelBeLoaded(BeginSceneName))
            {
                Debug.LogError("无法返回场景：" + BeginSceneName + "。请确认该场景已在 Build Settings 中勾选。", this);
                return;
            }
            loading = true;
            button.interactable = false;
            SceneManager.LoadSceneAsync(BeginSceneName, LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(ReturnToBeginScene);
        }
    }
}
