using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ReverseDefense
{
    [RequireComponent(typeof(GridMapView))]
    public sealed class FixedLoopGamePlay : MonoBehaviour
    {
        [Header("网格：横向列数 × 纵向行数")]
        [Min(2)] public int Columns = 20;
        [Min(2)] public int Rows = 10;
        [Tooltip("每个建筑仍占 1 格；此值只控制一格在 UI 中的显示大小。")]
        [Min(8f)] public float CellPixels = 64f;

        [Header("主道路边界：左下角为起点，坐标从 0 开始")]
        public int Left = 5;
        public int Right = 14;
        public int Bottom = 2;
        public int Top = 7;

        [Header("行走")]
        [Min(0.01f)] public float StepSeconds = 0.3f;
        [SerializeField] private bool paused;
        [SerializeField] private int completedLaps;
        [SerializeField] private int pathIndex;

        [Header("可选：普通 UI Text，需绑定中文字体")]
        public Text LapText;
        public Text StateText;
        public Text PauseButtonText;

        private GridMapView map;
        private RectTransform player;
        private List<Cell> path;
        private float progress;

        private void Start()
        {
            try { path = FixedLoopPath.Create(Columns, Rows, Left, Bottom, Right, Top); }
            catch (ArgumentException error)
            {
                Debug.LogError("主道路配置错误：" + error.Message, this);
                enabled = false;
                return;
            }

            map = GetComponent<GridMapView>();
            if (map.MapRoot == null || map.Player == null || map.Columns != Columns ||
                map.Rows != Rows || !Mathf.Approximately(map.CellPixels, CellPixels))
            {
                Debug.LogError("请退出 Play，在组件上点击“生成/更新 UGUI 地图”，并保存场景。", this);
                enabled = false;
                return;
            }
            player = map.Player;
            pathIndex = 0; completedLaps = 0; progress = 0;
            player.anchoredPosition = map.CellToLocal(path[0]);
            RefreshHUD();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (paused || path == null) return;

            progress += Time.deltaTime / Mathf.Max(0.01f, StepSeconds);
            // 一帧可能跨过多个格子；逐格结算，避免低帧率时漏算圈数。
            while (progress >= 1f)
            {
                progress -= 1f;
                pathIndex = (pathIndex + 1) % path.Count;
                if (pathIndex == 0)
                {
                    completedLaps++;
                    Debug.Log("完成主道路第 " + completedLaps + " 圈", this);
                    RefreshHUD();
                }
            }
            int nextIndex = (pathIndex + 1) % path.Count;
            player.anchoredPosition = Vector2.Lerp(map.CellToLocal(path[pathIndex]),
                map.CellToLocal(path[nextIndex]), progress);
        }

        public void TogglePause()
        {
            paused = !paused;
            RefreshHUD();
        }

        private void RefreshHUD()
        {
            if (LapText != null) LapText.text = "当前圈数：" + completedLaps;
            if (StateText != null) StateText.text = paused ? "已暂停" : "行走中";
            if (PauseButtonText != null) PauseButtonText.text = paused ? "继续" : "暂停";
        }
    }
}
