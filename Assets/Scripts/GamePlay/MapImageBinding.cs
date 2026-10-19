using UnityEngine;
using UnityEngine.UI;

namespace ReverseDefense
{
    [DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public sealed class MapImageBinding : MonoBehaviour
    {
        [InspectorName("外观类别")] public MapImageKind Kind;
        [InspectorName("独立外观（不跟随地图外观）")]
        [Tooltip("勾选后，可单独修改本物体的 Image；批量应用和更新地图都会保留它。")]
        public bool UseCustomAppearance;
    }
}
