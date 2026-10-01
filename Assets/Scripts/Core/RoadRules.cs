using System;
using System.Collections.Generic;

namespace ReverseDefense
{
    public struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;
        public Cell(int x, int y) { X = x; Y = y; }
        public static Cell operator +(Cell a, Cell b) { return new Cell(a.X + b.X, a.Y + b.Y); }
        public static Cell operator -(Cell a, Cell b) { return new Cell(a.X - b.X, a.Y - b.Y); }
        public bool Equals(Cell other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is Cell && Equals((Cell)obj); }
        public override int GetHashCode() { return X * 397 ^ Y; }
        public override string ToString() { return "(" + X + "," + Y + ")"; }
    }

    public enum TileKind { Road, Resource, Healing, Monster, Risk }

    public sealed class Card
    {
        public readonly int Id;
        public readonly string Name;
        public readonly TileKind Kind;
        public readonly int Priority;
        public readonly int Level;
        public readonly Cell[] Shape;
        public Card(int id, string name, TileKind kind, int priority, int level, params Cell[] shape)
        {
            Id = id; Name = name; Kind = kind; Priority = priority; Level = level; Shape = shape;
        }
        public Cell[] Footprint(Cell origin, int rotation)
        {
            var result = new Cell[Shape.Length];
            int minX = int.MaxValue, minY = int.MaxValue;
            for (int i = 0; i < Shape.Length; i++)
            {
                Cell p = Shape[i];
                for (int r = 0; r < ((rotation % 4 + 4) % 4); r++) p = new Cell(p.Y, -p.X);
                result[i] = p; minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
            }
            for (int i = 0; i < result.Length; i++) result[i] = origin + new Cell(result[i].X - minX, result[i].Y - minY);
            return result;
        }
    }

    public sealed class Tile
    {
        public readonly Card Source;
        public TileKind Kind { get { return Source.Kind; } }
        public int Priority { get { return Source.Priority; } }
        public int Level { get { return Source.Level; } }
        public Tile(Card source) { Source = source; }
    }

    public sealed class Board
    {
        public readonly int Width;
        public readonly int Height;
        private readonly Dictionary<Cell, Tile> tiles = new Dictionary<Cell, Tile>();
        public Board(int width, int height) { Width = width; Height = height; }
        public IEnumerable<KeyValuePair<Cell, Tile>> Tiles { get { return tiles; } }
        public bool Contains(Cell c) { return c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height; }
        public Tile Get(Cell c) { Tile t; return tiles.TryGetValue(c, out t) ? t : null; }
        public bool IsRoad(Cell c) { Tile t = Get(c); return t != null && t.Kind == TileKind.Road; }
        public void Set(Cell c, Tile tile) { tiles[c] = tile; }
        public Board Copy()
        {
            var copy = new Board(Width, Height);
            foreach (var pair in tiles) copy.tiles.Add(pair.Key, pair.Value);
            return copy;
        }
    }

    public sealed class RouteResult
    {
        public bool Valid;
        public string Reason;
        public string Name;
        public int DirectionRank = -1;
        public readonly List<Cell> Path = new List<Cell>();
        public readonly HashSet<Cell> Cells = new HashSet<Cell>();
    }

    public sealed class RoutePlan
    {
        public bool Valid;
        public string Reason;
        public readonly List<RouteResult> Routes = new List<RouteResult>();
        public RouteResult Preview
        {
            get
            {
                var preview = new RouteResult { Valid = Valid, Reason = Reason, Name = "整轮路线预览" };
                foreach (RouteResult route in Routes)
                {
                    preview.Path.AddRange(route.Path);
                    foreach (Cell c in route.Cells) preview.Cells.Add(c);
                }
                return preview;
            }
        }
    }

    public static class RoadRules
    {
        // Neighbours and added circuits follow world up, down, left, right.
        public static readonly Cell[] Directions = { new Cell(0, 1), new Cell(0, -1), new Cell(-1, 0), new Cell(1, 0) };

        public static bool TryNext(Board board, Cell previous, Cell current, out Cell next)
        {
            next = current;
            foreach (Cell direction in Directions)
            {
                Cell candidate = current + direction;
                if (candidate.Equals(previous) || !board.IsRoad(candidate)) continue;
                next = candidate; return true;
            }
            return false;
        }

        public static RoutePlan BuildPlan(Board board, IList<Cell> main)
        {
            var plan = new RoutePlan();
            if (main.Count < 4) { plan.Reason = "原主道路没有形成闭环。"; return plan; }
            var primary = MakeRoute(main, "原主道路", -1);
            plan.Routes.Add(primary);
            var rootIndex = new Dictionary<Cell, int>();
            var parent = new Dictionary<Cell, Cell>();
            var ordered = new List<Cell>();
            var queue = new Queue<Cell>();
            for (int i = 0; i < main.Count; i++)
            {
                if (!board.IsRoad(main[i]) || Distance(main[i], main[(i + 1) % main.Count]) != 1 || rootIndex.ContainsKey(main[i]))
                { plan.Reason = "原主道路已断开，无法完成主道路循环。"; return plan; }
                rootIndex.Add(main[i], i); queue.Enqueue(main[i]); ordered.Add(main[i]);
            }
            // Keep the original circuit fixed. Grow a deterministic forest from all main-road cells.
            while (queue.Count > 0)
            {
                Cell c = queue.Dequeue();
                foreach (Cell d in Directions)
                {
                    Cell n = c + d;
                    if (!board.IsRoad(n) || rootIndex.ContainsKey(n)) continue;
                    rootIndex.Add(n, rootIndex[c]); parent.Add(n, c); ordered.Add(n); queue.Enqueue(n);
                }
            }
            foreach (var pair in board.Tiles)
            {
                if (pair.Value.Kind == TileKind.Road)
                {
                    if (!rootIndex.ContainsKey(pair.Key))
                    { plan.Reason = "存在未连接主道路的独立道路。"; return plan; }
                    int neighbours = 0;
                    foreach (Cell d in Directions) if (board.IsRoad(pair.Key + d)) neighbours++;
                    if (neighbours < 2)
                    { plan.Reason = "还有未修完的支路 " + pair.Key + "，请接成闭环后提交。"; return plan; }
                }
                else
                {
                    bool touchesRoad = false;
                    foreach (var other in board.Tiles)
                    {
                        if (other.Value.Source.Id != pair.Value.Source.Id) continue;
                        foreach (Cell d in Directions) if (board.IsRoad(other.Key + d)) touchesRoad = true;
                    }
                    if (!touchesRoad)
                    { plan.Reason = "功能地块必须至少有一格紧邻道路。"; return plan; }
                }
            }

            var extra = new List<RouteResult>();
            var visitedEdges = new HashSet<string>();
            foreach (Cell a in ordered)
            foreach (Cell direction in Directions)
            {
                Cell b = a + direction, p;
                if (!board.IsRoad(b) || !visitedEdges.Add(Edge(a, b))) continue;
                if (primary.Cells.Contains(a) && primary.Cells.Contains(b)) continue;
                if ((parent.TryGetValue(a, out p) && p.Equals(b)) || (parent.TryGetValue(b, out p) && p.Equals(a))) continue;
                // Each non-tree edge introduces one independent added circuit. This avoids exponential
                // enumeration of every possible simple cycle, while covering all completed road cells.
                List<Cell> path = BuildCircuit(main, a, b, parent, rootIndex);
                int rank = DirectionOf(path, primary.Cells);
                extra.Add(MakeRoute(path, "", rank));
            }
            extra.Sort((a, b) =>
            {
                int rank = a.DirectionRank.CompareTo(b.DirectionRank); if (rank != 0) return rank;
                Cell pa = SortPoint(a, primary.Cells), pb = SortPoint(b, primary.Cells);
                int y = pb.Y.CompareTo(pa.Y); if (y != 0) return y;
                int x = pa.X.CompareTo(pb.X); if (x != 0) return x;
                return string.CompareOrdinal(PathKey(a.Path), PathKey(b.Path));
            });
            string[] names = { "上", "下", "左", "右" };
            for (int i = 0; i < extra.Count; i++)
            {
                extra[i].Name = "拼接闭环 " + (i + 1) + "（" + names[extra[i].DirectionRank] + "）";
                plan.Routes.Add(extra[i]);
            }
            var covered = new HashSet<Cell>();
            foreach (RouteResult route in plan.Routes)
            {
                for (int i = 0; i < route.Path.Count; i++)
                    if (Distance(route.Path[i], route.Path[(i + 1) % route.Path.Count]) != 1)
                    { plan.Reason = "拼接闭环连接异常。"; return plan; }
                foreach (Cell c in route.Cells) covered.Add(c);
            }
            foreach (Cell c in ordered)
                if (!covered.Contains(c)) { plan.Reason = "新道路尚未接入可行走的闭环。"; return plan; }
            plan.Valid = true;
            plan.Reason = "原主道路 " + main.Count + " 格，拼接闭环 " + extra.Count + " 条；先走主道路，再按上、下、左、右循环。";
            return plan;
        }

        private static List<Cell> BuildCircuit(IList<Cell> main, Cell a, Cell b, Dictionary<Cell, Cell> parent, Dictionary<Cell, int> roots)
        {
            int ra = roots[a], rb = roots[b];
            List<Cell> ca = Chain(a, parent), cb = Chain(b, parent);
            var path = new List<Cell>();
            if (ra != rb)
            {
                if (ra > rb) { int temp = ra; ra = rb; rb = temp; List<Cell> chain = ca; ca = cb; cb = chain; }
                for (int i = 0; i < ra; i++) path.Add(main[i]);
                ca.Reverse(); path.AddRange(ca); path.AddRange(cb);
                for (int i = rb + 1; i < main.Count; i++) path.Add(main[i]);
            }
            else
            {
                var inB = new HashSet<Cell>(cb); Cell common = ca[ca.Count - 1];
                foreach (Cell c in ca) if (inB.Contains(c)) { common = c; break; }
                ca = ca.GetRange(0, ca.IndexOf(common) + 1);
                cb = cb.GetRange(0, cb.IndexOf(common) + 1);
                ca.Reverse(); var cycle = new List<Cell>(ca); cycle.AddRange(cb);
                if (DirectionIndex(cycle[cycle.Count - 2] - common) < DirectionIndex(cycle[1] - common)) cycle.Reverse();
                List<Cell> access = Chain(common, parent); access.Reverse();
                for (int i = 0; i < ra; i++) path.Add(main[i]);
                path.AddRange(access);
                for (int i = 1; i < cycle.Count; i++) path.Add(cycle[i]);
                for (int i = access.Count - 2; i >= 0; i--) path.Add(access[i]);
                for (int i = ra + 1; i < main.Count; i++) path.Add(main[i]);
            }
            return path;
        }

        private static List<Cell> Chain(Cell c, Dictionary<Cell, Cell> parent)
        {
            var chain = new List<Cell> { c }; Cell p;
            while (parent.TryGetValue(c, out p)) { c = p; chain.Add(c); }
            return chain;
        }
        private static RouteResult MakeRoute(IEnumerable<Cell> path, string name, int direction)
        {
            var route = new RouteResult { Valid = true, Name = name, DirectionRank = direction, Reason = "闭环连接有效。" };
            foreach (Cell c in path) { route.Path.Add(c); route.Cells.Add(c); }
            return route;
        }
        private static int DirectionOf(IList<Cell> path, HashSet<Cell> main)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (Cell c in main) { minX = Math.Min(minX, c.X); maxX = Math.Max(maxX, c.X); minY = Math.Min(minY, c.Y); maxY = Math.Max(maxY, c.Y); }
            int rank = 4;
            foreach (Cell c in path)
            {
                if (main.Contains(c)) continue;
                if (c.Y > maxY) rank = Math.Min(rank, 0);
                else if (c.Y < minY) rank = Math.Min(rank, 1);
                else if (c.X < minX) rank = Math.Min(rank, 2);
                else if (c.X > maxX) rank = Math.Min(rank, 3);
            }
            if (rank < 4) return rank;
            for (int i = 1; i < path.Count; i++)
                if (main.Contains(path[i - 1]) && !main.Contains(path[i])) return DirectionIndex(path[i] - path[i - 1]);
            return 0;
        }
        private static Cell SortPoint(RouteResult route, HashSet<Cell> main)
        {
            Cell point = new Cell(int.MaxValue, int.MinValue);
            foreach (Cell c in route.Path)
                if (!main.Contains(c) && (c.Y > point.Y || (c.Y == point.Y && c.X < point.X))) point = c;
            return point;
        }
        private static int DirectionIndex(Cell d) { for (int i = 0; i < Directions.Length; i++) if (Directions[i].Equals(d)) return i; return 4; }
        private static int Distance(Cell a, Cell b) { return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y); }
        private static string Edge(Cell a, Cell b) { return a.X < b.X || (a.X == b.X && a.Y < b.Y) ? a + ":" + b : b + ":" + a; }
        private static string PathKey(IEnumerable<Cell> path) { return string.Join(";", path); }
    }
}
