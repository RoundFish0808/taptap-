using UnityEngine;
using UnityEngine.UI;

namespace ReverseDefense
{
    // One component per detail panel; keep it on the always-active UIManager.
    public sealed class CharacterDetailPanelSwitch : MonoBehaviour
    {
        [Header("从 Hierarchy 拖入场景界面")]
        public GameObject FormationPanel;
        public GameObject DetailPanel;

        [Header("打开详情的按钮（可以绑定多个）")]
        public Button[] OpenButtons = new Button[0];
        public Button BackButton;

        private bool initialized;

        private void Start()
        {
            if (!IsSceneObject(FormationPanel) || !IsSceneObject(DetailPanel) ||
                FormationPanel == DetailPanel ||
                FormationPanel.transform.IsChildOf(DetailPanel.transform) ||
                DetailPanel.transform.IsChildOf(FormationPanel.transform) ||
                transform.IsChildOf(FormationPanel.transform) ||
                transform.IsChildOf(DetailPanel.transform))
            {
                Debug.LogError("角色详情配置错误：界面必须从 Hierarchy 拖入，两个界面不能互为父子；脚本应挂在两个界面之外的常驻 UIManager 上。", this);
                enabled = false;
                return;
            }
            if (BackButton == null)
            {
                ReportButtonError("Back Button 未绑定。请拖入详情界面返回按钮上的 Button 组件。");
                return;
            }
            if (!BackButton.gameObject.scene.IsValid())
            {
                ReportButtonError("Back Button 引用了 Project 资源：" + BackButton.name + "。请改为 Hierarchy 中详情界面的返回按钮。");
                return;
            }
            if (OpenButtons == null || OpenButtons.Length == 0)
            {
                ReportButtonError("Open Buttons 数量为0。请展开列表，把 Size 设为至少1，并填写 Element 0。");
                return;
            }
            for (int i = 0; i < OpenButtons.Length; i++)
            {
                var button = OpenButtons[i];
                if (button == null)
                {
                    ReportButtonError("Open Buttons 的 Element " + i + " 未绑定。");
                    return;
                }
                if (!button.gameObject.scene.IsValid())
                {
                    ReportButtonError("Open Buttons 的 Element " + i + " 引用了 Project 资源，请改拖 Hierarchy 中的按钮。");
                    return;
                }
                if (button == BackButton)
                {
                    ReportButtonError("Open Buttons 的 Element " + i + " 与 Back Button 是同一个按钮。");
                    return;
                }
            }

            foreach (var button in OpenButtons) button.onClick.AddListener(OpenDetail);
            BackButton.onClick.AddListener(ReturnToFormation);
            // Do not activate FormationPanel here: the game screen is initially visible.
            DetailPanel.SetActive(false);
            initialized = true;
        }

        public void OpenDetail()
        {
            if (!initialized || !enabled || !FormationPanel.activeInHierarchy) return;
            FormationPanel.SetActive(false);
            DetailPanel.SetActive(true);
        }

        public void ReturnToFormation()
        {
            if (!initialized || !enabled || !DetailPanel.activeInHierarchy) return;
            DetailPanel.SetActive(false);
            FormationPanel.SetActive(true);
        }

        private static bool IsSceneObject(GameObject panel)
        {
            return panel != null && panel.scene.IsValid();
        }

        private void ReportButtonError(string reason)
        {
            Debug.LogError("角色详情配置错误：" + reason, this);
            enabled = false;
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            foreach (var button in OpenButtons)
                if (button != null) button.onClick.RemoveListener(OpenDetail);
            if (BackButton != null) BackButton.onClick.RemoveListener(ReturnToFormation);
        }
    }
}
