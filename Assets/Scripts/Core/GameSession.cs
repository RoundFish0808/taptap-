using System;
using System.Collections.Generic;

namespace ReverseDefense
{
    public sealed class DemoSettings
    {
        public float StepSeconds = 0.42f;
        public float AttackSeconds = 0.42f;
        public int MaxHealth = 140;
        public int Attack = 14;
        public int Defence = 2;
        public int CampHealing = 20;
        public int BossEveryLaps = 2;
        public float MovementDamage = 0.22f;
        public double DropChance = 0.8;
        public int HandLimit = 14;
    }

    public sealed class Encounter
    {
        public string Name;
        public int Health;
        public int MaxHealth;
        public int Attack;
        public bool Boss;
        public bool Risk;
    }

    public sealed class GameSession
    {
        public readonly DemoSettings Settings;
        public Board Board { get; private set; }
        public readonly List<Card> Hand = new List<Card>();
        public readonly List<string> Messages = new List<string>();
        public Cell Camp { get; private set; }
        public Cell Previous { get; private set; }
        public Cell Current { get; private set; }
        public Cell Next { get; private set; }
        public float MoveProgress { get; private set; }
        public float Health { get; private set; }
        public int Attack { get; private set; }
        public int Gold { get; private set; }
        public int Laps { get; private set; }
        public int Kills { get; private set; }
        public int BossesDefeated { get; private set; }
        public bool WonOnce { get; private set; }
        public bool VictoryChoice { get; private set; }
        public bool Failed { get; private set; }
        public bool ManualPause;
        public bool InteractionPause;
        public bool Building { get; private set; }
        public int PendingCards { get; private set; }
        public Encounter Enemy { get; private set; }
        public RouteResult Route { get; private set; }
        public RoutePlan Schedule { get; private set; }
        public RoutePlan NextSchedule { get { return deferredPlan ?? Schedule; } }
        public RouteResult ConstructionRoute { get { return constructionPlan == null ? Route : constructionPlan.Preview; } }
        public int RouteIndex { get; private set; }
        public string RouteName { get { return Route.Name; } }
        public int Revision { get; private set; }
        public bool IsPaused { get { return ManualPause || InteractionPause || Building || VictoryChoice || Failed; } }
        private Board savedBoard;
        private List<Card> savedHand;
        private readonly Random random;
        private readonly HashSet<int> triggered = new HashSet<int>();
        private readonly Queue<Encounter> encounterQueue = new Queue<Encounter>();
        private float combatClock;
        private int nextCardId = 1;
        private readonly List<Cell> mainPath = new List<Cell>();
        private RoutePlan constructionPlan, deferredPlan;
        private int routeStep;

        public GameSession(DemoSettings settings = null, int seed = 17)
        {
            Settings = settings ?? new DemoSettings(); random = new Random(seed);
            Board = new Board(14, 9); Camp = new Cell(4, 2);
            Current = Camp; Previous = new Cell(5, 2);
            Health = Settings.MaxHealth; Attack = Settings.Attack;
            Card basic = NewCard("初始道路", TileKind.Road, 1, 1, new Cell(0, 0));
            for (int x = 4; x <= 8; x++) { Board.Set(new Cell(x, 2), new Tile(basic)); Board.Set(new Cell(x, 6), new Tile(basic)); }
            for (int y = 3; y <= 5; y++) { Board.Set(new Cell(4, y), new Tile(basic)); Board.Set(new Cell(8, y), new Tile(basic)); }
            Board.Set(new Cell(3, 4), new Tile(NewCard("初始怪物营地", TileKind.Monster, 1, 1, new Cell(0, 0))));
            Board.Set(new Cell(9, 4), new Tile(NewCard("初始资源地块", TileKind.Resource, 1, 1, new Cell(0, 0))));
            mainPath.Add(Camp);
            for (int y = 3; y <= 6; y++) mainPath.Add(new Cell(4, y));
            for (int x = 5; x <= 8; x++) mainPath.Add(new Cell(x, 6));
            for (int y = 5; y >= 2; y--) mainPath.Add(new Cell(8, y));
            for (int x = 7; x >= 5; x--) mainPath.Add(new Cell(x, 2));
            AddStarterHand(); Schedule = RoadRules.BuildPlan(Board, mainPath); Route = Schedule.Routes[0]; UpdateNext();
            Log("探索开始：角色会沿金色主循环自动行走。");
            Log("试把三格转角放在 (4,7)，再把四格转角放在 (6,7)，接成上方绕路。");
        }

