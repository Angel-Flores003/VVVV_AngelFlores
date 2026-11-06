using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// Pairs each art sprite with its template sprite (and a red "broken" variant) for the editor's Template View,
    /// plus each side's edge signature as written in the connection spec: "S" solid, "F" frame (top/bottom edges),
    /// "Fn" thin frame beam / "Fw" wide frame girder (left/right edges), "-" empty.
    /// </summary>
    public class TileTemplateMap : ScriptableObject
    {
        [Serializable]
        public struct Pair
        {
            public Sprite art;
            public Sprite template;
            public Sprite broken;
            public string north, south, east, west;
        }

        public List<Pair> pairs = new List<Pair>();
    }
}
