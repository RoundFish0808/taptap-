using UnityEngine;
using UnityEngine.UI;

namespace ReverseDefense
{
    public enum MapImageKind { Ground, Road, Camp, Player }

    [CreateAssetMenu(fileName = "地图外观", menuName = "逆塔防/地图外观")]
    public sealed class MapAppearance : ScriptableObject
    {
        [InspectorName("普通地面图片")] public Sprite Ground;
        [InspectorName("道路图片")] public Sprite Road;
        [InspectorName("营地图片")] public Sprite Camp;
        [InspectorName("角色图片")] public Sprite Player;
        [InspectorName("图片染色")] public Color Tint = Color.white;

        public void Apply(Image image, MapImageKind kind)
        {
            Sprite sprite;
            Color placeholder;
            switch (kind)
            {
                case MapImageKind.Road:
                    sprite = Road; placeholder = new Color(0.78f, 0.65f, 0.36f); break;
                case MapImageKind.Camp:
                    sprite = Camp; placeholder = new Color(0.25f, 0.75f, 0.48f); break;
                case MapImageKind.Player:
                    sprite = Player; placeholder = new Color(0.3f, 0.8f, 1f); break;
                default:
                    sprite = Ground; placeholder = new Color(0.18f, 0.24f, 0.28f); break;
            }
            image.sprite = sprite;
            image.color = sprite != null ? Tint : placeholder;
            image.type = Image.Type.Simple;
            image.preserveAspect = kind == MapImageKind.Player;
        }
    }
}
