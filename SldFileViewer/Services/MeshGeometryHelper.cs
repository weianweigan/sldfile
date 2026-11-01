using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace SldFileViewer.Services;

/// <summary>
/// 纯托管高性能 3D 网格几何辅助生成工具（测量引线、双向工程箭头、球体锚点标记、包围盒线框、圆环拟合与高亮）
/// </summary>
public static class MeshGeometryHelper
{
    /// <summary>
    /// 在两点之间生成立体粗线段
    /// </summary>
    public static void AppendThickLine(MeshGeometry3D geo, Point3D p0, Point3D p1, double thickness)
    {
        var dir = p1 - p0;
        double len = dir.Length;
        if (len < 1e-4) return;

        // 构造垂直于 dir 的两个正交基向量 u 和 w
        Vector3D v = Math.Abs(dir.X) > 0.9 * len ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0);
        Vector3D u = Vector3D.CrossProduct(dir, v);
        u.Normalize();
        u *= thickness;

        Vector3D w = Vector3D.CrossProduct(dir, u);
        w.Normalize();
        w *= thickness;

        int vertexBase = geo.Positions.Count;

        // 两个正交十字交叉四边形面带（双面可见，保证全方位视角丰满立体）
        geo.Positions.Add(new Point3D(p0.X - u.X, p0.Y - u.Y, p0.Z - u.Z));
        geo.Positions.Add(new Point3D(p0.X + u.X, p0.Y + u.Y, p0.Z + u.Z));
        geo.Positions.Add(new Point3D(p1.X + u.X, p1.Y + u.Y, p1.Z + u.Z));
        geo.Positions.Add(new Point3D(p1.X - u.X, p1.Y - u.Y, p1.Z - u.Z));

        geo.Positions.Add(new Point3D(p0.X - w.X, p0.Y - w.Y, p0.Z - w.Z));
        geo.Positions.Add(new Point3D(p0.X + w.X, p0.Y + w.Y, p0.Z + w.Z));
        geo.Positions.Add(new Point3D(p1.X + w.X, p1.Y + w.Y, p1.Z + w.Z));
        geo.Positions.Add(new Point3D(p1.X - w.X, p1.Y - w.Y, p1.Z - w.Z));

        // 面 1 正反双面
        geo.TriangleIndices.Add(vertexBase);
        geo.TriangleIndices.Add(vertexBase + 1);
        geo.TriangleIndices.Add(vertexBase + 2);
        geo.TriangleIndices.Add(vertexBase);
        geo.TriangleIndices.Add(vertexBase + 2);
        geo.TriangleIndices.Add(vertexBase + 3);

        geo.TriangleIndices.Add(vertexBase);
        geo.TriangleIndices.Add(vertexBase + 2);
        geo.TriangleIndices.Add(vertexBase + 1);
        geo.TriangleIndices.Add(vertexBase);
        geo.TriangleIndices.Add(vertexBase + 3);
        geo.TriangleIndices.Add(vertexBase + 2);

        // 面 2 正反双面
        geo.TriangleIndices.Add(vertexBase + 4);
        geo.TriangleIndices.Add(vertexBase + 5);
        geo.TriangleIndices.Add(vertexBase + 6);
        geo.TriangleIndices.Add(vertexBase + 4);
        geo.TriangleIndices.Add(vertexBase + 6);
        geo.TriangleIndices.Add(vertexBase + 7);

