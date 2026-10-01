using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ReverseDefense;

internal static class Program
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        passed++; Console.WriteLine("PASS: " + name);
    }
    private static void Main()
    {
        try { Run(); }
        catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    }
    private static void Run()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Packages", "manifest.json")))
        {
            DirectoryInfo parent = Directory.GetParent(root);
            if (parent == null) throw new Exception("Cannot locate Unity manifest.");
            root = parent.FullName;
        }
        using (JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Packages", "manifest.json"))))
            Check(UniqueJsonKeys(manifest.RootElement), "Unity manifest is valid JSON with no duplicated package keys");
        var g = new GameSession();
        Check(g.Route.Valid && g.Route.Path.Count == 16, "Initial stable loop returns to camp");
        float health = g.Health; Cell current = g.Current;
        g.ManualPause = true; g.Tick(15);
        Check(g.Health == health && g.Current.Equals(current), "Manual pause costs nothing and freezes simulation");
        g.ManualPause = false; g.InteractionPause = true; g.Tick(1);
        Check(g.Health == health && g.MoveProgress == 0, "Inspection pause freezes movement");
        g.InteractionPause = false;
        Card first = g.Hand[0], second = g.Hand[1]; int handCount = g.Hand.Count;
        Check(g.Place(first, new Cell(4, 7), 0), "Place first incomplete construction card");
        Check(g.Building && !g.ConstructionRoute.Valid && g.Hand.Count == handCount - 1, "Incomplete routes may remain while building");
        g.Tick(10); Check(g.Current.Equals(current) && g.Health == health, "Building freezes movement and damage");
        Check(g.Place(second, new Cell(6, 7), 0) && g.ConstructionRoute.Valid, "Second card completes usable longer loop");
        Check(g.ConfirmConstruction() && !g.Building && g.Route.Path.Count == 16 && g.Schedule.Routes[1].Path.Count == 20,
            "Commit keeps original main circuit and schedules added circuit");
        Check(g.Hand.Count == handCount - 2, "Successful commit consumes both new cards");

        var visitedMain = WalkPhase(g);
        Check(SamePath(visitedMain, g.Schedule.Routes[0].Path), "First actual lap walks the original main circuit without diverting");
        Check(g.Laps == 1 && g.RouteIndex == 1 && g.Current.Equals(g.Camp), "First camp return switches to added circuit");
        var visitedAdded = WalkPhase(g);
        Check(SamePath(visitedAdded, g.Schedule.Routes[1].Path) && visitedAdded.Contains(new Cell(4, 8)),
            "Second actual lap walks every step of the constructed circuit");
        Check(g.Laps == 2 && g.RouteIndex == 0, "Completed queue returns to original main circuit");

        for (int actorIndex = 0; actorIndex < 16; actorIndex++)
        {
            var moving = new GameSession(new DemoSettings { MaxHealth = 10000 });
            Cell target = moving.Route.Path[actorIndex];
            for (int i = 0; i < 200 && !moving.Current.Equals(target); i++) moving.Tick(0.1f);
            moving.Place(moving.Hand[0], new Cell(4, 7), 0);
            moving.Place(moving.Hand[0], new Cell(6, 7), 0);
            Cell oldNext = moving.Next;
            Check(moving.ConfirmConstruction() && moving.Route.Path.Count == 16 && moving.Next.Equals(oldNext) &&
                moving.Schedule.Routes.Count == 2 && moving.Schedule.Routes[1].Cells.Contains(moving.Camp),
                "Mid-route construction preserves main lap and schedules added circuit at actor position " + actorIndex);
        }

        var durable = new DemoSettings { MaxHealth = 10000, MovementDamage = 0, CampHealing = 100 };
        g = new GameSession(durable);
        AddTop(g);
        AddCustom(g, "下方闭环", new Cell(4, 0), new Cell(0, 1), new Cell(0, 0), new Cell(1, 0),
            new Cell(2, 0), new Cell(3, 0), new Cell(4, 0), new Cell(4, 1));
        AddCustom(g, "左方闭环", new Cell(2, 5), new Cell(0, 0), new Cell(1, 0), new Cell(0, 1), new Cell(1, 1));
        AddCustom(g, "右方闭环", new Cell(9, 5), new Cell(0, 0), new Cell(1, 0), new Cell(0, 1), new Cell(1, 1));
        Check(g.ConfirmConstruction() && g.Schedule.Routes.Count >= 5, "All completed added circuits are scheduled");
        var ranks = new HashSet<int>(); int lastRank = -1; bool sorted = true;
        for (int i = 1; i < g.Schedule.Routes.Count; i++)
        {
            int rank = g.Schedule.Routes[i].DirectionRank; sorted &= rank >= lastRank; lastRank = rank; ranks.Add(rank);
        }
        Check(sorted && ranks.Count == 4, "Added circuits follow up, down, left, right independently of placement order");
        int phases = g.Schedule.Routes.Count;
        for (int i = 0; i < phases; i++)
        {
            Check(g.RouteIndex == i && SamePath(WalkPhase(g), g.Schedule.Routes[i].Path),
                "Actual walking follows scheduled phase " + i);
        }
        Check(g.RouteIndex == 0, "Every completed added circuit is visited before returning to main");

        g = new GameSession(durable); AddTop(g); g.ConfirmConstruction(); WalkPhase(g); g.Tick(0.1f);
        RouteResult active = g.Route; Cell activeNext = g.Next; float progressForDeferred = g.MoveProgress;
        AddCustom(g, "下方闭环", new Cell(4, 0), new Cell(0, 1), new Cell(0, 0), new Cell(1, 0),
            new Cell(2, 0), new Cell(3, 0), new Cell(4, 0), new Cell(4, 1));
        Check(g.ConfirmConstruction() && g.Route == active && g.Next.Equals(activeNext) && g.MoveProgress == progressForDeferred,
            "Construction during an added lap preserves its in-flight route");
        WalkPhase(g);
        Check(g.RouteIndex == 0 && g.Schedule.Routes.Count >= 3, "Deferred construction applies at camp and starts with main again");

        g = new GameSession(durable);
        AddCustom(g, "起点旁闭环", new Cell(3, 1), new Cell(0, 0), new Cell(1, 0), new Cell(0, 1));
        Check(g.ConfirmConstruction() && g.Schedule.Routes.Count == 2, "Single-junction closed circuit is supported");
        WalkPhase(g);
        int beforeLap = g.Laps;
        for (int i = 0; i < 4; i++) MoveOneCell(g);
        Check(g.Current.Equals(g.Camp) && g.Laps == beforeLap && g.RouteIndex == 1,
            "Passing camp inside a local circuit does not count an extra lap");
        WalkPhase(g);
        Check(g.Laps == beforeLap + 1 && g.RouteIndex == 0, "Local circuit completes and advances exactly once");

        g = new GameSession(); first = g.Hand[0]; second = g.Hand[1]; handCount = g.Hand.Count;
        g.Place(first, new Cell(4, 7), 0); g.Place(second, new Cell(0, 0), 0);
        Check(!g.ConfirmConstruction(), "Invalid or disconnected batch is rejected");
        Check(!g.Building && g.Hand.Count == handCount && g.Hand[0] == first && g.Hand[1] == second,
            "Every pending card returns in its original hand order");
        Check(g.Board.Get(new Cell(4, 7)) == null && g.Board.Get(new Cell(0, 0)) == null && g.Route.Valid,
            "Rollback restores complete original map and route");

        g = new GameSession(); Card replacement = g.Hand[4]; Cell replacementCell = new Cell(8, 4);
        Tile oldTile = g.Board.Get(replacementCell);
        g.Place(replacement, replacementCell, 0);
        Check(g.Board.Get(replacementCell).Source == replacement, "Replacement changes card-bound priority");
        g.Place(g.Hand[0], new Cell(0, 0), 0); g.ConfirmConstruction();
        Check(g.Board.Get(replacementCell) == oldTile && g.Hand.Contains(replacement),
            "Invalid batch restores overwritten road metadata and replacement card");
        g.Place(replacement, replacementCell, 0);
        Check(g.ConfirmConstruction() && !g.Hand.Contains(replacement) && g.Board.Get(replacementCell).Source == replacement,
            "Valid replacement consumes new card without refunding original card");

        g = new GameSession();
        for (int i = 0; i < 40 && g.Enemy == null; i++) g.Tick(0.1f);
        Check(g.Enemy != null, "Entering monster adjacency starts combat");
        int enemyHealth = g.Enemy.Health; health = g.Health;
        g.Place(g.Hand[0], new Cell(4, 7), 0); g.Tick(10);
        Check(g.Enemy.Health == enemyHealth && g.Health == health, "Building during combat freezes both combatants");
        g.CancelConstruction(); g.Tick(0.5f);
        Check(g.Enemy == null || g.Enemy.Health < enemyHealth, "Combat resumes after construction rollback");

        g = new GameSession(); g.ManualPause = true; g.Place(g.Hand[0], new Cell(4, 7), 0); g.ConfirmConstruction();
        Check(g.ManualPause && g.IsPaused, "Construction rollback preserves prior manual pause");
        g = new GameSession(); g.Tick(0.1f); float progress = g.MoveProgress;
        g.Place(g.Hand[0], new Cell(4, 7), 0); g.CancelConstruction();
        Check(Math.Abs(g.MoveProgress - progress) < 0.0001f && g.Route.Valid && g.Hand.Count == 9,
            "Cancellation preserves in-flight movement and restores cards");
        string reason;
        Check(!g.CanPlace(g.Hand[0], new Cell(13, 8), 0, out reason), "Multi-cell out-of-bounds placement rejected");
        Check(!g.CanPlace(g.Hand[4], g.Camp, 0, out reason), "Camp cannot be replaced");
        Check(!g.CanPlace(g.Hand[4], g.Next, 0, out reason), "In-flight destination cannot be replaced");
        Check(!g.CanPlace(g.Hand[5], new Cell(8, 4), 0, out reason), "Terrain cannot overwrite road layer");
        g = new GameSession(); Card resource = g.Hand[5];
        g.Place(resource, new Cell(0, 0), 0);
        Check(!g.ConfirmConstruction() && g.Hand.Contains(resource), "Detached terrain returns to hand");
        g.Place(resource, new Cell(5, 1), 0);
        Check(g.ConfirmConstruction(), "Multi-cell terrain valid when touching road");
        for (int i = 0; i < 160; i++) g.Tick(0.1f);
        Check(g.Gold > 0 && g.Kills > 0, "Automatic movement triggers terrain, combat and rewards");
        bool hasDrop = false; foreach (Card c in g.Hand) if (c.Id > 12) hasDrop = true;
        Check(hasDrop, "Combat drops new cards into hand");

        // Direction now takes precedence over road-card metadata, as requested.
        var board = new Board(5, 5); Cell centre = new Cell(2, 2), previous = new Cell(2, 1), next;
        SetRoad(board, centre, 1, 1); SetRoad(board, previous, 1, 1);
        SetRoad(board, new Cell(2, 3), 1, 3); SetRoad(board, new Cell(3, 2), 2, 1);
        RoadRules.TryNext(board, previous, centre, out next);
        Check(next.Equals(new Cell(2, 3)), "Up is chosen before a higher-priority right exit");
        SetRoad(board, new Cell(2, 3), 2, 3); RoadRules.TryNext(board, previous, centre, out next);
        Check(next.Equals(new Cell(2, 3)), "Road level does not override directional order");
        SetRoad(board, new Cell(2, 3), 2, 1); RoadRules.TryNext(board, previous, centre, out next);
        Check(next.Equals(new Cell(2, 3)), "Direction order remains deterministic with equal metadata");
        previous = new Cell(1, 2); SetRoad(board, previous, 1, 1); SetRoad(board, new Cell(3, 2), 0, 1);
        SetRoad(board, new Cell(2, 1), 2, 1); RoadRules.TryNext(board, previous, centre, out next);
        Check(next.Equals(new Cell(2, 3)), "Up wins over down when inertia unavailable");
        board = new Board(5, 5); previous = new Cell(2, 1);
        SetRoad(board, centre, 1, 1); SetRoad(board, previous, 1, 1);
        SetRoad(board, new Cell(1, 2), 1, 1); SetRoad(board, new Cell(3, 2), 1, 1);
        RoadRules.TryNext(board, previous, centre, out next);
        Check(next.Equals(new Cell(1, 2)), "Left wins over right when inertia unavailable");
        var shapeCard = new Card(99, "shape", TileKind.Road, 3, 2, new Cell(0, 0), new Cell(1, 0), new Cell(2, 0));
        Cell[] rotated = shapeCard.Footprint(new Cell(3, 4), 1);
        Check(rotated[0].X == rotated[2].X && Math.Abs(rotated[0].Y - rotated[2].Y) == 2,
            "Rotation normalizes multi-cell shape without changing card metadata");

        var tough = new DemoSettings { MaxHealth = 10000, MovementDamage = 0, CampHealing = 100, DropChance = 1 };
        g = new GameSession(tough);
        for (int i = 0; i < 2000 && !g.VictoryChoice; i++) g.Tick(0.1f);
        Check(g.VictoryChoice && g.WonOnce && g.BossesDefeated == 3, "Third distinct boss opens victory settlement");
        health = g.Health; current = g.Current; g.Tick(1);
        Check(g.Current.Equals(current) && g.Health == health, "Victory settlement freezes simulation");
        g.ContinueAfterVictory();
        Check(g.WonOnce && !g.VictoryChoice && !g.IsPaused, "Continue preserves victory and resumes same map");
        for (int i = 0; i < 800; i++) g.Tick(0.1f);
        Check(g.BossesDefeated > 3 && !g.VictoryChoice, "Endless continuation does not repeat victory prompt");
        var weak = new DemoSettings { MaxHealth = 1, MovementDamage = 2 };
        g = new GameSession(weak); g.Tick(1);
        Check(g.Failed && g.Health == 0 && g.IsPaused, "Death stops simulation and produces failure");

        g = new GameSession();
        for (int i = 0; i < 2500 && !g.VictoryChoice && !g.Failed; i++) g.Tick(0.1f);
        Check(g.VictoryChoice, "Default demo balance reaches three-boss victory without required construction");
        g.ContinueAfterVictory();
        for (int i = 0; i < 30000 && !g.Failed; i++) g.Tick(0.1f);
        Check(g.Failed && g.WonOnce, "Default endless mode eventually fails while preserving win record");
        Console.WriteLine("All " + passed + " checks passed.");
    }
    private static List<Cell> WalkPhase(GameSession game)
    {
        if (game.VictoryChoice) game.ContinueAfterVictory();
        int lap = game.Laps; var path = new List<Cell> { game.Current }; Cell last = game.Current;
        for (int i = 0; i < 10000 && game.Laps == lap && !game.IsPaused; i++)
        {
            game.Tick(0.02f);
            if (game.VictoryChoice) game.ContinueAfterVictory();
            if (!game.Current.Equals(last))
            {
                last = game.Current;
                if (game.Laps == lap) path.Add(last);
            }
        }
        if (game.Laps != lap + 1) throw new Exception("A scheduled phase did not finish: " + game.RouteName +
            ", lap=" + game.Laps + ", manual=" + game.ManualPause + ", victory=" + game.VictoryChoice +
            ", failed=" + game.Failed + ", current=" + game.Current + ", next=" + game.Next +
            ", logs=" + string.Join(" | ", game.Messages.GetRange(0, Math.Min(4, game.Messages.Count))));
        return path;
    }
    private static void MoveOneCell(GameSession game)
    {
        Cell old = game.Current;
        for (int i = 0; i < 1000 && game.Current.Equals(old); i++) game.Tick(0.01f);
    }
    private static bool SamePath(IList<Cell> a, IList<Cell> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (!a[i].Equals(b[i])) return false;
        return true;
    }
    private static void AddTop(GameSession g)
    {
        Card a = g.Hand[0], b = g.Hand[1]; g.Place(a, new Cell(4, 7), 0); g.Place(b, new Cell(6, 7), 0);
    }
    private static int testCardId = 10000;
    private static void AddCustom(GameSession g, string name, Cell position, params Cell[] shape)
    {
        var card = new Card(testCardId++, name, TileKind.Road, testCardId % 10, 1, shape); g.Hand.Add(card);
        if (!g.Place(card, position, 0)) throw new Exception("Cannot place custom test circuit " + name);
    }
    private static void SetRoad(Board b, Cell c, int priority, int level)
    { b.Set(c, new Tile(new Card(c.GetHashCode(), "road", TileKind.Road, priority, level, new Cell(0, 0)))); }
    private static bool UniqueJsonKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
                if (!keys.Add(property.Name) || !UniqueJsonKeys(property.Value)) return false;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement child in element.EnumerateArray()) if (!UniqueJsonKeys(child)) return false;
        return true;
    }
}
