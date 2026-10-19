using UnityEngine;

namespace ReverseDefense
{
    // 场景内的 Image 在编辑时生成并保存，运行时不重建、不覆盖图片。
    public sealed class GridMapView : MonoBehaviour
    {
        [InspectorName("地图外观")] public MapAppearance Appearance;
        public RectTransform MapRoot;
        public RectTransform Player;
        [HideInInspector] public int Columns;
        [HideInInspector] public int Rows;
        [HideInInspector] public float CellPixels;

        public Vector2 CellToLocal(Cell cell)
        {
            return new Vector2((cell.X - (Columns - 1) * 0.5f) * CellPixels,
                (cell.Y - (Rows - 1) * 0.5f) * CellPixels);
        }
    }
}
