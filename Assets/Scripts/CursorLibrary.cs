using System;
using UnityEngine;

public enum CursorType
{
    Pointer
}

// Maps each CursorType to a texture. Lives at Assets/Resources/CursorLibrary.asset
[CreateAssetMenu(fileName = "CursorLibrary", menuName = "Pawjections/Cursor Library")]
public class CursorLibrary : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public CursorType type;
        public Texture2D texture;

        [Tooltip("Pixel offset within the texture that marks the actual click point")]
        public Vector2 hotspot;
    }

    public Entry[] cursors;

    private static CursorLibrary instance;

    public static CursorLibrary Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<CursorLibrary>("CursorLibrary");
            }
            return instance;
        }
    }

    public static void Set(CursorType type)
    {
        foreach (Entry entry in Instance.cursors)
        {
            if (entry.type == type)
            {
                Cursor.SetCursor(entry.texture, entry.hotspot, CursorMode.Auto);
                return;
            }
        }
        Debug.LogWarning($"CursorLibrary has no entry for {type}");
    }

    public static void Reset()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