        geo.TriangleIndices.Add(vertexBase + 4);
        geo.TriangleIndices.Add(vertexBase + 6);
        geo.TriangleIndices.Add(vertexBase + 5);
        geo.TriangleIndices.Add(vertexBase + 4);
        geo.TriangleIndices.Add(vertexBase + 7);
        geo.TriangleIndices.Add(vertexBase + 6);
    }

    /// <summary>
    /// 在指定位置生成球体网格（用于测距点图钉/锚点）
    /// </summary>
    public static void AppendSphere(MeshGeometry3D geo, Point3D center, double radius, int latBands = 8, int longBands = 12)
    {
        if (radius <= 0) return;
        int vertexBase = geo.Positions.Count;

        for (int lat = 0; lat <= latBands; lat++)
        {
            double theta = lat * Math.PI / latBands;
            double sinTheta = Math.Sin(theta);
            double cosTheta = Math.Cos(theta);

            for (int lon = 0; lon <= longBands; lon++)
            {
                double phi = lon * 2 * Math.PI / longBands;
                double sinPhi = Math.Sin(phi);
                double cosPhi = Math.Cos(phi);

                double x = cosPhi * sinTheta;
                double y = cosTheta;
                double z = sinPhi * sinTheta;

                geo.Positions.Add(new Point3D(center.X + radius * x, center.Y + radius * y, center.Z + radius * z));
                geo.Normals.Add(new Vector3D(x, y, z));
            }
        }

        for (int lat = 0; lat < latBands; lat++)
        {
            for (int lon = 0; lon < longBands; lon++)
            {
                int first = vertexBase + (lat * (longBands + 1)) + lon;
                int second = first + longBands + 1;

                geo.TriangleIndices.Add(first);
                geo.TriangleIndices.Add(second);
                geo.TriangleIndices.Add(first + 1);

                geo.TriangleIndices.Add(second);
                geo.TriangleIndices.Add(second + 1);
                geo.TriangleIndices.Add(first + 1);
            }
        }
    }

    /// <summary>
    /// 在指定位置生成实体锥形尺寸工程箭头（从 baseCenter 指向 tip）
    /// </summary>
    public static void AppendCone(MeshGeometry3D geo, Point3D tip, Point3D baseCenter, double radius, int segments = 12)
    {
        var axis = tip - baseCenter;
        double len = axis.Length;
        if (len < 1e-4 || radius <= 1e-4) return;
        axis.Normalize();

        Vector3D v = Math.Abs(axis.X) > 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0);
        Vector3D u = Vector3D.CrossProduct(axis, v);
        u.Normalize();
        Vector3D w = Vector3D.CrossProduct(axis, u);
        w.Normalize();

        int vertexBase = geo.Positions.Count;
        geo.Positions.Add(tip); // 尖端 (索引 0)
        geo.Positions.Add(baseCenter); // 底面中心 (索引 1)

        // 底面圆周顶点
        for (int i = 0; i < segments; i++)
        {
            double angle = i * 2 * Math.PI / segments;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            Point3D pt = baseCenter + u * (radius * cos) + w * (radius * sin);
            geo.Positions.Add(pt);
        }

        // 锥体侧面与底面封口
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int idxCurrent = vertexBase + 2 + i;
            int idxNext = vertexBase + 2 + next;

            // 侧面（双面）
            geo.TriangleIndices.Add(vertexBase);
            geo.TriangleIndices.Add(idxCurrent);
            geo.TriangleIndices.Add(idxNext);

            geo.TriangleIndices.Add(vertexBase);
            geo.TriangleIndices.Add(idxNext);
            geo.TriangleIndices.Add(idxCurrent);

            // 底面（封口）
            geo.TriangleIndices.Add(vertexBase + 1);
            geo.TriangleIndices.Add(idxNext);
            geo.TriangleIndices.Add(idxCurrent);
        }
    }

    /// <summary>
    /// 在指定位置和法线下生成平滑立体圆环管（用于圆孔边缘高亮）
    /// </summary>
    public static void AppendCircleRing(MeshGeometry3D geo, Point3D center, Vector3D normal, double radius, double thickness, int segments = 48)
    {
        if (radius <= 0) return;
        Vector3D n = normal;
        if (n.Length < 1e-5) n = new Vector3D(0, 0, 1);
        n.Normalize();

        Vector3D temp = Math.Abs(n.X) > 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0);
        Vector3D u = Vector3D.CrossProduct(n, temp);
        u.Normalize();
        Vector3D v = Vector3D.CrossProduct(n, u);
        v.Normalize();

        Point3D prev = center + u * radius;
        for (int i = 1; i <= segments; i++)
        {
            double angle = i * 2.0 * Math.PI / segments;
            Point3D cur = center + u * (radius * Math.Cos(angle)) + v * (radius * Math.Sin(angle));
            AppendThickLine(geo, prev, cur, thickness);
            prev = cur;
        }
    }

    /// <summary>
    /// 在圆心处生成十字准星标（Center Mark）
    /// </summary>
    public static void AppendCenterMark(MeshGeometry3D geo, Point3D center, Vector3D normal, double markLen, double thickness)
    {
        if (markLen <= 0) return;
        Vector3D n = normal;
        if (n.Length < 1e-5) n = new Vector3D(0, 0, 1);
        n.Normalize();

        Vector3D temp = Math.Abs(n.X) > 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0);
        Vector3D u = Vector3D.CrossProduct(n, temp);
        u.Normalize();
        Vector3D v = Vector3D.CrossProduct(n, u);
        v.Normalize();

        AppendThickLine(geo, center - u * markLen, center + u * markLen, thickness);
        AppendThickLine(geo, center - v * markLen, center + v * markLen, thickness);
    }

    /// <summary>
    /// 生成包含 12 条棱边的 3D 模型外形包围盒线框
    /// </summary>
    public static MeshGeometry3D CreateBoundingBoxWireframe(Point3D min, Point3D max, double thickness)
    {
        var geo = new MeshGeometry3D();

        var p000 = new Point3D(min.X, min.Y, min.Z);
        var p001 = new Point3D(min.X, min.Y, max.Z);
        var p010 = new Point3D(min.X, max.Y, min.Z);
        var p011 = new Point3D(min.X, max.Y, max.Z);
        var p100 = new Point3D(max.X, min.Y, min.Z);
        var p101 = new Point3D(max.X, min.Y, max.Z);
        var p110 = new Point3D(max.X, max.Y, min.Z);
        var p111 = new Point3D(max.X, max.Y, max.Z);

        AppendThickLine(geo, p000, p100, thickness);
        AppendThickLine(geo, p001, p101, thickness);
        AppendThickLine(geo, p010, p110, thickness);
        AppendThickLine(geo, p011, p111, thickness);

        AppendThickLine(geo, p000, p010, thickness);
        AppendThickLine(geo, p100, p110, thickness);
        AppendThickLine(geo, p001, p011, thickness);
        AppendThickLine(geo, p101, p111, thickness);

        AppendThickLine(geo, p000, p001, thickness);
        AppendThickLine(geo, p100, p101, thickness);
        AppendThickLine(geo, p010, p011, thickness);
        AppendThickLine(geo, p110, p111, thickness);

        geo.Freeze();
        return geo;
    }

    /// <summary>
    /// 空间 3D 点集拟合空间圆（Kåsa 最小二乘代数法 + Newell 平面法向量）
    /// </summary>
    public static bool Fit3DCircle(IReadOnlyList<Point3D> points, out Point3D center, out Vector3D normal, out double radius, out double relError)
    {
        center = new Point3D();
        normal = new Vector3D(0, 0, 1);
        radius = 0;
        relError = 1.0;

        int n = points.Count;
        if (n < 5) return false;

        // 1. Newell 方法提取多边形最佳平面法向量
        double nx = 0, ny = 0, nz = 0;
        double sumX = 0, sumY = 0, sumZ = 0;
        for (int i = 0; i < n; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % n];
            nx += (p1.Y - p2.Y) * (p1.Z + p2.Z);
            ny += (p1.Z - p2.Z) * (p1.X + p2.X);
            nz += (p1.X - p2.X) * (p1.Y + p2.Y);
            sumX += p1.X; sumY += p1.Y; sumZ += p1.Z;
        }

        normal = new Vector3D(nx, ny, nz);
        if (normal.Length < 1e-6) return false;
        normal.Normalize();

        Point3D centroid = new Point3D(sumX / n, sumY / n, sumZ / n);

        // 2. 建立正交基向量 (u, v)
        Vector3D temp = Math.Abs(normal.X) > 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0);
        Vector3D u = Vector3D.CrossProduct(normal, temp);
        u.Normalize();
        Vector3D v = Vector3D.CrossProduct(normal, u);
        v.Normalize();

        // 3. 投影到 2D 平面并构建 3x3 矩阵方程
        double s_u = 0, s_v = 0, s_uu = 0, s_vv = 0, s_uv = 0;
        double s_uz = 0, s_vz = 0, s_z = 0;

        for (int i = 0; i < n; i++)
        {
            Vector3D diff = points[i] - centroid;
            double ui = Vector3D.DotProduct(diff, u);
            double vi = Vector3D.DotProduct(diff, v);
            double zi = ui * ui + vi * vi;

            s_u += ui; s_v += vi;
            s_uu += ui * ui; s_vv += vi * vi; s_uv += ui * vi;
            s_uz += ui * zi; s_vz += vi * zi; s_z += zi;
        }

        double a11 = 2 * s_uu, a12 = 2 * s_uv, a13 = s_u;
        double a21 = 2 * s_uv, a22 = 2 * s_vv, a23 = s_v;
        double a31 = 2 * s_u,  a32 = 2 * s_v,  a33 = n;

        double det = a11 * (a22 * a33 - a23 * a32) -
                     a12 * (a21 * a33 - a23 * a31) +
                     a13 * (a21 * a32 - a22 * a31);

        if (Math.Abs(det) < 1e-9) return false;

        double b1 = s_uz, b2 = s_vz, b3 = s_z;

        double uc = (b1 * (a22 * a33 - a23 * a32) - a12 * (b2 * a33 - a23 * b3) + a13 * (b2 * a32 - a22 * b3)) / det;
        double vc = (a11 * (b2 * a33 - a23 * b3) - b1 * (a21 * a33 - a23 * a31) + a13 * (a21 * b3 - b2 * a31)) / det;
        double c  = (a11 * (a22 * b3 - b2 * a32) - a12 * (a21 * b3 - b2 * a31) + b1 * (a21 * a32 - a22 * a31)) / det;

        double rSq = c + uc * uc + vc * vc;
        if (rSq <= 1e-4) return false;

        radius = Math.Sqrt(rSq);
        center = centroid + u * uc + v * vc;

        // 4. 计算标准差与极差（Max-Min Deviation）
        double sumSqErr = 0;
        double minD = double.MaxValue;
        double maxD = double.MinValue;
        for (int i = 0; i < n; i++)
        {
            double dist = (points[i] - center).Length;
            double err = dist - radius;
            sumSqErr += err * err;
            if (dist < minD) minD = dist;
            if (dist > maxD) maxD = dist;
        }

        double stdDev = Math.Sqrt(sumSqErr / n);
        relError = radius > 1e-4 ? stdDev / radius : 1.0;
        double rangeError = radius > 1e-4 ? (maxD - minD) / radius : 1.0;

        // 严格空间圆校验：相对标准差 < 3.0%，极差 < 5.5% (矩形极差高达 30%~80%，坚决剔除)
        return relError < 0.030 && rangeError < 0.055 && radius >= 0.1;
    }

    /// <summary>
    /// 从网格的轮廓边（OutlineEdges）沿着闭环自动追踪并拟合圆孔边缘
    /// </summary>
    public static Models.MeasureElement? DetectCircleFromOutlineEdges(
        SldFile.DisplayMesh mesh,
        int hitVertexIndex,
        Func<Point3D, Point3D> toAbsCoord)
    {
        if (mesh?.OutlineEdges == null || mesh.OutlineEdges.Count == 0 || mesh.Positions == null) return null;
        if (hitVertexIndex < 0 || hitVertexIndex >= mesh.Positions.Count) return null;

        // 快速寻找与命中点重合或相连的轮廓边顶点
        var hitPos = mesh.Positions[hitVertexIndex];
        int startV = -1;

        // 构建邻接表
        var adj = new Dictionary<int, List<int>>();
        foreach (var (s, e) in mesh.OutlineEdges)
        {
            if (!adj.TryGetValue(s, out var l1)) { l1 = new List<int>(); adj[s] = l1; }
            if (!l1.Contains(e)) l1.Add(e);

            if (!adj.TryGetValue(e, out var l2)) { l2 = new List<int>(); adj[e] = l2; }
            if (!l2.Contains(s)) l2.Add(s);
        }

        if (adj.ContainsKey(hitVertexIndex))
        {
            startV = hitVertexIndex;
        }
        else
        {
            // 在容差 0.5mm 范围内寻找重合的轮廓边顶点
            float bestDistSq = 0.5f * 0.5f;
            foreach (var kvp in adj)
            {
                int vi = kvp.Key;
                if (vi < mesh.Positions.Count)
                {
                    float dSq = System.Numerics.Vector3.DistanceSquared(hitPos, mesh.Positions[vi]);
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        startV = vi;
                    }
                }
            }
        }

        if (startV == -1 || !adj.ContainsKey(startV)) return null;

        // 沿环追踪闭环多边形回路
        var visited = new HashSet<int>();
        var loop = new List<int> { startV };
        int current = startV;
        int prev = -1;

        while (loop.Count < 256)
        {
            visited.Add(current);
            if (!adj.TryGetValue(current, out var neighbors)) break;

            int next = -1;
            foreach (var nb in neighbors)
            {
                if (nb != prev && (nb == startV || !visited.Contains(nb)))
                {
                    next = nb;
                    break;
                }
            }

            if (next == -1) break;
            if (next == startV)
            {
                // 找到闭合环回路！
                if (loop.Count >= 6)
                {
                    var resultPts = new List<Point3D>(loop.Count);
                    foreach (var idx in loop)
                    {
                        var vec = mesh.Positions[idx];
                        resultPts.Add(toAbsCoord(new Point3D(vec.X, vec.Y, vec.Z)));
                    }

                    // 1. 折角检验：圆环回路中各顶点相邻转向角必须均匀平滑，绝不能存在矩形或尖锐直角 (转向角 > 45度)
                    bool hasSharpCorner = false;
                    int pCnt = resultPts.Count;
                    for (int i = 0; i < pCnt; i++)
                    {
                        var pPrev = resultPts[(i - 1 + pCnt) % pCnt];
                        var pCur = resultPts[i];
                        var pNext = resultPts[(i + 1) % pCnt];

                        var d1 = pCur - pPrev;
                        var d2 = pNext - pCur;
                        if (d1.Length > 1e-5 && d2.Length > 1e-5)
                        {
                            d1.Normalize();
                            d2.Normalize();
                            // cos(45°) ≈ 0.707。若 d1 · d2 < 0.70 说明折角大于 45° (如长方形 90° 直角点积为 0)
                            if (Vector3D.DotProduct(d1, d2) < 0.70)
                            {
                                hasSharpCorner = true;
                                break;
                            }
                        }
                    }

                    if (!hasSharpCorner &&
                        Fit3DCircle(resultPts, out Point3D circleCenter, out Vector3D normal, out double radius, out double relErr) &&
                        relErr <= 0.030 && radius >= 0.2)
                    {
                        return new Models.MeasureElement(circleCenter, normal, radius, resultPts);
                    }
                }
                break;
            }

            loop.Add(next);
            prev = current;
            current = next;
        }

        return null;
    }

    /// <summary>
    /// 从命中的曲面 Part 拟合圆柱面（Cylinder Surface）
    /// </summary>
    public static Models.MeasureElement? DetectCylinderFromPart(
        SldFile.DisplayMesh mesh,
        int partIndex,
        Func<Point3D, Point3D> toAbsCoord)
    {
        if (mesh?.Parts == null || partIndex < 0 || partIndex >= mesh.Parts.Count) return null;
        var part = mesh.Parts[partIndex];
        if (part.TriangleCount < 4 || part.VertexCount < 6) return null;

        int tStart = part.TriangleStart * 3;
        int tEnd = tStart + part.TriangleCount * 3;
        if (tEnd > mesh.TriangleIndices.Count) return null;

        var faceNormals = new List<Vector3D>();
        double totalArea = 0;
        Point3D weightedCenter = new Point3D();

        for (int t = tStart; t < tEnd; t += 3)
        {
            int i0 = mesh.TriangleIndices[t];
            int i1 = mesh.TriangleIndices[t + 1];
            int i2 = mesh.TriangleIndices[t + 2];
            if (i0 >= mesh.Positions.Count || i1 >= mesh.Positions.Count || i2 >= mesh.Positions.Count) continue;

            var p0 = mesh.Positions[i0];
            var p1 = mesh.Positions[i1];
            var p2 = mesh.Positions[i2];

            var cr = System.Numerics.Vector3.Cross(p1 - p0, p2 - p0);
            float len = cr.Length();
            if (len < 1e-6f) continue;

            double area = len * 0.5;
            totalArea += area;

            var norm = new Vector3D(cr.X / len, cr.Y / len, cr.Z / len);
            faceNormals.Add(norm);

            Point3D triCenter = new Point3D((p0.X + p1.X + p2.X) / 3.0, (p0.Y + p1.Y + p2.Y) / 3.0, (p0.Z + p1.Z + p2.Z) / 3.0);
            weightedCenter = new Point3D(weightedCenter.X + triCenter.X * area,
                                        weightedCenter.Y + triCenter.Y * area,
                                        weightedCenter.Z + triCenter.Z * area);
        }

        if (faceNormals.Count < 4 || totalArea < 1e-4) return null;
        weightedCenter = new Point3D(weightedCenter.X / totalArea, weightedCenter.Y / totalArea, weightedCenter.Z / totalArea);

        // 如果面法向几乎平行，说明是平面不是圆柱面
        var n0 = faceNormals[0];
        bool allParallel = true;
        foreach (var fn in faceNormals)
        {
            if (Math.Abs(Vector3D.DotProduct(n0, fn)) < 0.95)
            {
                allParallel = false;
                break;
            }
        }
        if (allParallel) return null;

        // 提取圆柱中心轴线向量
        Vector3D axis = new Vector3D(0, 0, 0);
        int pairCount = 0;
        for (int i = 0; i < faceNormals.Count - 1 && pairCount < 20; i++)
        {
            for (int j = i + 1; j < faceNormals.Count && pairCount < 20; j++)
            {
                var cross = Vector3D.CrossProduct(faceNormals[i], faceNormals[j]);
                if (cross.Length > 0.15)
                {
                    cross.Normalize();
                    if (pairCount == 0 || Vector3D.DotProduct(axis, cross) >= 0)
                        axis += cross;
                    else
                        axis -= cross;
                    pairCount++;
                }
            }
        }

        if (axis.Length < 1e-4) return null;
        axis.Normalize();

        // 收集所有 Part 顶点并计算到轴线的垂直半径
        int vStart = part.VertexStart;
        int vEnd = vStart + part.VertexCount;
        if (vEnd > mesh.Positions.Count) return null;

        double sumRadius = 0;
        int ptCount = 0;
        var partPoints = new List<Point3D>();

        for (int v = vStart; v < vEnd; v++)
        {
            var p = mesh.Positions[v];
            Point3D pt = new Point3D(p.X, p.Y, p.Z);
            partPoints.Add(pt);

            // 垂直距离
            Vector3D diff = pt - weightedCenter;
            double projOnAxis = Vector3D.DotProduct(diff, axis);
            double distToAxis = (diff - axis * projOnAxis).Length;
            sumRadius += distToAxis;
            ptCount++;
        }

        if (ptCount < 6) return null;
        double avgRadius = sumRadius / ptCount;
        if (avgRadius < 0.1) return null;

        double sumSqErr = 0;
        for (int v = 0; v < ptCount; v++)
        {
            Vector3D diff = partPoints[v] - weightedCenter;
            double projOnAxis = Vector3D.DotProduct(diff, axis);
            double distToAxis = (diff - axis * projOnAxis).Length;
            double err = distToAxis - avgRadius;
            sumSqErr += err * err;
        }

        double stdDev = Math.Sqrt(sumSqErr / ptCount);
        double relErr = stdDev / avgRadius;

        if (relErr > 0.05) return null;

        // 收集绝对坐标三角形用于高亮
        var absVerts = new List<Point3D>();
        for (int t = tStart; t < tEnd; t += 3)
        {
            int i0 = mesh.TriangleIndices[t];
            int i1 = mesh.TriangleIndices[t + 1];
            int i2 = mesh.TriangleIndices[t + 2];
            absVerts.Add(toAbsCoord(new Point3D(mesh.Positions[i0].X, mesh.Positions[i0].Y, mesh.Positions[i0].Z)));
            absVerts.Add(toAbsCoord(new Point3D(mesh.Positions[i1].X, mesh.Positions[i1].Y, mesh.Positions[i1].Z)));
            absVerts.Add(toAbsCoord(new Point3D(mesh.Positions[i2].X, mesh.Positions[i2].Y, mesh.Positions[i2].Z)));
        }

        return new Models.MeasureElement(partIndex, toAbsCoord(weightedCenter), axis, avgRadius, totalArea, absVerts);
    }

    /// <summary>
    /// 将 3D 点集沿已知轴向投影到截面 2D 平面，使用 Kåsa 最小二乘法拟合圆心和半径
    /// </summary>
    public static bool FitCircleOnAxisPlane(
        IReadOnlyList<Point3D> points,
        Vector3D axis,
        Point3D origin,
        out Point3D center,
        out double radius,
        out double relError)
    {
        center = origin;
        radius = 0;
        relError = 1.0;
        int n = points.Count;
        if (n < 4) return false;

        // 构造与 axis 正交的基 (u, v)
        Vector3D temp = Math.Abs(axis.X) > 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0);
        Vector3D u = Vector3D.CrossProduct(axis, temp);
        u.Normalize();
        Vector3D v = Vector3D.CrossProduct(axis, u);
        v.Normalize();

        double s_u = 0, s_v = 0, s_uu = 0, s_vv = 0, s_uv = 0;
        double s_uz = 0, s_vz = 0, s_z = 0;

        for (int i = 0; i < n; i++)
        {
            Vector3D diff = points[i] - origin;
            double ui = Vector3D.DotProduct(diff, u);
            double vi = Vector3D.DotProduct(diff, v);
            double zi = ui * ui + vi * vi;

            s_u += ui; s_v += vi;
            s_uu += ui * ui; s_vv += vi * vi; s_uv += ui * vi;
            s_uz += ui * zi; s_vz += vi * zi; s_z += zi;
        }

        double a11 = 2 * s_uu, a12 = 2 * s_uv, a13 = s_u;
        double a21 = 2 * s_uv, a22 = 2 * s_vv, a23 = s_v;
        double a31 = 2 * s_u,  a32 = 2 * s_v,  a33 = n;

        double det = a11 * (a22 * a33 - a23 * a32) -
                     a12 * (a21 * a33 - a23 * a31) +
                     a13 * (a21 * a32 - a22 * a31);

        if (Math.Abs(det) < 1e-9) return false;

        double b1 = s_uz, b2 = s_vz, b3 = s_z;

        double uc = (b1 * (a22 * a33 - a23 * a32) - a12 * (b2 * a33 - a23 * b3) + a13 * (b2 * a32 - a22 * b3)) / det;
        double vc = (a11 * (b2 * a33 - a23 * b3) - b1 * (a21 * a33 - a23 * a31) + a13 * (a21 * b3 - b2 * a31)) / det;
        double c  = (a11 * (a22 * b3 - b2 * a32) - a12 * (a21 * b3 - b2 * a31) + b1 * (a21 * a32 - a22 * a31)) / det;

        double rSq = c + uc * uc + vc * vc;
        if (rSq <= 1e-4) return false;

        radius = Math.Sqrt(rSq);
        center = origin + u * uc + v * vc;

        // 计算相对径向均方根误差
        double errSumSq = 0;
        for (int i = 0; i < n; i++)
        {
            Vector3D d = points[i] - center;
            double dProjAxis = Vector3D.DotProduct(d, axis);
            double dRadial = (d - axis * dProjAxis).Length;
            double diffR = dRadial - radius;
            errSumSq += diffR * diffR;
        }
        relError = Math.Sqrt(errSumSq / n) / radius;
        return true;
    }

    /// <summary>
    /// 从命中的网格三角形出发，通过拓扑连通扩散智能识别当前单个圆柱孔/圆柱销筒壁表面，
    /// 严禁跨越空气扩散到其他无关平面或零件
    /// </summary>
    public static Models.MeasureElement? DetectCylinderSurface(
        SldFile.DisplayMesh mesh,
        int i0, int i1, int i2,
        Func<Point3D, Point3D> toAbsCoord)
    {
        if (mesh == null || mesh.Positions == null || mesh.TriangleIndices == null) return null;
        if (i0 >= mesh.Positions.Count || i1 >= mesh.Positions.Count || i2 >= mesh.Positions.Count) return null;

        var p0 = mesh.Positions[i0];
        var p1 = mesh.Positions[i1];
        var p2 = mesh.Positions[i2];
        var cr = System.Numerics.Vector3.Cross(p1 - p0, p2 - p0);
        float crLen = cr.Length();
        if (crLen < 1e-6f) return null;
        var nHit = new Vector3D(cr.X / crLen, cr.Y / crLen, cr.Z / crLen);

        int totalTriangles = mesh.TriangleIndices.Count / 3;

        // 1. 构建命中区域局部邻接表 (仅建立命中点 160mm 范围内的局部邻接表，毫秒级快速索引)
        var hitCenter = new Point3D((p0.X + p1.X + p2.X) / 3.0, (p0.Y + p1.Y + p2.Y) / 3.0, (p0.Z + p1.Z + p2.Z) / 3.0);
        int hitTriIndex = -1;

        var vertToTri = new Dictionary<int, List<int>>();
        var triNormals = new Vector3D[totalTriangles];
        var triCenters = new Point3D[totalTriangles];
        var triValid = new bool[totalTriangles];

        for (int t = 0; t < totalTriangles; t++)
        {
            int t0 = mesh.TriangleIndices[t * 3];
            int t1 = mesh.TriangleIndices[t * 3 + 1];
            int t2 = mesh.TriangleIndices[t * 3 + 2];
            if (t0 >= mesh.Positions.Count || t1 >= mesh.Positions.Count || t2 >= mesh.Positions.Count) continue;

            if ((t0 == i0 && t1 == i1 && t2 == i2) ||
                (t0 == i1 && t1 == i2 && t2 == i0) ||
                (t0 == i2 && t1 == i0 && t2 == i1))
            {
                hitTriIndex = t;
            }

            var tp0 = mesh.Positions[t0];
            var tp1 = mesh.Positions[t1];
            var tp2 = mesh.Positions[t2];

            Point3D tc = new Point3D((tp0.X + tp1.X + tp2.X) / 3.0, (tp0.Y + tp1.Y + tp2.Y) / 3.0, (tp0.Z + tp1.Z + tp2.Z) / 3.0);
            if ((tc - hitCenter).Length > 180.0) continue;

            var tcr = System.Numerics.Vector3.Cross(tp1 - tp0, tp2 - tp0);
            float tlen = tcr.Length();
            if (tlen < 1e-6f) continue;

            triNormals[t] = new Vector3D(tcr.X / tlen, tcr.Y / tlen, tcr.Z / tlen);
            triCenters[t] = tc;
            triValid[t] = true;

            void AddV(int v)
            {
                if (!vertToTri.TryGetValue(v, out var list))
                {
                    list = new List<int>(6);
                    vertToTri[v] = list;
                }
                list.Add(t);
            }
            AddV(t0); AddV(t1); AddV(t2);
        }

        if (hitTriIndex == -1)
        {
            double bestD = double.MaxValue;
            for (int t = 0; t < totalTriangles; t++)
            {
                if (triValid[t])
                {
                    double d = (triCenters[t] - hitCenter).Length;
                    if (d < bestD) { bestD = d; hitTriIndex = t; }
                }
            }
        }
        if (hitTriIndex == -1 || !triValid[hitTriIndex]) return null;

        // 2. 检查与命中面共享边的邻近三角形，判定是否为曲面，并提取轴线方向
        // 若邻居全部共面 (dot >= 0.9995)，说明点击的是纯平面（如圆柱平底端面或滑块平面），直接返回 null！
        Vector3D axis = new Vector3D(0, 0, 0);
        bool foundAxis = false;

        int hi0 = mesh.TriangleIndices[hitTriIndex * 3];
        int hi1 = mesh.TriangleIndices[hitTriIndex * 3 + 1];
        int hi2 = mesh.TriangleIndices[hitTriIndex * 3 + 2];

        var neighbors = new List<int>();
        void CollectNeighbors(int v)
        {
            if (vertToTri.TryGetValue(v, out var list))
            {
                foreach (var t in list)
                {
                    if (t != hitTriIndex && !neighbors.Contains(t)) neighbors.Add(t);
                }
            }
        }
        CollectNeighbors(hi0); CollectNeighbors(hi1); CollectNeighbors(hi2);

        bool hasCurvature = false;
        foreach (int nt in neighbors)
        {
            if (!triValid[nt]) continue;
            double dot = Vector3D.DotProduct(nHit, triNormals[nt]);

            // 二面角在 1 度 ~ 40 度之间，确认为圆弧曲面
            if (dot > 0.76 && dot < 0.9998)
            {
                var cross = Vector3D.CrossProduct(nHit, triNormals[nt]);
                if (cross.Length > 0.02)
                {
                    cross.Normalize();
                    axis = cross;
                    foundAxis = true;
                    hasCurvature = true;
                    break;
                }
            }
        }

        // 若 1 环邻居太接近平行（如高密度网格），搜寻 2 环邻居提取曲率主轴
        var ring2 = new List<int>();
        if (!hasCurvature)
        {
            foreach (int nt in neighbors)
            {
                int nt0 = mesh.TriangleIndices[nt * 3];
                int nt1 = mesh.TriangleIndices[nt * 3 + 1];
                int nt2 = mesh.TriangleIndices[nt * 3 + 2];
                void CollectR2(int v)
                {
                    if (vertToTri.TryGetValue(v, out var list))
                    {
                        foreach (var t in list)
                        {
                            if (t != hitTriIndex && !neighbors.Contains(t) && !ring2.Contains(t)) ring2.Add(t);
                        }
                    }
                }
                CollectR2(nt0); CollectR2(nt1); CollectR2(nt2);
            }

            foreach (int nt in ring2)
            {
                if (!triValid[nt]) continue;
                double dot = Vector3D.DotProduct(nHit, triNormals[nt]);
                if (dot > 0.76 && dot < 0.9998)
                {
                    var cross = Vector3D.CrossProduct(nHit, triNormals[nt]);
                    if (cross.Length > 0.02)
                    {
                        cross.Normalize();
                        axis = cross;
                        foundAxis = true;
                        hasCurvature = true;
                        break;
                    }
                }
            }
        }

        if (!hasCurvature || !foundAxis) return null;
        axis.Normalize();

        // 3. 在命中点局部采集曲面样本点拟合截面圆
        var samplePts = new List<Point3D>
        {
            new Point3D(p0.X, p0.Y, p0.Z),
            new Point3D(p1.X, p1.Y, p1.Z),
            new Point3D(p2.X, p2.Y, p2.Z)
        };

        foreach (int nt in neighbors)
        {
            if (!triValid[nt]) continue;
            var tn = triNormals[nt];
            if (Math.Abs(Vector3D.DotProduct(tn, axis)) < 0.20)
            {
                int nt0 = mesh.TriangleIndices[nt * 3];
                int nt1 = mesh.TriangleIndices[nt * 3 + 1];
                int nt2 = mesh.TriangleIndices[nt * 3 + 2];
                var tp0 = mesh.Positions[nt0];
                var tp1 = mesh.Positions[nt1];
                var tp2 = mesh.Positions[nt2];
                samplePts.Add(new Point3D(tp0.X, tp0.Y, tp0.Z));
                samplePts.Add(new Point3D(tp1.X, tp1.Y, tp1.Z));
                samplePts.Add(new Point3D(tp2.X, tp2.Y, tp2.Z));
            }
        }

        if (samplePts.Count < 6) return null;

        if (!FitCircleOnAxisPlane(samplePts, axis, hitCenter, out Point3D cylCenterProj, out double radius, out double relErr))
        {
            return null;
        }

        // 严格圆拟合校验：相对径向误差必须在 5% 以内
        if (radius < 0.3 || radius > 3000.0 || relErr > 0.05) return null;

        // 4. 【核心拓扑连通 BFS 扩散】：仅收集属于当前圆柱单曲面的三角形！
        // 遇到端面、倒角或直角台阶分界线，扩散自动停在边缘，绝不跨越空气扩散到其他无关平面！
        var visited = new bool[totalTriangles];
        var queue = new Queue<int>();
        var cylTriangles = new List<int>();

        visited[hitTriIndex] = true;
        queue.Enqueue(hitTriIndex);

        double minAxisProj = double.MaxValue;
        double maxAxisProj = double.MinValue;
        double totalArea = 0;
        double hitAxisProj = Vector3D.DotProduct(hitCenter - cylCenterProj, axis);

        while (queue.Count > 0)
        {
            int curT = queue.Dequeue();
            cylTriangles.Add(curT);

            Point3D curC = triCenters[curT];
            double curProj = Vector3D.DotProduct(curC - cylCenterProj, axis);
            minAxisProj = Math.Min(minAxisProj, curProj);
            maxAxisProj = Math.Max(maxAxisProj, curProj);

            int ct0 = mesh.TriangleIndices[curT * 3];
            int ct1 = mesh.TriangleIndices[curT * 3 + 1];
            int ct2 = mesh.TriangleIndices[curT * 3 + 2];

            var cp0 = mesh.Positions[ct0];
            var cp1 = mesh.Positions[ct1];
            var cp2 = mesh.Positions[ct2];
            totalArea += 0.5 * System.Numerics.Vector3.Cross(cp1 - cp0, cp2 - cp0).Length();

            int[] cVerts = new[] { ct0, ct1, ct2 };
            foreach (int cv in cVerts)
            {
                if (!vertToTri.TryGetValue(cv, out var adjList)) continue;
                foreach (int nextT in adjList)
                {
                    if (visited[nextT] || !triValid[nextT]) continue;

                    // 1. 法向必须垂直于对称轴 (|tn · axis| < 0.20)
                    var nextN = triNormals[nextT];
                    if (Math.Abs(Vector3D.DotProduct(nextN, axis)) > 0.20) continue;

                    // 2. 与当前三角形法向必须平滑过渡（夹角 < 35度，dot > 0.80，防止跨越 90 度直角或 45 度倒角台阶）
                    if (Vector3D.DotProduct(triNormals[curT], nextN) < 0.80) continue;

                    // 3. 中心点到对称轴线的垂直距离必须在半径 +- 8% 以内
                    Point3D nextC = triCenters[nextT];
                    Vector3D diff = nextC - cylCenterProj;
                    double projAxis = Vector3D.DotProduct(diff, axis);

                    if (Math.Abs(projAxis - hitAxisProj) > 250.0) continue;

                    double distToAxis = (diff - axis * projAxis).Length;
                    if (Math.Abs(distToAxis - radius) / radius > 0.08) continue;

                    visited[nextT] = true;
                    queue.Enqueue(nextT);
                }
            }
        }

        if (cylTriangles.Count < 4) return null;

        // 5. 生成该圆柱单曲面的所有绝对坐标顶点
        var cylAbsVerts = new List<Point3D>(cylTriangles.Count * 3);
        foreach (int t in cylTriangles)
        {
            int t0 = mesh.TriangleIndices[t * 3];
            int t1 = mesh.TriangleIndices[t * 3 + 1];
            int t2 = mesh.TriangleIndices[t * 3 + 2];

            cylAbsVerts.Add(toAbsCoord(new Point3D(mesh.Positions[t0].X, mesh.Positions[t0].Y, mesh.Positions[t0].Z)));
            cylAbsVerts.Add(toAbsCoord(new Point3D(mesh.Positions[t1].X, mesh.Positions[t1].Y, mesh.Positions[t1].Z)));
            cylAbsVerts.Add(toAbsCoord(new Point3D(mesh.Positions[t2].X, mesh.Positions[t2].Y, mesh.Positions[t2].Z)));
        }

        double midAxisProj = (minAxisProj + maxAxisProj) * 0.5;
        Point3D cylinderCenter = cylCenterProj + axis * midAxisProj;

        return new Models.MeasureElement(0, toAbsCoord(cylinderCenter), axis, radius, totalArea, cylAbsVerts);
    }

    /// <summary>
    /// 生成高亮几何元素对应的 3D 网格（点、直线边、圆孔发光圆环与准星、圆柱面微外推面片、平面面片）
    /// </summary>
    public static MeshGeometry3D? CreateElementHighlightMesh(Models.MeasureElement? elem, double lineThickness, double sphereRadius)
    {
        if (elem == null) return null;
        var geo = new MeshGeometry3D();

        if (elem.Type == Models.MeasureElementType.Point)
        {
            AppendSphere(geo, elem.CenterPoint, sphereRadius);
        }
        else if (elem.Type == Models.MeasureElementType.Edge)
        {
            AppendThickLine(geo, elem.EdgeStart, elem.EdgeEnd, lineThickness);
            AppendSphere(geo, elem.EdgeStart, sphereRadius * 0.8);
            AppendSphere(geo, elem.EdgeEnd, sphereRadius * 0.8);
        }
        else if (elem.Type == Models.MeasureElementType.Circle)
        {
            // 高亮圆孔：统一采用 72 分段细腻立体圆环管（绝不粗苯锯齿，放大平滑圆润）
            AppendCircleRing(geo, elem.CenterPoint, elem.AxisDirection, elem.Radius, lineThickness * 0.9, 72);

            // 在圆心处绘制微型精巧的十字准星标（Center Mark）与中心定位小球
            double markLen = Math.Clamp(elem.Radius * 0.22, 1.0, 8.0);
            AppendCenterMark(geo, elem.CenterPoint, elem.AxisDirection, markLen, lineThickness * 0.75);
            AppendSphere(geo, elem.CenterPoint, sphereRadius * 0.5);
        }
        else if (elem.Type == Models.MeasureElementType.Cylinder)
        {
            // 圆柱面高亮：单面正向渲染 + 法向微外推（彻底消除双面半透明撕裂）
            if (elem.FaceVertices.Count >= 3)
            {
                double pushDist = Math.Clamp(sphereRadius * 0.08, 0.06, 0.35);
                for (int i = 0; i < elem.FaceVertices.Count - 2; i += 3)
                {
                    Point3D p0 = elem.FaceVertices[i];
                    Point3D p1 = elem.FaceVertices[i + 1];
                    Point3D p2 = elem.FaceVertices[i + 2];

                    Vector3D fn = Vector3D.CrossProduct(p1 - p0, p2 - p0);
                    if (fn.Length > 1e-6) fn.Normalize();
                    Vector3D offset = fn * pushDist;

                    int idx0 = geo.Positions.Count;
                    geo.Positions.Add(p0 + offset);
                    geo.Positions.Add(p1 + offset);
                    geo.Positions.Add(p2 + offset);

                    geo.Normals.Add(fn);
                    geo.Normals.Add(fn);
                    geo.Normals.Add(fn);

                    // 仅添加单面正向三角形！
                    geo.TriangleIndices.Add(idx0);
                    geo.TriangleIndices.Add(idx0 + 1);
                    geo.TriangleIndices.Add(idx0 + 2);
                }
            }

            // 在圆柱代表点处打出微型十字准星与定位小球
            double markLen = Math.Clamp(elem.Radius * 0.22, 1.0, 8.0);
            AppendCenterMark(geo, elem.CenterPoint, elem.AxisDirection, markLen, lineThickness * 0.75);
            AppendSphere(geo, elem.CenterPoint, sphereRadius * 0.55);
        }
        else if (elem.Type == Models.MeasureElementType.Face)
        {
            if (elem.FaceVertices.Count >= 3)
            {
                Vector3D normal = elem.FaceNormal;
                if (normal.Length < 1e-4) normal = new Vector3D(0, 0, 1);
                double pushDist = Math.Clamp(sphereRadius * 0.08, 0.06, 0.35);
                Vector3D offset = normal * pushDist;

                int vBase = geo.Positions.Count;
                for (int i = 0; i < elem.FaceVertices.Count; i++)
                {
                    Point3D v = elem.FaceVertices[i];
                    geo.Positions.Add(v + offset);
                    geo.Normals.Add(normal);
                }

                // 1. 面片单面正向渲染
                for (int i = 0; i < elem.FaceVertices.Count - 2; i += 3)
                {
                    geo.TriangleIndices.Add(vBase + i);
                    geo.TriangleIndices.Add(vBase + i + 1);
                    geo.TriangleIndices.Add(vBase + i + 2);
                }

                // 2. 提取外边界非共享边（Boundary Edges），生成清晰锐利的 CAD 工程高亮外框线！
                var edgeCounts = new Dictionary<(long, long), (Point3D A, Point3D B)>();
                for (int i = 0; i < elem.FaceVertices.Count - 2; i += 3)
                {
                    Point3D p0 = elem.FaceVertices[i];
                    Point3D p1 = elem.FaceVertices[i + 1];
                    Point3D p2 = elem.FaceVertices[i + 2];
                    void AddEdge(Point3D a, Point3D b)
                    {
                        long k1 = (long)(Math.Round(a.X, 2) * 1000) ^ ((long)(Math.Round(a.Y, 2) * 1000) << 20) ^ ((long)(Math.Round(a.Z, 2) * 1000) << 40);
                        long k2 = (long)(Math.Round(b.X, 2) * 1000) ^ ((long)(Math.Round(b.Y, 2) * 1000) << 20) ^ ((long)(Math.Round(b.Z, 2) * 1000) << 40);
                        var key = k1 < k2 ? (k1, k2) : (k2, k1);
                        if (edgeCounts.ContainsKey(key)) edgeCounts.Remove(key);
                        else edgeCounts[key] = (a + offset, b + offset);
                    }
                    AddEdge(p0, p1); AddEdge(p1, p2); AddEdge(p2, p0);
                }

                foreach (var edge in edgeCounts.Values)
                {
                    AppendThickLine(geo, edge.A, edge.B, lineThickness * 0.85);
                }
            }
        }

        if (geo.Positions.Count == 0) return null;
        geo.Freeze();
        return geo;
    }

    /// <summary>
    /// 生成单根立体尺寸引线
    /// </summary>
    public static MeshGeometry3D? CreateSingleLineMesh(Point3D p0, Point3D p1, double thickness)
    {
        if ((p1 - p0).Length < 1e-4) return null;
        var geo = new MeshGeometry3D();
        AppendThickLine(geo, p0, p1, thickness);
        geo.Freeze();
        return geo;
    }

    /// <summary>
    /// 生成带两端双向实心工程箭头（Arrowheads）与刻度线的 SolidWorks 原生标准尺寸线
    /// </summary>
    public static MeshGeometry3D? CreateDimensionLineMesh(Point3D p0, Point3D p1, double thickness, double arrowSize)
    {
        var dir = p1 - p0;
        double len = dir.Length;
        if (len < 1e-4) return null;

        var geo = new MeshGeometry3D();
        AppendThickLine(geo, p0, p1, thickness);

        // 箭头长度与半径自适应
        double arrowLen = Math.Min(len * 0.25, arrowSize * 2.5);
        double arrowRadius = Math.Max(thickness * 1.8, arrowSize * 0.7);

        Vector3D normDir = dir;
        normDir.Normalize();

        if (len >= arrowLen * 1.8)
        {
            // 起点端工程箭头 (指向 p0)
            Point3D base0 = p0 + normDir * arrowLen;
            AppendCone(geo, p0, base0, arrowRadius, 10);

            // 终点端工程箭头 (指向 p1)
            Point3D base1 = p1 - normDir * arrowLen;
            AppendCone(geo, p1, base1, arrowRadius, 10);
        }
        else
        {
            // 距离过短时采用刻度线
            Vector3D v = Math.Abs(dir.X) > 0.9 * len ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0);
            Vector3D u = Vector3D.CrossProduct(dir, v);
            u.Normalize();
            u *= arrowSize;

            AppendThickLine(geo, p0 - u, p0 + u, thickness * 1.2);
            AppendThickLine(geo, p1 - u, p1 + u, thickness * 1.2);
        }

        geo.Freeze();
        return geo;
    }
}