        private Card NewCard(string name, TileKind kind, int priority, int level, params Cell[] shape)
        { return new Card(nextCardId++, name, kind, priority, level, shape); }

        private void AddStarterHand()
        {
            Hand.Add(NewCard("三格转角", TileKind.Road, 2, 1, new Cell(0, 0), new Cell(0, 1), new Cell(1, 1)));
            Hand.Add(NewCard("四格转角", TileKind.Road, 2, 1, new Cell(0, 1), new Cell(1, 1), new Cell(2, 1), new Cell(2, 0)));
            Hand.Add(NewCard("三格直路", TileKind.Road, 2, 1, new Cell(0, 0), new Cell(1, 0), new Cell(2, 0)));
            Hand.Add(NewCard("四格岔路", TileKind.Road, 2, 2, new Cell(0, 0), new Cell(1, 0), new Cell(2, 0), new Cell(1, 1)));
            Hand.Add(NewCard("优先道路", TileKind.Road, 3, 1, new Cell(0, 0)));
            Hand.Add(NewCard("双格矿场", TileKind.Resource, 2, 1, new Cell(0, 0), new Cell(1, 0)));
            Hand.Add(NewCard("恢复营地", TileKind.Healing, 2, 1, new Cell(0, 0)));
            Hand.Add(NewCard("怪物营地", TileKind.Monster, 2, 1, new Cell(0, 0)));
            Hand.Add(NewCard("风险矿场", TileKind.Risk, 3, 1, new Cell(0, 0)));
        }

        public bool CanPlace(Card card, Cell origin, int rotation, out string reason)
        {
            if (Failed || VictoryChoice) { reason = "请先处理结算。"; return false; }
            if (card == null || !Hand.Contains(card)) { reason = "这张卡不在手牌中。"; return false; }
            foreach (Cell c in card.Footprint(origin, rotation))
            {
                if (!Board.Contains(c)) { reason = "卡牌超出地图边界。"; return false; }
                Tile existing = Board.Get(c);
                if (card.Kind == TileKind.Road && (c.Equals(Current) || c.Equals(Next) || c.Equals(Camp)))
                { reason = "不能替换起点、角色所在格或下一步道路。"; return false; }
                if (existing != null && (existing.Kind == TileKind.Road) != (card.Kind == TileKind.Road))
                { reason = "道路与功能地块不能互相覆盖。"; return false; }
            }
            reason = "位置可放置；最终路线在施工完成时统一验证。"; return true;
        }

        public RouteResult Preview(Card card, Cell origin, int rotation)
        {
            string reason;
            if (!CanPlace(card, origin, rotation, out reason)) return new RouteResult { Reason = reason };
            Board candidate = Board.Copy();
            foreach (Cell c in card.Footprint(origin, rotation)) candidate.Set(c, new Tile(card));
            return RoadRules.BuildPlan(candidate, mainPath).Preview;
        }

        public bool Place(Card card, Cell origin, int rotation)
        {
            string reason;
            if (!CanPlace(card, origin, rotation, out reason)) { Log(reason); return false; }
            if (!Building)
            {
                savedBoard = Board.Copy(); savedHand = new List<Card>(Hand); Building = true;
            }
            foreach (Cell c in card.Footprint(origin, rotation)) Board.Set(c, new Tile(card));
            Hand.Remove(card); PendingCards++; constructionPlan = RoadRules.BuildPlan(Board, mainPath); Revision++;
            Log("暂放「" + card.Name + "」；施工暂停中，可以继续接路。");
            return true;
        }

