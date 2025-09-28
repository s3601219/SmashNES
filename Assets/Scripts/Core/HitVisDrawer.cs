using System.Collections.Generic;
using UnityEngine;

public class HitVisDrawer : MonoBehaviour
{
    public static HitVisDrawer Instance { get; private set; }

    [Header("Common")]
    public bool visible = true;

    [Tooltip("Keep drawings visible when paused (timeScale == 0).")]
    public bool persistWhenPaused = true;

    [Tooltip("Disable geometry regeneration until the next FixedUpdate to avoid flicker.")]
    public bool syncToPhysics = true;

    [Header("Solid Render (Meshes)")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 32760;
    public float zOffset = -0.01f;
    [Range(8, 256)] public int solidCircleSegments = 32;

    [Header("Outline Render (LineRenderers)")]
    public Material lineMaterial;
    [Range(0.001f, 0.25f)] public float lineWidth = 0.03f;
    [Range(8, 256)] public int outlineCircleSegments = 32;

    // Pools
    readonly List<MeshRenderer> meshPool = new List<MeshRenderer>();
    readonly List<LineRenderer> linePool = new List<LineRenderer>();
    int meshUsedThisStep = 0;
    int lineUsedThisStep = 0;

    // Materials
    Material solidMat;

    // We clear at the start of each physics step. When paused, we skip clearing.
    void FixedUpdate()
    {
        if (persistWhenPaused && Time.timeScale == 0f) return;

        // Clear geometry for anything not used LAST physics step
        for (int i = meshUsedThisStep; i < meshPool.Count; i++)
        {
            var mr = meshPool[i];
            var mf = mr ? mr.GetComponent<MeshFilter>() : null;
            if (mf && mf.sharedMesh) mf.sharedMesh.Clear();
            // keep GO enabled; geometry is empty → no flicker
        }
        for (int i = lineUsedThisStep; i < linePool.Count; i++)
        {
            var lr = linePool[i];
            if (lr) lr.positionCount = 0;
        }

        meshUsedThisStep = 0;
        lineUsedThisStep = 0;
    }

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var shader = Shader.Find("Sprites/Default");
        solidMat = new Material(shader) { color = Color.white };
        if (!lineMaterial) lineMaterial = new Material(shader);

        // Stability
        solidMat.enableInstancing = false;
        lineMaterial.enableInstancing = false;
    }

    // ---------- Solid (Mesh) ----------
    MeshRenderer GetMeshRenderer()
    {
        if (!visible) return null;

        if (meshUsedThisStep < meshPool.Count)
        {
            var mr = meshPool[meshUsedThisStep++];
            // never disable/enable to avoid flicker; just keep active
            return mr;
        }

        var go = new GameObject($"HitVisSolid_{meshPool.Count}");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>();
        var newMr = go.AddComponent<MeshRenderer>();
        newMr.sharedMaterial = solidMat;
        newMr.sortingLayerName = sortingLayerName;
        newMr.sortingOrder = sortingOrder;
        meshPool.Add(newMr);
        meshUsedThisStep++;
        return newMr;
    }

    public void DrawCircleSolid(Vector2 center, float radius, Color color, int? segOverride = null)
    {
        if (!visible || radius <= 0f) return;

        int segs = Mathf.Clamp(segOverride ?? solidCircleSegments, 8, 256);
        var mr = GetMeshRenderer();
        if (!mr) return;

        var mf = mr.GetComponent<MeshFilter>();

        int vertCount = segs + 1;
        int triCount  = segs;

        var verts = new Vector3[vertCount];
        var cols  = new Color[vertCount];
        var tris  = new int[triCount * 3];

        verts[0] = new Vector3(center.x, center.y, zOffset);
        cols[0]  = color;

        float step = Mathf.PI * 2f / segs;
        for (int i = 0; i < segs; i++)
        {
            float a = i * step;
            verts[i + 1] = new Vector3(
                center.x + Mathf.Cos(a) * radius,
                center.y + Mathf.Sin(a) * radius,
                zOffset);
            cols[i + 1] = color;

            tris[i * 3 + 0] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 2 <= segs) ? (i + 2) : 1;
        }

