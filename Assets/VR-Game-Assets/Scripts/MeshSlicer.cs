using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Talla una malla en dues per un pla (en espai local de la malla).
// Cada triangle es retalla contra el pla (mantenint l'ordre dels vèrtexs perquè les cares no s'inverteixin),
// i la secció tallada es tapa amb una cara nova (submalla extra al final) triangulada amb "ear clipping".
public static class MeshSlicer
{
    private struct Vertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 uv;

        public static Vertex Lerp(Vertex a, Vertex b, float t)
        {
            return new Vertex
            {
                position = Vector3.Lerp(a.position, b.position, t),
                normal = Vector3.Lerp(a.normal, b.normal, t).normalized,
                uv = Vector2.Lerp(a.uv, b.uv, t)
            };
        }
    }

    private class MeshBuilder
    {
        public readonly List<Vector3> positions = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int>[] triangles;

        public MeshBuilder(int subMeshCount)
        {
            triangles = new List<int>[subMeshCount];
            for (int i = 0; i < subMeshCount; i++)
                triangles[i] = new List<int>();
        }

        public void AddTriangle(int subMesh, Vertex a, Vertex b, Vertex c)
        {
            int start = positions.Count;
            Add(a); Add(b); Add(c);
            triangles[subMesh].Add(start);
            triangles[subMesh].Add(start + 1);
            triangles[subMesh].Add(start + 2);
        }

        private void Add(Vertex v)
        {
            positions.Add(v.position);
            normals.Add(v.normal);
            uvs.Add(v.uv);
        }

        public bool IsEmpty => positions.Count == 0;

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (positions.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++)
                mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    /// <summary>
    /// Talla la malla pel pla. "positive" és la part cap on apunta la normal del pla.
    /// Les dues malles resultants tenen una submalla més que l'original: la tapa del tall.
    /// Retorna false si el pla no travessa la malla.
    /// </summary>
    public static bool Slice(Mesh mesh, Plane plane, out Mesh positive, out Mesh negative)
    {
        positive = null;
        negative = null;
        if (mesh == null || !mesh.isReadable)
            return false;

        Vector3[] srcPositions = mesh.vertices;
        Vector3[] srcNormals = mesh.normals;
        Vector2[] srcUvs = mesh.uv;
        bool hasNormals = srcNormals != null && srcNormals.Length == srcPositions.Length;
        bool hasUvs = srcUvs != null && srcUvs.Length == srcPositions.Length;

        var distances = new float[srcPositions.Length];
        for (int i = 0; i < srcPositions.Length; i++)
            distances[i] = plane.GetDistanceToPoint(srcPositions[i]);

        int capSubMesh = mesh.subMeshCount;
        var pos = new MeshBuilder(capSubMesh + 1);
        var neg = new MeshBuilder(capSubMesh + 1);
        var cutSegments = new List<Vector3>();

        var polyPos = new List<Vertex>(4);
        var polyNeg = new List<Vertex>(4);

        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            int[] tris = mesh.GetTriangles(sub);
            for (int t = 0; t < tris.Length; t += 3)
            {
                int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                float d0 = distances[i0], d1 = distances[i1], d2 = distances[i2];

                Vertex v0 = MakeVertex(i0), v1 = MakeVertex(i1), v2 = MakeVertex(i2);

                if (d0 >= 0f && d1 >= 0f && d2 >= 0f)
                {
                    pos.AddTriangle(sub, v0, v1, v2);
                    continue;
                }
                if (d0 < 0f && d1 < 0f && d2 < 0f)
                {
                    neg.AddTriangle(sub, v0, v1, v2);
                    continue;
                }

                // Retall del triangle (Sutherland-Hodgman) mantenint l'ordre original
                polyPos.Clear();
                polyNeg.Clear();
                Vertex[] v = { v0, v1, v2 };
                float[] d = { d0, d1, d2 };
                int cutCount = 0;
                Vector3 cutA = Vector3.zero;

                for (int k = 0; k < 3; k++)
                {
                    int n = (k + 1) % 3;
                    if (d[k] >= 0f) polyPos.Add(v[k]); else polyNeg.Add(v[k]);

                    if ((d[k] >= 0f) != (d[n] >= 0f))
                    {
                        float tt = d[k] / (d[k] - d[n]);
                        Vertex x = Vertex.Lerp(v[k], v[n], tt);
                        polyPos.Add(x);
                        polyNeg.Add(x);

                        if (cutCount == 0) cutA = x.position;
                        else { cutSegments.Add(cutA); cutSegments.Add(x.position); }
                        cutCount++;
                    }
                }

                AddPolygon(pos, sub, polyPos);
                AddPolygon(neg, sub, polyNeg);
            }
        }

        if (pos.IsEmpty || neg.IsEmpty || cutSegments.Count < 6)
            return false;

        // Tapes: la part positiva mira cap a -normal (cap al tall), la negativa cap a +normal
        AddCaps(cutSegments, plane.normal, mesh.bounds, pos, neg, capSubMesh);

        positive = pos.ToMesh(mesh.name + "_A");
        negative = neg.ToMesh(mesh.name + "_B");
        return true;

        Vertex MakeVertex(int index)
        {
            return new Vertex
            {
                position = srcPositions[index],
                normal = hasNormals ? srcNormals[index] : Vector3.up,
                uv = hasUvs ? srcUvs[index] : Vector2.zero
            };
        }
    }

    private static void AddPolygon(MeshBuilder builder, int subMesh, List<Vertex> poly)
    {
        for (int i = 1; i + 1 < poly.Count; i++)
            builder.AddTriangle(subMesh, poly[0], poly[i], poly[i + 1]);
    }

    // ---------- Tapa del tall ----------

    private static void AddCaps(List<Vector3> segments, Vector3 planeNormal, Bounds bounds,
                                MeshBuilder pos, MeshBuilder neg, int capSubMesh)
    {
        float weld = Mathf.Max(bounds.size.magnitude * 1e-4f, 1e-6f);

        // Uneix punts repetits i construeix el graf de segments
        var points = new List<Vector3>();
        var adjacency = new Dictionary<int, List<int>>();
        for (int i = 0; i < segments.Count; i += 2)
        {
            int a = WeldIndex(points, segments[i], weld);
            int b = WeldIndex(points, segments[i + 1], weld);
            if (a == b) continue;
            Link(adjacency, a, b);
            Link(adjacency, b, a);
        }

        // Base 2D sobre el pla per triangular i per a les UVs de la tapa
        Vector3 axisU = Vector3.Cross(planeNormal, Mathf.Abs(planeNormal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 axisV = Vector3.Cross(planeNormal, axisU);
        float uvScale = 1f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 1e-5f);

        foreach (var loop in BuildLoops(adjacency))
        {
            var loop2D = new List<Vector2>(loop.Count);
            foreach (int idx in loop)
                loop2D.Add(new Vector2(Vector3.Dot(points[idx], axisU), Vector3.Dot(points[idx], axisV)));

            // Ear clipping necessita el polígon en sentit antihorari
            if (SignedArea(loop2D) < 0f)
            {
                loop.Reverse();
                loop2D.Reverse();
            }

            List<int> tris = Triangulate(loop2D);
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 a = points[loop[tris[t]]];
                Vector3 b = points[loop[tris[t + 1]]];
                Vector3 c = points[loop[tris[t + 2]]];
                AddCapTriangle(neg, capSubMesh, a, b, c, planeNormal, axisU, axisV, uvScale);
                AddCapTriangle(pos, capSubMesh, a, b, c, -planeNormal, axisU, axisV, uvScale);
            }
        }
    }

    private static void AddCapTriangle(MeshBuilder builder, int subMesh, Vector3 a, Vector3 b, Vector3 c,
                                       Vector3 facing, Vector3 axisU, Vector3 axisV, float uvScale)
    {
        // A Unity la cara visible és la que té Cross(b - a, c - a) en la direcció de la normal
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
        {
            Vector3 tmp = b; b = c; c = tmp;
        }
        builder.AddTriangle(subMesh, CapVertex(a), CapVertex(b), CapVertex(c));

        Vertex CapVertex(Vector3 p) => new Vertex
        {
            position = p,
            normal = facing,
            uv = new Vector2(Vector3.Dot(p, axisU), Vector3.Dot(p, axisV)) * uvScale
        };
    }

    private static int WeldIndex(List<Vector3> points, Vector3 p, float epsilon)
    {
        float eps2 = epsilon * epsilon;
        for (int i = 0; i < points.Count; i++)
            if ((points[i] - p).sqrMagnitude <= eps2)
                return i;
        points.Add(p);
        return points.Count - 1;
    }

    private static void Link(Dictionary<int, List<int>> adjacency, int from, int to)
    {
        if (!adjacency.TryGetValue(from, out var list))
            adjacency[from] = list = new List<int>(2);
        if (!list.Contains(to))
            list.Add(to);
    }

    // Recorre el graf de segments i en treu els contorns tancats
    private static List<List<int>> BuildLoops(Dictionary<int, List<int>> adjacency)
    {
        var loops = new List<List<int>>();
        var usedEdges = new HashSet<long>();

        foreach (var start in adjacency.Keys)
        {
            foreach (var first in adjacency[start])
            {
                if (usedEdges.Contains(EdgeKey(start, first)))
                    continue;

                var loop = new List<int> { start };
                usedEdges.Add(EdgeKey(start, first));
                int current = first;
                int guard = 0;

                while (current != start && guard++ < 100000)
                {
                    loop.Add(current);
                    int next = -1;
                    foreach (var candidate in adjacency[current])
                    {
                        if (!usedEdges.Contains(EdgeKey(current, candidate)))
                        {
                            next = candidate;
                            break;
                        }
                    }
                    if (next == -1)
                        break;
                    usedEdges.Add(EdgeKey(current, next));
                    current = next;
                }

                if (current == start && loop.Count >= 3)
                    loops.Add(loop);
            }
        }
        return loops;
    }

    private static long EdgeKey(int a, int b)
    {
        int min = Mathf.Min(a, b), max = Mathf.Max(a, b);
        return ((long)min << 32) | (uint)max;
    }

    private static float SignedArea(List<Vector2> poly)
    {
        float area = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
            area += a.x * b.y - b.x * a.y;
        }
        return area * 0.5f;
    }

    // Ear clipping per a polígons simples (també còncaus) en sentit antihorari
    private static List<int> Triangulate(List<Vector2> poly)
    {
        var result = new List<int>();
        var remaining = new List<int>(poly.Count);
        for (int i = 0; i < poly.Count; i++)
            remaining.Add(i);

        int guard = 0;
        while (remaining.Count > 3 && guard++ < 100000)
        {
            bool earFound = false;
            int count = remaining.Count;

            for (int i = 0; i < count; i++)
            {
                int ia = remaining[(i - 1 + count) % count];
                int ib = remaining[i];
                int ic = remaining[(i + 1) % count];
                Vector2 a = poly[ia], b = poly[ib], c = poly[ic];

                // Vèrtex reflex o degenerat: no és una orella
                if (Cross(b - a, c - b) <= 1e-12f)
                    continue;

                bool containsOther = false;
                for (int j = 0; j < count; j++)
                {
                    int ip = remaining[j];
                    if (ip == ia || ip == ib || ip == ic) continue;
                    if (PointInTriangle(poly[ip], a, b, c)) { containsOther = true; break; }
                }
                if (containsOther)
                    continue;

                result.Add(ia); result.Add(ib); result.Add(ic);
                remaining.RemoveAt(i);
                earFound = true;
                break;
            }

            // Polígon estrany (punts alineats, autointerseccions): acaba amb un ventall
            if (!earFound)
                break;
        }

        for (int i = 1; i + 1 < remaining.Count; i++)
        {
            result.Add(remaining[0]); result.Add(remaining[i]); result.Add(remaining[i + 1]);
        }
        return result;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float c1 = Cross(b - a, p - a);
        float c2 = Cross(c - b, p - b);
        float c3 = Cross(a - c, p - c);
        return c1 >= 0f && c2 >= 0f && c3 >= 0f;
    }
}