        public bool ConfirmConstruction()
        {
            if (!Building) return true;
            RoutePlan checkedPlan = RoadRules.BuildPlan(Board, mainPath);
            if (!checkedPlan.Valid)
            {
                string why = checkedPlan.Reason; int count = PendingCards;
                RestoreConstruction(); Log("提交失败：" + why + " 本批 " + count + " 张卡已全部退回。");
                return false;
            }
            int used = PendingCards; Building = false; PendingCards = 0; savedBoard = null; savedHand = null;
            constructionPlan = null;
            if (RouteIndex == 0)
            {
                Schedule = checkedPlan; deferredPlan = null; Route = Schedule.Routes[0];
            }
            else deferredPlan = checkedPlan;
            UpdateNext(); Revision++;
            Log("施工完成：消耗 " + used + " 张新卡；原主道路先走，拼接闭环 " + (checkedPlan.Routes.Count - 1) + " 条。");
            return true;
        }

        private void RestoreConstruction()
        {
            Board = savedBoard; Hand.Clear(); Hand.AddRange(savedHand);
            savedBoard = null; savedHand = null; Building = false; PendingCards = 0;
            constructionPlan = null; UpdateNext(); Revision++;
        }

        public void CancelConstruction()
        {
            if (!Building) return;
            RestoreConstruction(); Log("已撤销整批施工，卡牌与原地图已恢复。");
        }

        public void ContinueAfterVictory()
        {
            if (!VictoryChoice) return;
            VictoryChoice = false; Log("胜利记录已保留。继续本次地图，直到角色倒下。"); Revision++;
        }

        public void Tick(float seconds)
        {
            if (IsPaused || seconds <= 0) return;
            // Consume elapsed time in small slices; speed changes never skip terrain or combat triggers.
            float remaining = Math.Min(seconds, 2f);
            while (remaining > 0.00001f && !IsPaused)
            {
                if (Enemy != null)
                {
                    float portion = Math.Min(remaining, Math.Max(0.00001f, Settings.AttackSeconds - combatClock));
                    combatClock += portion; remaining -= portion;
                    if (combatClock + 0.00001f >= Settings.AttackSeconds) { combatClock = 0; CombatRound(); }
                }
                else
                {
                    float portion = Math.Min(remaining, Math.Max(0.00001f, (1f - MoveProgress) * Settings.StepSeconds));
                    MoveProgress += portion / Settings.StepSeconds; remaining -= portion;
                    if (MoveProgress >= 0.99999f)
                    {
                        Previous = Current; Current = Next; MoveProgress = 0;
                        routeStep = (routeStep + 1) % Route.Path.Count;
                        Health -= Settings.MovementDamage;
                        if (CheckDeath()) break;
                        OnArrival(routeStep == 0); UpdateNext();
                    }
                }
            }
        }

        private void OnArrival(bool finishedRoute)
        {
            if (finishedRoute)
            {
                Laps++; triggered.Clear(); Health = Math.Min(Settings.MaxHealth, Health + Settings.CampHealing);
                Log("完成第 " + Laps + " 圈「" + Route.Name + "」，起点恢复 " + Settings.CampHealing + " 点生命。");
                if (Laps % Settings.BossEveryLaps == 0)
                {
                    int index = BossesDefeated + 1;
                    encounterQueue.Enqueue(new Encounter { Name = "第 " + index + " 位首领", Boss = true,
                        Health = 45 + index * 24, MaxHealth = 45 + index * 24, Attack = 7 + index * 3 });
                }
                if (deferredPlan != null) { Schedule = deferredPlan; deferredPlan = null; RouteIndex = 0; }
                else RouteIndex = (RouteIndex + 1) % Schedule.Routes.Count;
                Route = Schedule.Routes[RouteIndex];
                Log("下一圈：「" + Route.Name + "」。"); Revision++;
            }
            var effects = new List<KeyValuePair<Cell, Tile>>();
            var found = new HashSet<int>();
            foreach (var pair in Board.Tiles)
            {
                if (pair.Value.Kind == TileKind.Road || triggered.Contains(pair.Value.Source.Id)) continue;
                int distance = Math.Abs(pair.Key.X - Current.X) + Math.Abs(pair.Key.Y - Current.Y);
                if (distance == 1 && found.Add(pair.Value.Source.Id)) effects.Add(pair);
            }
            effects.Sort((a, b) => { int p = b.Value.Priority.CompareTo(a.Value.Priority);
                if (p != 0) return p; int y = b.Key.Y.CompareTo(a.Key.Y); return y != 0 ? y : a.Key.X.CompareTo(b.Key.X); });
            foreach (var pair in effects)
            {
                Tile tile = pair.Value; triggered.Add(tile.Source.Id);
                if (tile.Kind == TileKind.Resource) { Gold += 8; Log("矿场产出：获得 8 金币。"); }
                if (tile.Kind == TileKind.Healing) { Health = Math.Min(Settings.MaxHealth, Health + 12); Log("恢复营地：恢复 12 点生命。"); }
                if (tile.Kind == TileKind.Monster || tile.Kind == TileKind.Risk)
                {
                    bool risk = tile.Kind == TileKind.Risk;
                    int hp = 20 + Laps * 7 + (risk ? 28 : 0);
                    encounterQueue.Enqueue(new Encounter { Name = risk ? "矿场守卫" : "成长怪物", Health = hp,
                        MaxHealth = hp, Attack = 4 + Laps * 2 + (risk ? 4 : 0), Risk = risk });
                }
            }
            StartNextEncounter();
        }