        var mesh = mf.sharedMesh;
        if (!mesh) { mesh = new Mesh(); mf.sharedMesh = mesh; }
        mesh.Clear();
        mesh.vertices  = verts;
        mesh.colors    = cols;
        mesh.triangles = tris;
    }

    public void DrawBoxSolid(Vector2 center, Vector2 size, Color color)
    {
        if (!visible || size.x <= 0f || size.y <= 0f) return;

        var mr = GetMeshRenderer();
        if (!mr) return;
        var mf = mr.GetComponent<MeshFilter>();

        Vector2 h = size * 0.5f;
        var verts = new Vector3[4]
        {
            new Vector3(center.x - h.x, center.y - h.y, zOffset),
            new Vector3(center.x - h.x, center.y + h.y, zOffset),
            new Vector3(center.x + h.x, center.y + h.y, zOffset),
            new Vector3(center.x + h.x, center.y - h.y, zOffset)
        };
        var cols = new Color[4] { color, color, color, color };
        var tris = new int[6] { 0, 1, 2, 0, 2, 3 };

        var mesh = mf.sharedMesh;
        if (!mesh) { mesh = new Mesh(); mf.sharedMesh = mesh; }
        mesh.Clear();
        mesh.vertices  = verts;
        mesh.colors    = cols;
        mesh.triangles = tris;
    }

    // ---------- Outline (LineRenderer) ----------
    LineRenderer GetLineRenderer()
    {
        if (!visible) return null;

        if (lineUsedThisStep < linePool.Count)
        {
            var lr = linePool[lineUsedThisStep++];
            return lr;
        }

        var go = new GameObject($"HitVisLine_{linePool.Count}");
        go.transform.SetParent(transform, false);
        var newLR = go.AddComponent<LineRenderer>();
        newLR.material = lineMaterial;
        newLR.widthMultiplier = lineWidth;
        newLR.positionCount = 0;
        newLR.loop = true;
        newLR.useWorldSpace = true;
        newLR.numCapVertices = 2;
        newLR.sortingLayerName = sortingLayerName;
        newLR.sortingOrder = sortingOrder + 1; // outline above fill
        linePool.Add(newLR);
        lineUsedThisStep++;
        return newLR;
    }

    public void DrawCircle(Vector2 center, float radius, Color color, int? segOverride = null)
    {
        if (!visible || radius <= 0f) return;

        int segs = Mathf.Clamp(segOverride ?? outlineCircleSegments, 8, 256);
        var lr = GetLineRenderer();
        if (!lr) return;

        lr.startColor = lr.endColor = color;
        lr.positionCount = segs;

        float step = Mathf.PI * 2f / segs;
        for (int i = 0; i < segs; i++)
        {
            float a = i * step;
            lr.SetPosition(i, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
        }
    }

    public void DrawBox(Vector2 center, Vector2 size, Color color)
    {
        if (!visible || size.x <= 0f || size.y <= 0f) return;

        var lr = GetLineRenderer();
        if (!lr) return;

        lr.startColor = lr.endColor = color;
        lr.positionCount = 4;

        Vector2 h = size * 0.5f;
        lr.SetPosition(0, new Vector3(center.x - h.x, center.y - h.y));
        lr.SetPosition(1, new Vector3(center.x - h.x, center.y + h.y));
        lr.SetPosition(2, new Vector3(center.x + h.x, center.y + h.y));
        lr.SetPosition(3, new Vector3(center.x + h.x, center.y - h.y));
    }

    // Utility
    public void ToggleVisible() => visible = !visible;

    [ContextMenu("Clear All Now")]
    public void ClearAllNow()
    {
        foreach (var mr in meshPool)
        {
            var mf = mr ? mr.GetComponent<MeshFilter>() : null;
            if (mf && mf.sharedMesh) mf.sharedMesh.Clear();
        }
        foreach (var lr in linePool)
            if (lr) lr.positionCount = 0;

        meshUsedThisStep = 0;
        lineUsedThisStep = 0;
    }
}
