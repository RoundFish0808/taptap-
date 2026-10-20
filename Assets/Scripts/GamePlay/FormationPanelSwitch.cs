using UnityEngine;
using UnityEngine.UI;

namespace ReverseDefense
{
    // Attach to an always-active object outside both panels.
    public sealed class FormationPanelSwitch : MonoBehaviour
    {
        [Header("已有界面根物体（两个界面不能互为父子）")]
        public GameObject GamePanel;
        public GameObject FormationPanel;

        [Header("已有按钮，代码自动绑定点击事件")]
        public Button OpenButton;
        public Button CloseButton;

        [Tooltip("绑定当前玩法控制器：打开编队时暂停，返回后点击开始继续。")]
        public FixedLoopGamePlay GamePlay;

        private void Start()
        {
            if ((GamePanel != null && !GamePanel.scene.IsValid()) ||
                (FormationPanel != null && !FormationPanel.scene.IsValid()))
            {
                Debug.LogError("Game Panel 和 Formation Panel 必须从 Hierarchy 拖入场景中的界面，不能从 Project 拖入预制体资源。", this);
                enabled = false;
                return;
            }
            if (GamePanel == null || FormationPanel == null ||
                OpenButton == null || CloseButton == null ||
                GamePanel == FormationPanel ||
                GamePanel.transform.IsChildOf(FormationPanel.transform) ||
                FormationPanel.transform.IsChildOf(GamePanel.transform) ||
                transform.IsChildOf(GamePanel.transform) ||
                transform.IsChildOf(FormationPanel.transform))
            {
                Debug.LogError("编队界面配置错误：请绑定两个独立界面和打开/关闭按钮，并把切换脚本挂在两个界面之外的常驻物体上。", this);
                enabled = false;
                return;
            }

            OpenButton.onClick.AddListener(OpenFormation);
            CloseButton.onClick.AddListener(CloseFormation);
            CloseFormation();
        }

        public void OpenFormation()
        {
            if (!enabled || GamePanel == null || FormationPanel == null) return;
            if (GamePlay != null) GamePlay.PauseGame();
            GamePanel.SetActive(false);
            FormationPanel.SetActive(true);
        }

        public void CloseFormation()
        {
            if (!enabled || GamePanel == null || FormationPanel == null) return;
            FormationPanel.SetActive(false);
            GamePanel.SetActive(true);
        }

        private void OnDestroy()
        {
            if (OpenButton != null) OpenButton.onClick.RemoveListener(OpenFormation);
            if (CloseButton != null) CloseButton.onClick.RemoveListener(CloseFormation);
        }
    }
}
