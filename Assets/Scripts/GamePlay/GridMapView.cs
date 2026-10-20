using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReverseDefense
{
    // 场景内的 Image 在编辑时生成并保存，运行时不重建、不覆盖图片。
    public sealed class GridMapView : MonoBehaviour
    {
        [InspectorName("地图外观")] public MapAppearance Appearance;
        public RectTransform MapRoot;
        public RectTransform Player;
        [Tooltip("按行走顺序拖入道路 Image 或空 RectTransform，第0项为起点，不要重复起点。填写后优先使用此路线，不生成地图。")]
        public RectTransform[] RoadNodes = new RectTransform[0];
        [Header("人物翻转（地块编号从1开始）")]
        [Tooltip("12表示 Road Nodes 的 Element 11，到达该地块时开始翻转。")]
        [Min(1)] public int FlipStartNode = 12;
        [Tooltip("包含该地块，到达下一块后恢复正常。")]
        [Min(1)] public int FlipEndNode = 19;
        [HideInInspector] public int Columns;
        [HideInInspector] public int Rows;
        [HideInInspector] public float CellPixels;

        public Vector2 CellToLocal(Cell cell)
        {
            return new Vector2((cell.X - (Columns - 1) * 0.5f) * CellPixels,
                (cell.Y - (Rows - 1) * 0.5f) * CellPixels);
        }

        public bool HasManualRoad { get { return RoadNodes != null && RoadNodes.Length > 0; } }

        public int ValidateRoad(List<Cell> gridPath)
        {
            if (Player == null) throw new ArgumentException("请将场景中的人物 Image 拖到 Grid Map View / Player。");
            if (!HasManualRoad)
            {
                if (MapRoot == null) throw new ArgumentException("请绑定 Road Nodes；使用旧网格路线时需绑定 Map Root。");
                return gridPath.Count;
            }
            if (RoadNodes.Length < 2) throw new ArgumentException("Road Nodes 至少需要两个节点。");
            var used = new HashSet<RectTransform>();
            foreach (var node in RoadNodes)
                if (node == null || node == Player || !used.Add(node))
                    throw new ArgumentException("Road Nodes 不可为空、重复或使用人物自身；起点只填写一次。");
            return RoadNodes.Length;
        }

        public Vector3 RoadPosition(int index, List<Cell> gridPath)
        {
            if (HasManualRoad) return RoadNodes[index].TransformPoint(RoadNodes[index].rect.center);
            return MapRoot.TransformPoint(CellToLocal(gridPath[index]));
        }

        public void UpdatePlayerFacing(int roadIndex)
        {
            int nodeNumber = roadIndex + 1;
            Vector3 angles = Player.localEulerAngles;
            angles.y = nodeNumber >= FlipStartNode && nodeNumber <= FlipEndNode ? -180f : 0f;
            Player.localRotation = Quaternion.Euler(angles);
        }
    }
}
