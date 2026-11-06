using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace AtomicRealm.WorldBuilder
{
    static class TemplateViewMenu
    {
        const string Item = "Tools/Atomic Realm/Template View";

        [MenuItem(Item + " %&t")]
        static void Toggle() => TemplateView.SetOn(!TemplateView.On);

        [MenuItem(Item + " %&t", true)]
        static bool Validate()
        {
            Menu.SetChecked(Item, TemplateView.On);
            return true;
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    class TemplateViewToggle : EditorToolbarToggle
    {
        public const string Id = "AtomicRealm/TemplateViewToggle";

        public TemplateViewToggle()
        {
            text = "Template View";
            tooltip = "Draw Atomic Realm tiles' templates over them, red where a seam doesn't match (editor only, nothing painted changes). Ctrl+Alt+T";
            this.RegisterValueChangedCallback(e => TemplateView.SetOn(e.newValue));
            // follow the menu / shortcut while shown (the toolbar can be collapsed and shown again)
            RegisterCallback<AttachToPanelEvent>(_ => { TemplateView.AddChangedListener(Sync); Sync(); });
            RegisterCallback<DetachFromPanelEvent>(_ => TemplateView.RemoveChangedListener(Sync));
        }

        void Sync() => SetValueWithoutNotify(TemplateView.On);
    }

    // readout next to the button: the overlaid tilemap(s), their tiles in this Scene view, and anything that keeps
    // their templates from showing (from the view's last redraw)
    [EditorToolbarElement(Id, typeof(SceneView))]
    class TemplateViewStats : Label
    {
        public const string Id = "AtomicRealm/TemplateViewStats";

        public TemplateViewStats()
        {
            style.unityTextAlign = UnityEngine.TextAnchor.MiddleLeft;
            style.marginLeft = 4; style.marginRight = 4;
            tooltip = "Template View: the selected tilemap(s) (all tilemaps when none is selected), its tiles in this Scene view, and anything that keeps their templates from showing";
            schedule.Execute(Refresh).Every(250);
            Refresh();
        }

        void Refresh() => text = Describe(OwnSceneView());

        // the Scene view this readout sits in
        SceneView OwnSceneView()
        {
            foreach (SceneView v in SceneView.sceneViews)
                if (v != null && v.rootVisualElement.panel == panel) return v;
            return null;
        }

        static string Describe(SceneView view)
        {
            if (!TemplateView.On) return "Off";
            var st = TemplateView.StatsFor(view);
            if (st == null) return "";
            if (st.tiles == 0) return (st.layer != null ? st.layer + ": " : "") + (st.zoomedOut ? "Zoomed out (not drawn)" : "0 tiles");
            string s = st.layer != null ? $"{st.layer}: {st.tiles} tiles" : $"{st.tiles} tiles";
            if (st.failed) return s + " · Overlay error (see console)";
            if (st.unreadable > 0) return s + $" · {st.unreadable} templates unreadable (packed in a Sprite Atlas?)";
            if (st.withoutTemplate > 0 && st.withoutTemplate == st.atomicRealm) return s + " · No templates found";
            if (st.withoutTemplate > 0) return s + $" · {st.withoutTemplate} without template";
            return s;
        }
    }

    [Overlay(typeof(SceneView), "AtomicRealm/TemplateView", "Atomic Realm", true)]
    class TemplateViewOverlay : ToolbarOverlay
    {
        TemplateViewOverlay() : base(TemplateViewToggle.Id, TemplateViewStats.Id) { }
    }
}
