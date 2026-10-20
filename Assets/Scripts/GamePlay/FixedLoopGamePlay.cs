using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ReverseDefense
{
    // Scene bindings only; movement timing and rounds live in Core.
    [RequireComponent(typeof(GridMapView))]
    public sealed class FixedLoopGamePlay : MonoBehaviour
    {
        [Header("旧网格路线（填写 Road Nodes 后不使用）")]
        [Min(2)] public int Columns = 20;
        [Min(2)] public int Rows = 10;
        [Min(8f)] public float CellPixels = 64;
        public int Left = 5, Right = 14, Bottom = 2, Top = 7;

        [Header("人物移动")]
        [Tooltip("从一个道路节点移动到下一个节点的秒数。")]
        [Min(0.01f)] public float StepSeconds = 0.3f;

        [Header("道路随机生怪")]
        [Tooltip("直接拖入 Project 文件夹中的 UI 怪物预制体，无需预先挂脚本。")]
        public GameObject MonsterPrefab;
        public RectTransform MonsterRoot;
        [Min(0.01f)] public float SpawnIntervalSeconds = 3;
        [Min(1)] public int RoadCapacity = 4;
        [Min(1)] public int MonsterHealth = 8;
        [Min(1)] public int MonsterAttack = 2;

        [Header("自动战斗（原型数值）")]
        [Min(1)] public int HeroHealth = 100;
        [Min(1)] public int HeroAttack = 4;
        [Min(0.01f)] public float AttackSeconds = 1;

        [Header("拖入现有 Button 和普通 UI Text")]
        public Button StartButton;
        public Button PauseButton;
        public Text RoundText;
        public Text LapText;
        public Text StateText;
        public Text PauseButtonText;
        public Text HeroHealthText;

        public PatrolSession Session { get; private set; }
        private GridMapView map;
        private List<Cell> path;
        private RoadCombatPresenter combatView;

        private void Start()
        {
            try
            {
                map = GetComponent<GridMapView>();
                path = map.HasManualRoad ? null : FixedLoopPath.Create(Columns, Rows, Left, Bottom, Right, Top);
                int count = map.ValidateRoad(path);
                if (!map.HasManualRoad && (map.Columns != Columns || map.Rows != Rows ||
                    !Mathf.Approximately(map.CellPixels, CellPixels)))
                    throw new ArgumentException("旧网格尺寸不一致，请检查已保存地图，或改用 Road Nodes。");
                if (MonsterPrefab == null || MonsterRoot == null)
                    throw new ArgumentException("请从 Project 文件夹绑定 Monster Prefab，并绑定场景中的 Monster Root。");
                if (MonsterPrefab.GetComponent<RectTransform>() == null)
                    throw new ArgumentException("当前道路使用 UI Image，怪物预制体根物体需要 RectTransform。");
                Session = new PatrolSession(count, StepSeconds, HeroHealth, HeroAttack, AttackSeconds, RoadCapacity);
                Session.AddSpawner(new MonsterSpawnClock(0, "road", SpawnIntervalSeconds,
                    MonsterHealth, MonsterAttack, true));
                combatView = new RoadCombatPresenter(Session, MonsterPrefab, MonsterRoot, map, path);
                if (StartButton != null) StartButton.onClick.AddListener(StartRound);
                if (PauseButton != null) PauseButton.onClick.AddListener(PauseGame);
                RefreshView();
            }
            catch (ArgumentException error)
            {
                Debug.LogError("GamePlay 配置错误：" + error.Message, this);
                if (StartButton != null) StartButton.interactable = false;
                if (PauseButton != null) PauseButton.interactable = false;
                enabled = false;
            }
        }

        private void Update()
        {
            if (Session == null) return;
            if (Input.GetKeyDown(KeyCode.Space)) PauseGame();
            Session.Tick(Time.deltaTime);
            RefreshView(Session.Paused || !Session.Rounds.Running ? 0 : Time.deltaTime);
        }

        public void StartRound()
        {
            if (!enabled || Session == null || !Session.StartRound()) return;
            RefreshView();
        }

        // Compatibility for previously saved button events; Pause never resumes.
        public void TogglePause() { PauseGame(); }
        public void PauseGame()
        {
            if (!enabled || Session == null) return;
            Session.Pause();
            RefreshView();
        }

        private void RefreshView(float seconds = 0)
        {
            combatView.Refresh(seconds);
            var walker = Session.Walker;
            map.Player.position = Vector3.Lerp(map.RoadPosition(walker.Index, path),
                map.RoadPosition(walker.NextIndex, path), (float)walker.Progress);
            map.UpdatePlayerFacing(walker.Index);
            var rounds = Session.Rounds;
            if (StartButton != null) StartButton.interactable = Session.CanStartOrResume;
            if (PauseButton != null) PauseButton.interactable = rounds.Running && !Session.Paused;
            if (RoundText != null) RoundText.text = "回合 " + rounds.Current + " / " + rounds.Total;
            if (LapText != null) LapText.text = "已完成圈数：" + walker.Laps;
            if (PauseButtonText != null) PauseButtonText.text = Session.Paused ? "已暂停" : "暂停";
            if (HeroHealthText != null) HeroHealthText.text = Session.Hero.Health + "/" + Session.Hero.MaxHealth;
            if (StateText != null) StateText.text = rounds.Failed ? "人物已倒下" : rounds.Finished ? "10个回合已完成" :
                !rounds.Running ? "等待开始回合" : Session.Paused ? "已暂停，点击开始继续" :
                Session.Battle.Target != null ? "战斗中" : "行走中";
        }

        private void OnDestroy()
        {
            if (StartButton != null) StartButton.onClick.RemoveListener(StartRound);
            if (PauseButton != null) PauseButton.onClick.RemoveListener(PauseGame);
            if (combatView != null) combatView.Dispose();
        }
    }
}
