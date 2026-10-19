using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ReverseDefense.Editor
{
    [CustomEditor(typeof(MapAppearance))]
    public sealed class MapAppearanceInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("拖入图片后，当前打开场景中使用此配置的地图会立即更新，无需 Play。清空图片会恢复占位颜色。图片染色默认为白色，显示原图颜色。", MessageType.Info);
            bool changed = DrawDefaultInspector();
            if (GUILayout.Button("应用到当前打开的地图") || changed)
                MapAppearanceEditorUtility.ApplyLoadedMaps((MapAppearance)target);
        }
    }

    [CustomEditor(typeof(GridMapView))]
    public sealed class GridMapViewInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            bool changed = DrawDefaultInspector();
            var map = (GridMapView)target;
            if (changed) MapAppearanceEditorUtility.Apply(map);
            if (GUILayout.Button("选择地图外观配置"))
            {
                if (map.Appearance == null)
                {
                    Undo.RecordObject(map, "绑定地图外观");
                    map.Appearance = MapAppearanceEditorUtility.GetDefaultAppearance();
                    EditorUtility.SetDirty(map);
                    EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
                }
                Selection.activeObject = map.Appearance;
                EditorGUIUtility.PingObject(map.Appearance);
            }
            if (GUILayout.Button("应用外观到整张地图")) MapAppearanceEditorUtility.Apply(map);
        }
    }

    [CustomEditor(typeof(MapImageBinding)), CanEditMultipleObjects]
    public sealed class MapImageBindingInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (DrawDefaultInspector()) MapAppearanceEditorUtility.ApplyLoadedMaps(null);
        }
    }

    public static class MapAppearanceEditorUtility
    {
        public const string DefaultPath = "Assets/Settings/DefaultMapAppearance.asset";

        public static MapAppearance GetDefaultAppearance()
        {
            var appearance = AssetDatabase.LoadAssetAtPath<MapAppearance>(DefaultPath);
            if (appearance != null) return appearance;
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            appearance = ScriptableObject.CreateInstance<MapAppearance>();
            AssetDatabase.CreateAsset(appearance, DefaultPath);
            AssetDatabase.SaveAssets();
            return appearance;
        }

        public static void Bind(Image image, MapImageKind kind)
        {
            var binding = image.GetComponent<MapImageBinding>();
            if (binding == null)
            {
                binding = Undo.AddComponent<MapImageBinding>(image.gameObject);
                // 首次迁移时保留已经手工替换过的图片。
                binding.UseCustomAppearance = image.sprite != null;
            }
            Undo.RecordObject(binding, "设置外观类别");
            binding.Kind = kind;
            EditorUtility.SetDirty(binding);
        }

        public static void Apply(GridMapView map)
        {
            if (map.Appearance == null || map.MapRoot == null || EditorApplication.isPlaying) return;
            foreach (var binding in map.MapRoot.GetComponentsInChildren<MapImageBinding>(true))
            {
                if (binding.UseCustomAppearance) continue;
                var image = binding.GetComponent<Image>();
                Undo.RecordObject(image, "批量应用地图外观");
                map.Appearance.Apply(image, binding.Kind);
                EditorUtility.SetDirty(image);
            }
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void ApplyLoadedMaps(MapAppearance appearance)
        {
            foreach (var map in UnityEngine.Object.FindObjectsOfType<GridMapView>(true))
                if (appearance == null || map.Appearance == appearance) Apply(map);
        }
    }
}
