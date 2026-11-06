using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// Moves every tile of the selected tilemap(s) by a whole number of cells, in one step (undoable). The tiles
    /// change cells, the Transform stays put, so all layers keep sharing the same cell coordinates. Selecting a
    /// Grid shifts all its tilemaps together. Per-cell transform (flip/rotation), colour and flags are kept.
    /// Much faster than dragging a large selection with the Tile Palette's Move tool, which repaints the whole
    /// selection at every step of the drag.
    /// </summary>
    public class ShiftTilemapWindow : EditorWindow
    {
        Vector2Int _offset;
        int _step = 1;

        [MenuItem("Tools/Atomic Realm/Shift Tilemap...")]
        static void Open()
        {
            var w = GetWindow<ShiftTilemapWindow>(true, "Shift Tilemap");
            w.minSize = new Vector2(280, 190);
        }

        void OnSelectionChange() => Repaint();

        void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeChanged;
        void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        void OnPlayModeChanged(PlayModeStateChange _) => Repaint();

        // the selected tilemaps, or all tilemaps under a selected Grid
        static List<Tilemap> Targets()
        {
            var maps = new List<Tilemap>();
            foreach (var go in Selection.gameObjects)
            {
                if (go.TryGetComponent(out Tilemap map)) maps.Add(map);
                else if (go.TryGetComponent(out Grid _)) maps.AddRange(go.GetComponentsInChildren<Tilemap>(true));
            }
            return maps.Distinct().Where(m => !EditorUtility.IsPersistent(m)).ToList();
        }

        void OnGUI()
        {
            var maps = Targets();
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            EditorGUILayout.LabelField(playing ? "Not available in Play Mode (changes would be lost when it stops)."
                : maps.Count == 0 ? "Select a Tilemap (or a Grid to shift all its tilemaps)."
                : "Shifting: " + string.Join(", ", maps.Select(m => m.name)), EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(maps.Count == 0 || playing))
            {
                EditorGUILayout.Space();
                _offset = EditorGUILayout.Vector2IntField("Offset (cells)", _offset);
                if (GUILayout.Button("Shift")) Shift(maps, _offset);

                EditorGUILayout.Space();
                _step = Mathf.Max(1, EditorGUILayout.IntField("Nudge step (cells)", _step));
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("↑", GUILayout.Width(40))) Shift(maps, new Vector2Int(0, _step));
                    GUILayout.FlexibleSpace();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("←", GUILayout.Width(40))) Shift(maps, new Vector2Int(-_step, 0));
                    if (GUILayout.Button("↓", GUILayout.Width(40))) Shift(maps, new Vector2Int(0, -_step));
                    if (GUILayout.Button("→", GUILayout.Width(40))) Shift(maps, new Vector2Int(_step, 0));
                    GUILayout.FlexibleSpace();
                }
            }
        }

        /// <summary>Moves all tiles of the given tilemaps by `offset` cells, as one undo step.</summary>
        public static void Shift(IList<Tilemap> maps, Vector2Int offset)
        {
            if (offset == Vector2Int.zero || maps.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Undo.RegisterCompleteObjectUndo(maps.ToArray(), "Shift Tilemap");
            var delta = new Vector3Int(offset.x, offset.y, 0);
            foreach (var map in maps)
            {
                map.CompressBounds();
                var bounds = map.cellBounds;
                if (bounds.size.x <= 0 || bounds.size.y <= 0) continue;

                // read every painted cell with its per-cell data, clear the block, write it back moved
                var changes = new List<TileChangeData>();
                foreach (var p in bounds.allPositionsWithin)
                {
                    var tile = map.GetTile(p);
                    if (tile == null) continue;
                    changes.Add(new TileChangeData(p + delta, tile, map.GetColor(p), map.GetTransformMatrix(p)));
                }
                var flags = changes.Select(c => map.GetTileFlags(c.position - delta)).ToList();
                map.SetTilesBlock(bounds, new TileBase[bounds.size.x * bounds.size.y * bounds.size.z]);
                map.SetTiles(changes.ToArray(), true);
                for (int i = 0; i < changes.Count; i++) map.SetTileFlags(changes[i].position, flags[i]);
                map.CompressBounds();
                EditorUtility.SetDirty(map);
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(maps[0].gameObject.scene);
        }
    }
}