        private void StartNextEncounter()
        {
            if (Enemy != null || encounterQueue.Count == 0 || VictoryChoice || Failed) return;
            Enemy = encounterQueue.Dequeue(); combatClock = 0; Log("遭遇「" + Enemy.Name + "」，自动战斗开始。"); Revision++;
        }

        private void CombatRound()
        {
            Enemy.Health -= Attack;
            if (Enemy.Health > 0)
            {
                Health -= Math.Max(1, Enemy.Attack - Settings.Defence); CheckDeath(); return;
            }
            Encounter defeated = Enemy; Enemy = null; Kills++; Gold += defeated.Boss ? 35 : defeated.Risk ? 20 : 6;
            Log("击败「" + defeated.Name + "」。");
            if (defeated.Boss)
            {
                BossesDefeated++; Attack += 4; Health = Math.Min(Settings.MaxHealth, Health + 30);
                if (BossesDefeated >= 3 && !WonOnce)
                { WonOnce = true; VictoryChoice = true; InteractionPause = false; Log("三位首领已击败，本局胜利！"); }
            }
            if (random.NextDouble() < Settings.DropChance) DropCard();
            Revision++; StartNextEncounter();
        }

        private void DropCard()
        {
            if (Hand.Count >= Settings.HandLimit) { Gold += 5; Log("手牌已满，掉落转为 5 金币。"); return; }
            int type = random.Next(6), priority = random.NextDouble() < 0.18 ? 3 : 2;
            Card card;
            if (type == 0) card = NewCard("两格直路", TileKind.Road, priority, 1, new Cell(0, 0), new Cell(1, 0));
            else if (type == 1) card = NewCard("三格转角", TileKind.Road, priority, 1, new Cell(0, 0), new Cell(0, 1), new Cell(1, 1));
            else if (type == 2) card = NewCard("优先道路", TileKind.Road, priority, 2, new Cell(0, 0));
            else card = NewCard(type == 3 ? "双格矿场" : type == 4 ? "恢复营地" : "怪物营地",
                type == 3 ? TileKind.Resource : type == 4 ? TileKind.Healing : TileKind.Monster, priority, 1,
                type == 3 ? new[] { new Cell(0, 0), new Cell(1, 0) } : new[] { new Cell(0, 0) });
            Hand.Add(card); Log("战斗掉落：「" + card.Name + "」，优先级 " + card.Priority + "。");
        }

        private bool CheckDeath()
        {
            if (Health > 0) return false;
            Health = 0; Failed = true; Log(WonOnce ? "远征结束：角色倒下，本局胜利记录已保留。" : "角色倒下，本局失败。"); Revision++; return true;
        }
        private void UpdateNext()
        {
            Cell next = Route.Path[(routeStep + 1) % Route.Path.Count];
            if (Board.IsRoad(next) && Math.Abs(next.X - Current.X) + Math.Abs(next.Y - Current.Y) == 1) Next = next;
            else { Next = Current; ManualPause = true; Log("计划道路中断，已安全暂停。"); }
        }
        public void Log(string message)
        {
            Messages.Insert(0, message); if (Messages.Count > 30) Messages.RemoveAt(Messages.Count - 1); Revision++;
        }
    }
}
