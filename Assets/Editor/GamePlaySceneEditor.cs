using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ReverseDefense.Editor
{
    [CustomEditor(typeof(FixedLoopGamePlay))]
    public sealed class FixedLoopGamePlayInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("在 Grid Map View 绑定场景人物与 Road Nodes；在此绑定开始、暂停按钮及回合文字。绑定 Monster Prefab 和 Monster Root 后会在道路随机生怪，遇怪自动战斗。共10回合；暂停后点击开始继续。无需配置人物状态机。", MessageType.Info);
        }
    }

    public static class GamePlaySceneEditor
    {
        [MenuItem("玩法/生成或更新当前场景的 UGUI 地图")]
        public static void SetupCurrentScene()
        {
            if (EditorApplication.isPlaying) return;
            var game = UnityEngine.Object.FindObjectOfType<FixedLoopGamePlay>();
            if (game == null)
            {
                var go = new GameObject("GamePlay");
                Undo.RegisterCreatedObjectUndo(go, "创建 GamePlay");
                game = Undo.AddComponent<FixedLoopGamePlay>(go);
            }
            // 兼容上一版纵向布局的场景组件。
            if (game.Columns == 10 && game.Rows == 20)
            {
                Undo.RecordObject(game, "改为横向地图");
                game.Columns = 20; game.Rows = 10;
                game.Left = 5; game.Right = 14; game.Bottom = 2; game.Top = 7;
            }
            Build(game);
            Selection.activeGameObject = game.gameObject;
        }

        public static void Build(FixedLoopGamePlay game)
        {
            if (EditorApplication.isPlaying) return;
            List<Cell> path;
            try { path = FixedLoopPath.Create(game.Columns, game.Rows, game.Left, game.Bottom, game.Right, game.Top); }
            catch (ArgumentException e) { Debug.LogError(e.Message, game); return; }
            if (game.CellPixels < 8) { Debug.LogError("Cell Pixels 至少为 8。", game); return; }

            var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(go, "创建 Canvas");
                canvas = go.GetComponent<Canvas>();
            }
            Undo.RecordObject(canvas, "配置 Canvas");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>() ?? Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            Undo.RecordObject(scaler, "配置屏幕适配");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(events, "创建 EventSystem");
            }

            var map = game.GetComponent<GridMapView>();
            Undo.RecordObject(map, "配置地图引用");
            if (map.Appearance == null) map.Appearance = MapAppearanceEditorUtility.GetDefaultAppearance();
            map.Columns = game.Columns; map.Rows = game.Rows; map.CellPixels = game.CellPixels;
            var root = map.MapRoot != null ? map.MapRoot : Rect("MapRoot", canvas.transform);
            map.MapRoot = root;
            Center(root, new Vector2(game.Columns, game.Rows) * game.CellPixels, Vector2.zero);
            var tiles = Rect("Tiles", root);
            Center(tiles, root.sizeDelta, Vector2.zero);
            var roads = new HashSet<Cell>(path);
            var expected = new HashSet<string>();
            for (int y = 0; y < game.Rows; y++)
                for (int x = 0; x < game.Columns; x++)
                {
                    var cell = new Cell(x, y);
                    string name = "Cell_" + x + "_" + y;
                    expected.Add(name);
                    var tile = Rect(name, tiles);
                    Center(tile, Vector2.one * (game.CellPixels - 3), map.CellToLocal(cell));
                    var image = tile.GetComponent<Image>() ?? Undo.AddComponent<Image>(tile.gameObject);
                    Undo.RecordObject(image, "配置地块");
                    image.raycastTarget = false;
                    MapAppearanceEditorUtility.Bind(image, cell.Equals(path[0]) ? MapImageKind.Camp :
                        roads.Contains(cell) ? MapImageKind.Road : MapImageKind.Ground);
                }
            for (int i = tiles.childCount - 1; i >= 0; i--)
                if (!expected.Contains(tiles.GetChild(i).name)) Undo.DestroyObjectImmediate(tiles.GetChild(i).gameObject);
            var actorLayer = Rect("Actors", root);
            Center(actorLayer, root.sizeDelta, Vector2.zero);
            var player = Rect("Player", actorLayer);
            Center(player, Vector2.one * game.CellPixels * 0.52f, map.CellToLocal(path[0]));
            var actorImage = player.GetComponent<Image>() ?? Undo.AddComponent<Image>(player.gameObject);
            Undo.RecordObject(actorImage, "配置角色");
            MapAppearanceEditorUtility.Bind(actorImage, MapImageKind.Player);
            actorImage.raycastTarget = false;
            map.Player = player;
            MapAppearanceEditorUtility.Apply(map);
            actorLayer.SetAsLastSibling();
            EditorUtility.SetDirty(map);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "创建 " + name);
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Center(RectTransform rect, Vector2 size, Vector2 position)
        {
            Undo.RecordObject(rect, "设置 UI 布局");
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = size;
            rect.anchoredPosition3D = new Vector3(position.x, position.y, 0);
        }

        // 在独立的编辑器项目中生成场景，不需要进入 Play。
        public static void PrepareSceneBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            SetupCurrentScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            var map = UnityEngine.Object.FindObjectOfType<GridMapView>();
            if (map == null || map.Columns != 20 || map.Rows != 10 || map.MapRoot.GetComponentsInChildren<Image>().Length != 201 ||
                map.MapRoot.anchoredPosition != Vector2.zero || map.Player == null)
                throw new Exception("UGUI 场景保存验证失败。");
            Debug.Log("UGUI_SCENE_VERIFIED: 20x10 centered grid, 200 cell Images and 1 player Image, saved without Play.");
        }
    }
}

