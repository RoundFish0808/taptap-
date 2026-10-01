using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReverseDefense
{
    public sealed class DemoController : MonoBehaviour
    {
        [Tooltip("可指定支持中文的 Font；未指定时使用系统微软雅黑。所有界面文字均为 Unity UI Text。")]
        public Font ChineseFont;
        private static readonly Color Background = Hex(0x10191e), Panel = Hex(0x18262c), Muted = Hex(0x92a6ad);
        private static readonly Color Ink = Hex(0xe5ece8), Gold = Hex(0xe8bd70), Teal = Hex(0x68bbaa), Red = Hex(0xe08075);
        private const float GridX = 165, GridY = 163, CellSize = 48;
        private GameSession game;
        private RectTransform canvasRoot, handRoot, overlay, ghost, actor;
        private readonly List<CellView> cells = new List<CellView>();
        private readonly List<Image> cardImages = new List<Image>();
        private readonly List<Card> displayedCards = new List<Card>();
        private GameSession drawnHandSession;
        private string handSignature;
        private Text status, stats, hpLabel, battle, detail, log, routeLabel, handLabel, notice;
        private Image hpFill, enemyFill;
        private Text pauseLabel, speedLabel;
        private Button commitButton, cancelButton;
        private Card selected;
        private Cell? inspected;
        private Cell? hover;
        private RouteResult preview;
        private int rotation, page, drawnRevision = -1;
        private float speed = 1, originalTimeScale;
        private bool started, dragging, overlayShown, smoke;

        private sealed class CellView
        {
            public Cell Position;
            public Image Base, Center, Highlight;
            public readonly Image[] Arms = new Image[4];
            public Text Symbol, Priority;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string scene = SceneManager.GetActiveScene().name;
            if ((scene == "SampleScene" || scene == "ReverseDefenseDemo") && FindObjectOfType<DemoController>() == null)
                new GameObject("逆塔防演示").AddComponent<DemoController>();
        }

        private void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            originalTimeScale = Time.timeScale;
            if (ChineseFont == null)
                ChineseFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun", "Noto Sans CJK SC" }, 18);
            if (ChineseFont == null) ChineseFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            game = new GameSession();
            BuildUI();
            smoke = Array.IndexOf(Environment.GetCommandLineArgs(), "-demo-smoke") >= 0;
            if (smoke)
            {
                started = true;
                if (EventSystem.current != null)
                    foreach (BaseInputModule input in EventSystem.current.GetComponents<BaseInputModule>()) input.enabled = false;
                StartCoroutine(SmokeCapture());
            }
            else ShowIntro();
        }

        private void Update()
        {
            if (!smoke && Input.GetKeyDown(KeyCode.Space) && started && !overlayShown) game.ManualPause = !game.ManualPause;
            if (!smoke && Input.GetKeyDown(KeyCode.R) && selected != null) { rotation = (rotation + 1) % 4; drawnRevision = -1; }
            if (!smoke && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) && !overlayShown)
            {
                if (selected != null || inspected.HasValue) ClearSelection();
                else game.ManualPause = !game.ManualPause;
            }
            game.InteractionPause = !started || selected != null || inspected.HasValue || overlayShown;
            Time.timeScale = game.IsPaused ? 0 : originalTimeScale;
            game.Tick(Time.unscaledDeltaTime * speed);
            Cell c;
            Cell? newHover = selected != null && ScreenToCell(Input.mousePosition, out c) ? (Cell?)c : null;
            if (!Nullable.Equals(hover, newHover) || drawnRevision != game.Revision)
            {
                hover = newHover;
                preview = selected != null && hover.HasValue ? game.Preview(selected, hover.Value, rotation) : null;
                RefreshMap();
            }
            if (drawnRevision != game.Revision)
            {
                RefreshHand(); drawnRevision = game.Revision;
            }
            RefreshLabels(); MoveActor();
            if (game.VictoryChoice && !overlayShown) { ClearSelection(); ShowOutcome(true); }
            if (game.Failed && !overlayShown) { ClearSelection(); ShowOutcome(false); }
            ghost.gameObject.SetActive(dragging && selected != null);
            if (ghost.gameObject.activeSelf)
            {
                Vector2 p = ScreenToCanvas(Input.mousePosition);
                ghost.anchoredPosition = new Vector2(Mathf.Clamp(p.x + 18, 0, 1280), -Mathf.Clamp(p.y - 40, 0, 775));
            }
        }

        private void OnDestroy() { Time.timeScale = originalTimeScale; }

        private void BuildUI()
        {
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("交互系统", typeof(EventSystem), typeof(StandaloneInputModule));
            GameObject root = new GameObject("中文演示界面", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900); scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            RectTransform viewport = root.GetComponent<RectTransform>();
            RectTransform backdrop = Box(viewport, "全屏背景", 0, 0, 0, 0, Background).rectTransform;
            backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one; backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            canvasRoot = Rect(viewport, "自适应内容", 0, 0, 1440, 900);
            canvasRoot.anchorMin = canvasRoot.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRoot.anchoredPosition = new Vector2(-720, 450);
            Box(canvasRoot, "背景", 0, 0, 1440, 900, Background);
            Box(canvasRoot, "标题装饰", 28, 28, 4, 52, Gold);
            Label(canvasRoot, "标题", "逆塔防 · 路线构筑", 46, 22, 500, 40, 28, Ink);
            Label(canvasRoot, "副标题", "把收益与危险，拼进自己的旅途", 48, 66, 500, 26, 15, Muted);
            status = Label(canvasRoot, "运行状态", "", 655, 31, 320, 30, 19, Gold);
            pauseLabel = MakeButton(canvasRoot, "暂停按钮", "免费暂停", 995, 31, 124, 40, () => game.ManualPause = !game.ManualPause).GetComponentInChildren<Text>();
            speedLabel = MakeButton(canvasRoot, "速度按钮", "速度 1 倍", 1131, 31, 112, 40,
                () => { speed = speed == 1 ? 2 : speed == 2 ? 4 : 1; }).GetComponentInChildren<Text>();
            MakeButton(canvasRoot, "重开按钮", "重新开局", 1255, 31, 157, 40, ShowRestart);
            Box(canvasRoot, "地图面板", 28, 115, 970, 526, Panel);
            Label(canvasRoot, "地图标题", "主循环地图", 49, 126, 250, 27, 19, Ink);
            Label(canvasRoot, "地图图例", "金色：本圈路线    灰色：等待循环道路    青色：整轮施工预览", 330, 129, 640, 25, 14, Muted);
            for (int x = 0; x < game.Board.Width; x++)
                Label(canvasRoot, "横坐标", x.ToString(), GridX + x * CellSize, GridY - 20, CellSize, 18, 12, Muted, TextAnchor.MiddleCenter);
            for (int y = 0; y < game.Board.Height; y++)
            {
                float top = GridY + (game.Board.Height - 1 - y) * CellSize;
                Label(canvasRoot, "纵坐标", y.ToString(), GridX - 24, top, 20, CellSize, 12, Muted, TextAnchor.MiddleCenter);
                for (int x = 0; x < game.Board.Width; x++) MakeCell(new Cell(x, y));
            }
            routeLabel = Label(canvasRoot, "路线反馈", "", 50, 603, 930, 26, 15, Gold);
            actor = Box(canvasRoot, "自动角色", 0, 0, 23, 23, Gold).rectTransform;
            actor.pivot = new Vector2(0.5f, 0.5f);
            actor.localRotation = Quaternion.Euler(0, 0, 45);
            Text heroSymbol = Label(actor, "角色标记", "勇", 0, 0, 23, 23, 13, Background, TextAnchor.MiddleCenter);
            heroSymbol.rectTransform.anchorMin = heroSymbol.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            heroSymbol.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            heroSymbol.rectTransform.anchoredPosition = Vector2.zero;
            heroSymbol.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);
            Box(canvasRoot, "信息面板", 1018, 115, 394, 526, Panel);
            Label(canvasRoot, "状态标题", "远征状态 · 拓路者", 1038, 130, 354, 28, 20, Ink);
            Box(canvasRoot, "生命背景", 1038, 174, 354, 12, Hex(0x283a3f));
            hpFill = Box(canvasRoot, "生命条", 1038, 174, 354, 12, Teal);
            hpLabel = Label(canvasRoot, "生命值", "", 1038, 193, 354, 25, 16, Ink);
            stats = Label(canvasRoot, "属性", "", 1038, 225, 354, 53, 15, Muted);
            Box(canvasRoot, "战斗背景", 1038, 290, 354, 75, Hex(0x223138));
            battle = Label(canvasRoot, "战斗信息", "", 1052, 299, 326, 45, 15, Ink);
            enemyFill = Box(canvasRoot, "敌方生命条", 1052, 350, 326, 5, Red);
            detail = Label(canvasRoot, "地块和卡牌详情", "", 1038, 383, 354, 106, 15, Ink);
            Label(canvasRoot, "日志标题", "旅途记录", 1038, 499, 354, 25, 16, Gold);
            log = Label(canvasRoot, "中文日志", "", 1038, 529, 354, 94, 13, Muted);
            Box(canvasRoot, "手牌面板", 28, 660, 1384, 216, Panel);
            handLabel = Label(canvasRoot, "手牌标题", "", 48, 672, 300, 27, 18, Ink);
            MakeButton(canvasRoot, "旋转卡牌", "旋转选中卡牌", 468, 671, 144, 30, () => { if (selected != null) { rotation = (rotation + 1) % 4; drawnRevision = -1; } });
            cancelButton = MakeButton(canvasRoot, "撤销施工", "撤销整批", 1033, 671, 127, 30, () => { ClearSelection(); game.CancelConstruction(); });
            commitButton = MakeButton(canvasRoot, "完成施工", "完成施工", 1172, 671, 210, 30, () => { ClearSelection(); game.ConfirmConstruction(); }, Gold);
            handRoot = Rect(canvasRoot, "中文卡牌", 49, 717, 1300, 128);
            MakeButton(canvasRoot, "上一页", "‹", 1363, 723, 29, 46, () => { page = Math.Max(0, page - 1); drawnRevision = -1; });
            MakeButton(canvasRoot, "下一页", "›", 1363, 777, 29, 46, () => { page++; drawnRevision = -1; });
            notice = Label(canvasRoot, "操作提示", "拖牌或点击卡牌后点击网格放置 · R 旋转 · 右键取消选择 · 空格免费暂停", 50, 850, 1330, 20, 13, Muted);
            ghost = Box(canvasRoot, "拖拽卡牌", 0, 0, 148, 64, Hex(0x364b4e)).rectTransform;
            Label(ghost, "拖拽提示", "松开放置\nR 键旋转", 8, 7, 132, 48, 16, Ink, TextAnchor.MiddleCenter);
            ghost.gameObject.SetActive(false);
            RefreshMap(); RefreshHand(); RefreshLabels(); MoveActor();
        }

        private void MakeCell(Cell position)
        {
            float x = GridX + position.X * CellSize, y = GridY + (game.Board.Height - 1 - position.Y) * CellSize;
            Image image = Box(canvasRoot, "网格 " + position, x + 2, y + 2, 44, 44, Hex(0x203138));
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            Cell captured = position; button.onClick.AddListener(() => ClickCell(captured));
            var view = new CellView { Position = position, Base = image };
            RectTransform rt = image.rectTransform;
            view.Center = Box(rt, "道路中心", 16, 16, 12, 12, Gold);
            view.Arms[0] = Box(rt, "上连接", 19, 0, 6, 22, Gold);
            view.Arms[1] = Box(rt, "下连接", 19, 22, 6, 22, Gold);
            view.Arms[2] = Box(rt, "左连接", 0, 19, 22, 6, Gold);
            view.Arms[3] = Box(rt, "右连接", 22, 19, 22, 6, Gold);
            view.Symbol = Label(rt, "地块中文", "", 3, 5, 38, 31, 19, Ink, TextAnchor.MiddleCenter);
            view.Priority = Label(rt, "卡牌优先级", "", 2, 1, 40, 15, 10, Muted, TextAnchor.UpperRight);
            view.Highlight = Box(rt, "放置预览", 0, 0, 44, 44, new Color(0, 0, 0, 0));
            cells.Add(view);
        }

        private void RefreshMap()
        {
            RouteResult visible = preview ?? (game.Building ? game.ConstructionRoute : game.Route);
            Board visibleBoard = game.Board;
            string placementReason;
            if (selected != null && hover.HasValue && game.CanPlace(selected, hover.Value, rotation, out placementReason))
            {
                visibleBoard = game.Board.Copy();
                foreach (Cell c in selected.Footprint(hover.Value, rotation)) visibleBoard.Set(c, new Tile(selected));
            }
            var edges = new HashSet<string>();
            if (visible != null && visible.Path.Count > 0)
            {
                for (int i = 1; i < visible.Path.Count; i++) AddEdge(edges, visible.Path[i - 1], visible.Path[i]);
                if (visible.Valid) AddEdge(edges, visible.Path[visible.Path.Count - 1], visible.Path[0]);
            }
            var footprint = new HashSet<Cell>();
            bool canPlace = false; string unused;
            if (selected != null && hover.HasValue)
            { foreach (Cell c in selected.Footprint(hover.Value, rotation)) footprint.Add(c); canPlace = game.CanPlace(selected, hover.Value, rotation, out unused); }
            foreach (CellView view in cells)
            {
                Tile tile = visibleBoard.Get(view.Position);
                bool road = tile != null && tile.Kind == TileKind.Road;
                bool active = visible != null && visible.Cells.Contains(view.Position);
                view.Base.color = tile == null ? Hex(0x203138) : road ? Hex(0x2a3538) : KindColor(tile.Kind) * new Color(0.43f, 0.43f, 0.43f, 1);
                view.Center.gameObject.SetActive(road);
                Color pathColor = visible != null && !visible.Valid ? Red : preview != null || game.Building ? Teal : Gold;
                view.Center.color = active ? pathColor : Hex(0x657078);
                for (int d = 0; d < 4; d++)
                {
                    Cell neighbour = view.Position + RoadRules.Directions[d];
                    view.Arms[d].gameObject.SetActive(road && visibleBoard.IsRoad(neighbour));
                    view.Arms[d].color = edges.Contains(Edge(view.Position, neighbour)) ? pathColor : Hex(0x657078);
                }
                view.Symbol.text = view.Position.Equals(game.Camp) ? "起" : tile == null || road ? "" : KindSymbol(tile.Kind);
                view.Symbol.color = view.Position.Equals(game.Camp) ? Gold : tile == null ? Ink : KindColor(tile.Kind);
                view.Priority.text = tile == null ? "" : "优" + tile.Priority;
                view.Highlight.color = footprint.Contains(view.Position)
                    ? new Color(canPlace ? Teal.r : Red.r, canPlace ? Teal.g : Red.g, canPlace ? Teal.b : Red.b, 0.42f)
                    : inspected.HasValue && inspected.Value.Equals(view.Position) ? new Color(1, 1, 1, 0.17f) : Color.clear;
            }
        }

        private static string Edge(Cell a, Cell b)
        { return a.X < b.X || (a.X == b.X && a.Y < b.Y) ? a + ":" + b : b + ":" + a; }
        private static void AddEdge(HashSet<string> edges, Cell a, Cell b) { edges.Add(Edge(a, b)); }

        private void RefreshHand()
        {
            int maxPage = Math.Max(0, (game.Hand.Count - 1) / 9); page = Mathf.Clamp(page, 0, maxPage);
            string signature = page.ToString();
            foreach (Card card in game.Hand) signature += ":" + card.Id;
            if (drawnHandSession == game && handSignature == signature)
            {
                for (int i = 0; i < cardImages.Count; i++)
                    cardImages[i].color = selected == displayedCards[i] ? Hex(0x3e4e48) : Hex(0x24363d);
                return;
            }
            drawnHandSession = game; handSignature = signature;
            foreach (Transform child in handRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            cardImages.Clear(); displayedCards.Clear();
            for (int index = page * 9; index < Math.Min(game.Hand.Count, page * 9 + 9); index++)
            {
                Card card = game.Hand[index]; float x = (index % 9) * 144;
                Image image = Box(handRoot, card.Name, x, 0, 136, 128, selected == card ? Hex(0x3e4e48) : Hex(0x24363d));
                image.raycastTarget = true;
                CardDragInput input = image.gameObject.AddComponent<CardDragInput>(); input.Owner = this; input.Card = card;
                Color accent = KindColor(card.Kind);
                Box(image.rectTransform, "卡牌色条", 0, 0, 136, 3, accent);
                Label(image.rectTransform, "卡牌名称", card.Name, 8, 9, 120, 23, 16, Ink, TextAnchor.MiddleCenter);
                Cell[] shape = card.Footprint(new Cell(0, 0), selected == card ? rotation : 0);
                int w = 1, h = 1;
                foreach (Cell c in shape) { w = Math.Max(w, c.X + 1); h = Math.Max(h, c.Y + 1); }
                float size = 15, sx = (136 - w * size) / 2, sy = 40 + (44 - h * size) / 2;
                foreach (Cell c in shape) Box(image.rectTransform, "形状方块", sx + c.X * size, sy + (h - 1 - c.Y) * size, size - 2, size - 2, accent);
                Label(image.rectTransform, "卡牌类型", card.Kind == TileKind.Road ? "道路 · " + card.Shape.Length + " 格" : "地块 · " + card.Shape.Length + " 格", 8, 86, 120, 18, 12, Muted, TextAnchor.MiddleCenter);
                Label(image.rectTransform, "固定优先级", "优先级 " + card.Priority + "   等级 " + card.Level, 4, 105, 128, 18, 11, accent, TextAnchor.MiddleCenter);
                cardImages.Add(image); displayedCards.Add(card);
            }
        }

        private void RefreshLabels()
        {
            status.text = game.Failed ? "远征结束" : game.VictoryChoice ? "三位首领已击败" : game.Building ? "施工暂停 · 已放 " + game.PendingCards + " 张" : game.ManualPause ? "手动暂停 · 无消耗" : selected != null || inspected.HasValue ? "查看与规划 · 暂停中" : !started ? "等待开始" : game.Enemy != null ? "自动战斗中" : "行走：" + game.RouteName;
            pauseLabel.text = game.ManualPause ? "恢复行走" : "免费暂停";
            speedLabel.text = "速度 " + speed + " 倍";
            hpFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 354 * game.Health / game.Settings.MaxHealth);
            hpLabel.text = "生命 " + Mathf.CeilToInt(game.Health) + " / " + game.Settings.MaxHealth;
            stats.text = "攻击 " + game.Attack + "    防御 " + game.Settings.Defence + "    金币 " + game.Gold +
                "\n完成 " + game.Laps + " 圈    击败 " + game.Kills + " 敌人    首领 " + Math.Min(3, game.BossesDefeated) + " / 3";
            battle.text = game.Enemy == null ? "未遭遇敌人\n每两圈出现一位首领" : "自动战斗 · " + game.Enemy.Name + "\n生命 " + Math.Max(0, game.Enemy.Health) + " / " + game.Enemy.MaxHealth + "    攻击 " + game.Enemy.Attack;
            enemyFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                game.Enemy == null ? 0 : 326f * Math.Max(0, game.Enemy.Health) / game.Enemy.MaxHealth);
            RouteResult shown = preview ?? (game.Building ? game.ConstructionRoute : game.Route);
            routeLabel.color = shown.Valid ? preview != null || game.Building ? Teal : Gold : Red;
            routeLabel.text = selected != null && !hover.HasValue ? "把卡牌移到地图上，查看占位与新路线。" :
                (preview != null ? "放置预览 · " : game.Building ? "施工预览 · " : "本圈：" + game.RouteName + " · ") +
                (preview != null || game.Building ? shown.Reason : shown.Path.Count + " 格；主道路先走，再依次走拼接闭环。" );
            if (selected != null)
                detail.text = "选中「" + selected.Name + "」\n优先级 " + selected.Priority + " · 等级 " + selected.Level + " · " + selected.Shape.Length + " 格\n" + KindDescription(selected.Kind) + "\nR 旋转 · 右键取消选择";
            else if (inspected.HasValue)
            {
                Tile tile = game.Board.Get(inspected.Value);
                detail.text = "查看网格 " + inspected.Value + "\n" + (tile == null ? "空地，可放置卡牌。" : "来源：「" + tile.Source.Name + "」\n优先级 " + tile.Priority + " · 等级 " + tile.Level + "\n" + KindDescription(tile.Kind));
                detail.text += "\n右键关闭详情并恢复时间";
            }
            else detail.text = "道路循环顺序\n原主道路 → 拼接闭环，共 " + (game.NextSchedule.Routes.Count - 1) + " 条\n拼接闭环：上 → 下 → 左 → 右\n当前：" + game.RouteName;
            log.text = string.Join("\n", game.Messages.GetRange(0, Math.Min(4, game.Messages.Count)).ToArray());
            handLabel.text = "手牌 " + game.Hand.Count + " / " + game.Settings.HandLimit + (page > 0 ? "  第 " + (page + 1) + " 页" : "");
            commitButton.interactable = game.Building; cancelButton.interactable = game.Building;
            commitButton.GetComponentInChildren<Text>().text = game.Building ? "完成施工 · 校验闭环" : "等待放置卡牌";
            if (game.Building) notice.text = "未闭合支路可暂时存在。完成施工时统一校验；失败退回本批全部卡牌，并恢复原地图。";
            else notice.text = "拖牌或点击卡牌后点击网格放置 · R 旋转 · 右键取消选择 / 关闭详情 · 空格免费暂停";
        }

        private void MoveActor()
        {
            Vector2 from = CellCenter(game.Current), to = CellCenter(game.Next);
            Vector2 p = Vector2.Lerp(from, to, game.Enemy == null ? game.MoveProgress : 0);
            actor.anchoredPosition = new Vector2(p.x, -p.y);
        }
        private Vector2 CellCenter(Cell c) { return new Vector2(GridX + (c.X + 0.5f) * CellSize, GridY + (game.Board.Height - c.Y - 0.5f) * CellSize); }

        public void SelectCard(Card card)
        {
            if (!started || overlayShown || !game.Hand.Contains(card)) return;
            if (selected != card) rotation = 0;
            inspected = null; selected = card; hover = null; drawnRevision = -1;
            game.InteractionPause = true;
        }
        public void BeginCardDrag(Card card) { SelectCard(card); dragging = selected == card; }
        public void EndCardDrag(Vector2 screen)
        {
            dragging = false; Cell c;
            if (selected != null && ScreenToCell(screen, out c)) PlaceSelected(c);
        }
        private void ClickCell(Cell c)
        {
            if (!started || overlayShown) return;
            if (selected != null) PlaceSelected(c);
            else { inspected = c; game.InteractionPause = true; RefreshMap(); }
        }
        private void PlaceSelected(Cell c)
        {
            if (game.Place(selected, c, rotation)) ClearSelection();
            else drawnRevision = -1;
        }
        private void ClearSelection()
        {
            selected = null; inspected = null; preview = null; hover = null; dragging = false;
            game.InteractionPause = false; drawnRevision = -1; RefreshMap();
        }
        private Vector2 ScreenToCanvas(Vector2 screen)
        {
            Vector2 local; RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screen, null, out local);
            return new Vector2(local.x - canvasRoot.rect.xMin, canvasRoot.rect.yMax - local.y);
        }
        private bool ScreenToCell(Vector2 screen, out Cell c)
        {
            Vector2 p = ScreenToCanvas(screen);
            c = new Cell(Mathf.FloorToInt((p.x - GridX) / CellSize), game.Board.Height - 1 - Mathf.FloorToInt((p.y - GridY) / CellSize));
            return game.Board.Contains(c);
        }

        private void ShowIntro()
        {
            OpenOverlay("开始探索");
            Label(overlay, "开场标题", "设计一条，考验自己的路线", 375, 219, 690, 54, 30, Ink, TextAnchor.MiddleCenter);
            Label(overlay, "角色选择", "本次出战：拓路者", 390, 291, 660, 34, 22, Gold, TextAnchor.MiddleCenter);
            Label(overlay, "开场说明", "角色先走原主道路，再依次循环拼接闭环。\n多个拼接闭环按「上、下、左、右」排列。\n\n拖放卡牌，或选中后点击网格；R 键旋转。\n接好全部道路后点击「完成施工」，失败则整批退牌。\n\n击败三位首领即获胜；可继续远征，或重新开局。", 390, 347, 660, 228, 19, Muted, TextAnchor.MiddleCenter);
            MakeButton(overlay, "开始按钮", "进入地图", 540, 600, 360, 52, () => { CloseOverlay(); started = true; }, Gold);
        }
        private void ShowOutcome(bool victory)
        {
            OpenOverlay(victory ? "胜利" : "结束");
            Label(overlay, "结算标题", victory ? "三位首领已击败 · 本局胜利" : game.WonOnce ? "远征结束 · 胜利记录保留" : "拓路者倒下 · 本局失败", 360, 275, 720, 62, 30, victory || game.WonOnce ? Gold : Red, TextAnchor.MiddleCenter);
            Label(overlay, "结算数据", "完成 " + game.Laps + " 圈  ·  击败 " + game.Kills + " 敌人  ·  获得 " + game.Gold + " 金币\n\n" + (victory ? "继续当前地图，探索路线能承受的极限。\n也可以带着这次胜利，开始新的一局。" : "调整地块与路线，重新开始探索。"), 390, 370, 660, 130, 20, Muted, TextAnchor.MiddleCenter);
            if (victory) MakeButton(overlay, "继续按钮", "继续本图战斗", 412, 552, 294, 52, () => { game.ContinueAfterVictory(); CloseOverlay(); }, Gold);
            MakeButton(overlay, "重新开始", "重新开始一局", victory ? 734 : 545, 552, victory ? 294 : 350, 52, Restart, victory ? (Color?)null : Gold);
        }
        private void ShowRestart()
        {
            if (overlayShown) return;
            OpenOverlay("重新开局");
            Label(overlay, "重开标题", "重新开始这次远征？", 390, 298, 660, 54, 30, Ink, TextAnchor.MiddleCenter);
            Label(overlay, "重开说明", "本次地图、卡牌和战斗进度将重置。", 390, 383, 660, 44, 20, Muted, TextAnchor.MiddleCenter);
            MakeButton(overlay, "继续当前局", "继续当前局", 412, 508, 294, 52, CloseOverlay);
            MakeButton(overlay, "确认重开", "重新开始", 734, 508, 294, 52, Restart, Gold);
        }
        private void Restart()
        {
            CloseOverlay(); game = new GameSession(); speed = 1; page = 0; started = true; ClearSelection();
        }
        private void OpenOverlay(string kind)
        {
            overlayShown = true;
            overlay = Box(canvasRoot, "结算与说明", 0, 0, 1440, 900, new Color(0.035f, 0.06f, 0.07f, 0.92f)).rectTransform;
            overlay.GetComponent<Image>().raycastTarget = true;
            Box(overlay, "说明面板", 340, 190, 760, 510, Panel);
            Box(overlay, "说明装饰", 340, 190, 760, 4, Gold);
        }
        private void CloseOverlay()
        {
            if (overlay != null) { overlay.gameObject.SetActive(false); Destroy(overlay.gameObject); }
            overlay = null; overlayShown = false;
        }

        private IEnumerator SmokeCapture()
        {
            yield return new WaitForSecondsRealtime(2f);
            string path = Path.Combine(Application.dataPath, "..", "SmokeOutput");
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-demo-output");
            if (index >= 0 && index + 1 < args.Length) path = args[index + 1];
            Directory.CreateDirectory(path);
            ChineseFont.RequestCharactersInTexture("逆塔防路线构筑中文生命优先级首领", 20, FontStyle.Normal);
            bool chinese = ChineseFont.HasCharacter('逆') && ChineseFont.HasCharacter('路') && ChineseFont.HasCharacter('中');
            Text[] textComponents = GetComponentsInChildren<Text>(true);
            File.WriteAllText(Path.Combine(path, "runtime-check.txt"), "RouteValid=" + game.Route.Valid + "\nChineseFont=" + ChineseFont.name +
                "\nChineseGlyphs=" + chinese + "\nTextComponents=" + textComponents.Length + "\nHealth=" + game.Health +
                "\nCurrent=" + game.Current + "\nScreen=" + Screen.width + "x" + Screen.height);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(path, "demo.png"));
            yield return new WaitForSecondsRealtime(0.25f);
            // Exercise real uGUI handlers across frames, including the lifetime of the drag source.
            game = new GameSession(); game.ManualPause = true; ClearSelection();
            yield return null;
            int originalHandCount = game.Hand.Count;
            CardDragInput dragSource = cardImages[0].GetComponent<CardDragInput>();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            dragSource.OnBeginDrag(pointer);
            yield return null;
            bool dragSourceAlive = dragSource != null && dragging && selected == game.Hand[0];
            pointer.position = CellScreen(new Cell(4, 7)); dragSource.OnEndDrag(pointer);
            yield return null;
            bool incompleteFrozen = game.Building && game.IsPaused && !game.ConstructionRoute.Valid && game.Hand.Count == originalHandCount - 1;
            dragSource = cardImages[0].GetComponent<CardDragInput>();
            dragSource.OnBeginDrag(pointer); yield return null;
            pointer.position = CellScreen(new Cell(6, 7)); dragSource.OnEndDrag(pointer);
            yield return null;
            bool previewComplete = game.Building && game.ConstructionRoute.Valid;
            commitButton.onClick.Invoke(); game.ManualPause = true;
            yield return null;
            bool committed = !game.Building && game.Route.Valid && game.Route.Path.Count == 16 && game.Schedule.Routes.Count == 2 &&
                game.Schedule.Routes[1].Path.Count == 20 && game.Hand.Count == originalHandCount - 2;
            File.AppendAllText(Path.Combine(path, "runtime-check.txt"), "\nDragSourceSurvived=" + dragSourceAlive +
                "\nIncompleteBuildPaused=" + incompleteFrozen + "\nTwoCardPreviewValid=" + previewComplete + "\nUICommitSucceeded=" + committed);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(path, "construction.png"));
            yield return new WaitForSecondsRealtime(0.25f);
            // Inspect the actual batch rollback button path as well.
            Card corner = game.Hand[0]; BeginCardDrag(corner); EndCardDrag(CellScreen(new Cell(0, 0)));
            yield return null;
            commitButton.onClick.Invoke(); yield return null;
            bool rollback = !game.Building && game.Hand.Contains(corner) && game.Board.Get(new Cell(0, 0)) == null && game.Route.Valid;
            File.AppendAllText(Path.Combine(path, "runtime-check.txt"), "\nUIRollbackSucceeded=" + rollback);
            game.ManualPause = false; speed = 4;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (game.Laps < 1 && Time.realtimeSinceStartup < deadline) yield return null;
            game.ManualPause = true;
            bool mainThenAdded = game.Laps == 1 && game.RouteIndex == 1 && game.Route.Path.Count == 20;
            File.AppendAllText(Path.Combine(path, "runtime-check.txt"), "\nMainThenAddedLoop=" + mainThenAdded);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(path, "added-loop.png"));
            if (!(chinese && dragSourceAlive && incompleteFrozen && previewComplete && committed && rollback && mainThenAdded))
                Debug.LogError("Demo runtime verification failed; inspect runtime-check.txt.");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        private Vector2 CellScreen(Cell c)
        {
            Vector2 p = CellCenter(c);
            return RectTransformUtility.WorldToScreenPoint(null, canvasRoot.TransformPoint(new Vector3(p.x, -p.y, 0)));
        }

        private RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); return rt;
        }
        private Image Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            Image image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        private Text Label(Transform parent, string name, string content, float x, float y, float w, float h, int size, Color color, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            Text text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = ChineseFont; text.text = content; text.fontSize = size; text.color = color; text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false; text.raycastTarget = false; text.lineSpacing = 1.15f; return text;
        }
        private Button MakeButton(Transform parent, string name, string content, float x, float y, float w, float h, UnityEngine.Events.UnityAction action, Color? accent = null)
        {
            Image image = Box(parent, name, x, y, w, h, accent ?? Hex(0x2c4147)); image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            ColorBlock colors = button.colors; colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f); colors.disabledColor = new Color(0.6f, 0.6f, 0.6f); button.colors = colors;
            Label(image.rectTransform, "按钮中文", content, 4, 0, w - 8, h, h <= 32 ? 13 : 16, accent.HasValue ? Background : Ink, TextAnchor.MiddleCenter); return button;
        }
        private static Color Hex(int value) { return new Color(((value >> 16) & 255) / 255f, ((value >> 8) & 255) / 255f, (value & 255) / 255f, 1); }
        private static Color KindColor(TileKind kind) { return kind == TileKind.Road ? Gold : kind == TileKind.Resource ? Hex(0x8ec8c1) : kind == TileKind.Healing ? Hex(0x8ebc81) : kind == TileKind.Monster ? Hex(0xd48b7b) : Hex(0xba9ed0); }
        private static string KindSymbol(TileKind kind) { return kind == TileKind.Resource ? "矿" : kind == TileKind.Healing ? "营" : kind == TileKind.Monster ? "怪" : "险"; }
        private static string KindDescription(TileKind kind)
        {
            if (kind == TileKind.Road) return "相邻道路自动连接；同一卡各格优先级相同。";
            if (kind == TileKind.Resource) return "紧邻路线，每圈触发一次，获得 8 金币。";
            if (kind == TileKind.Healing) return "紧邻路线，每圈触发一次，恢复 12 生命。";
            if (kind == TileKind.Monster) return "紧邻路线，每圈出现怪物；战斗可掉落卡牌。";
            return "更强守卫、更高收益；击败获得 20 金币。";
        }
    }

    public sealed class CardDragInput : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public DemoController Owner;
        public Card Card;
        public void OnPointerClick(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Owner.SelectCard(Card); }
        public void OnBeginDrag(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Owner.BeginCardDrag(Card); }
        public void OnDrag(PointerEventData data) { }
        public void OnEndDrag(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Owner.EndCardDrag(data.position); }
    }
}
