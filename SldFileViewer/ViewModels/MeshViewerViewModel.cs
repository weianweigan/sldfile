using System;
using System.IO;
using System.Windows.Media.Media3D;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SldFile;
using SldFileViewer.Models;
using SldFileViewer.Services;

namespace SldFileViewer.ViewModels;

public partial class MeshViewerViewModel : ObservableObject
{
    private readonly ISwDocumentService _docService;
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private DisplayMesh? _mesh;

    [ObservableProperty]
    private int _vertexCount;

    [ObservableProperty]
    private int _triangleCount;

    [ObservableProperty]
    private int _edgeCount;

    [ObservableProperty]
    private bool _hasMesh;

    [ObservableProperty]
    private bool _hasParasolid;

    [ObservableProperty]
    private int _currentConfigIndex;

    [ObservableProperty]
    private string _dimensionsText = string.Empty;

    [ObservableProperty]
    private MeshGeometry3D? _wpfMesh;

    [ObservableProperty]
    private MeshGeometry3D? _wpfEdgeMesh;

    [ObservableProperty]
    private bool _showEdges = true;

    // ── 剖视图 (Cross-Section View) 状态与属性 ──

    [ObservableProperty]
    private bool _isSectionViewEnabled;

    [ObservableProperty]
    private SectionAxis _sectionAxis = SectionAxis.X;

    [ObservableProperty]
    private double _sectionOffsetPercent = 0.0;

    [ObservableProperty]
    private bool _isSectionInverted;

    [ObservableProperty]
    private string _sectionOffsetDisplay = "0.0 mm";

    [ObservableProperty]
    private MeshGeometry3D? _wpfSectionPlaneMesh;

    [ObservableProperty]
    private MeshGeometry3D? _wpfSectionPlaneBorderMesh;

    [ObservableProperty]
    private MeshGeometry3D? _wpfSectionCutMesh;

    [ObservableProperty]
    private MeshGeometry3D? _wpfSectionManipulatorMesh;

    public bool IsAxisX
    {
        get => SectionAxis == SectionAxis.X;
        set { if (value) SectionAxis = SectionAxis.X; }
    }

    public bool IsAxisY
    {
        get => SectionAxis == SectionAxis.Y;
        set { if (value) SectionAxis = SectionAxis.Y; }
    }

    public bool IsAxisZ
    {
        get => SectionAxis == SectionAxis.Z;
        set { if (value) SectionAxis = SectionAxis.Z; }
    }

    public System.Numerics.Vector3 GetCurrentSectionAxisDir()
    {
        return SectionAxis switch
        {
            SectionAxis.Y => new System.Numerics.Vector3(0, 1, 0),
            SectionAxis.Z => new System.Numerics.Vector3(0, 0, 1),
            _ => new System.Numerics.Vector3(1, 0, 0)
        };
    }

    public System.Numerics.Vector3 GetCurrentSectionArrowDir()
    {
        var axis = GetCurrentSectionAxisDir();
        return IsSectionInverted ? -axis : axis;
    }

    public double GetCurrentAxisHalfDim()
    {
        if (Mesh == null || Mesh.Positions.Count == 0) return 50.0;
        var size = Mesh.Max - Mesh.Min;
        return SectionAxis switch
        {
            SectionAxis.Y => Math.Max(size.Y * 0.5, 0.1),
            SectionAxis.Z => Math.Max(size.Z * 0.5, 0.1),
            _ => Math.Max(size.X * 0.5, 0.1)
        };
    }

    public MeshViewerViewModel(ISwDocumentService docService, IExportService exportService, IDialogService dialogService)
    {
        _docService = docService;
        _exportService = exportService;
        _dialogService = dialogService;
    }

    public void LoadMesh(int configIndex = 0)
    {
        CurrentConfigIndex = configIndex;
        var mesh = _docService.GetDisplayMesh();
        Mesh = mesh;
        HasMesh = mesh != null && mesh.VertexCount > 0;

        HasParasolid = _docService.CurrentFile != null &&
            (_docService.CurrentFile.ExtractParasolid(configIndex) != null);

        if (mesh != null)
        {
            VertexCount = mesh.VertexCount;
            TriangleCount = mesh.TriangleCount;
            EdgeCount = mesh.EdgeCount;

            var size = mesh.Max - mesh.Min;
            DimensionsText = $"{size.X:F1} x {size.Y:F1} x {size.Z:F1} mm";

            RebuildWpfMeshes();
        }
        else
        {
            VertexCount = 0;
            TriangleCount = 0;
            EdgeCount = 0;
            DimensionsText = "-";
            WpfMesh = null;
            WpfEdgeMesh = null;
        }
    }

    partial void OnCurrentConfigIndexChanged(int value)
    {
        HasParasolid = _docService.CurrentFile != null &&
            (_docService.CurrentFile.ExtractParasolid(value) != null);
    }

    partial void OnIsSectionViewEnabledChanged(bool value) => RebuildWpfMeshes();
    partial void OnSectionAxisChanged(SectionAxis value)
    {
        OnPropertyChanged(nameof(IsAxisX));
        OnPropertyChanged(nameof(IsAxisY));
        OnPropertyChanged(nameof(IsAxisZ));
        RebuildWpfMeshes();
    }
    partial void OnSectionOffsetPercentChanged(double value) => RebuildWpfMeshes();
    partial void OnIsSectionInvertedChanged(bool value) => RebuildWpfMeshes();

    [RelayCommand]
    private void SetSectionAxis(SectionAxis axis)
    {
        SectionAxis = axis;
    }

    [RelayCommand]
    private void ToggleSectionInvert()
    {
        IsSectionInverted = !IsSectionInverted;
    }

    [RelayCommand]
    private void ResetSectionOffset()
    {
        SectionOffsetPercent = 0.0;
    }

    private void RebuildWpfMeshes()
    {
        if (Mesh == null || Mesh.Positions.Count == 0)
        {
            WpfMesh = null;
            WpfEdgeMesh = null;
            return;
        }

        var center = (Mesh.Min + Mesh.Max) * 0.5f;

        if (!IsSectionViewEnabled)
        {
            BuildWpfMesh(Mesh, center);
            BuildEdgeMesh(Mesh, center);
            WpfSectionPlaneMesh = null;
            WpfSectionPlaneBorderMesh = null;
            WpfSectionCutMesh = null;
            WpfSectionManipulatorMesh = null;
            return;
        }

        // 计算剖切平面轴向与距离
        var size = Mesh.Max - Mesh.Min;
        var axisDir = GetCurrentSectionAxisDir();
        double halfDim = GetCurrentAxisHalfDim();

        float offset = (float)(halfDim * (SectionOffsetPercent / 100.0));
        SectionOffsetDisplay = $"{offset:F1} mm";

        // 裁剪面法向量与距离
        // 默认保留 P · axisDir <= offset 侧 (即切除 +axisDir 侧，箭头指向剖切推进侧)
        // 反向保留 P · axisDir >= offset 侧 (即切除 -axisDir 侧)
        System.Numerics.Vector3 clipNormal = IsSectionInverted ? axisDir : -axisDir;
        float clipDist = IsSectionInverted ? offset : -offset;

        BuildClippedWpfMesh(Mesh, center, clipNormal, clipDist);
        BuildClippedEdgeMesh(Mesh, center, clipNormal, clipDist);
        BuildSectionPlaneMeshes(size, axisDir, offset);
    }

    private void BuildWpfMesh(DisplayMesh mesh, System.Numerics.Vector3 center)
    {
        var geo = new MeshGeometry3D();

        for (int i = 0; i < mesh.Positions.Count; i++)
        {
            var p = mesh.Positions[i] - center;
            geo.Positions.Add(new Point3D(p.X, p.Y, p.Z));
        }

        if (mesh.Normals.Count == mesh.Positions.Count)
        {
            for (int i = 0; i < mesh.Normals.Count; i++)
            {
                var n = mesh.Normals[i];
                geo.Normals.Add(new Vector3D(n.X, n.Y, n.Z));
            }
        }

        int posCount = mesh.Positions.Count;
        for (int i = 0; i + 2 < mesh.TriangleIndices.Count; i += 3)
        {
            int i0 = mesh.TriangleIndices[i];
            int i1 = mesh.TriangleIndices[i + 1];
            int i2 = mesh.TriangleIndices[i + 2];
            if (i0 >= 0 && i0 < posCount &&
                i1 >= 0 && i1 < posCount &&
                i2 >= 0 && i2 < posCount)
            {
                geo.TriangleIndices.Add(i0);
                geo.TriangleIndices.Add(i1);
                geo.TriangleIndices.Add(i2);
            }
        }

        geo.Freeze();
        WpfMesh = geo;
    }

    private void BuildClippedWpfMesh(DisplayMesh mesh, System.Numerics.Vector3 center, System.Numerics.Vector3 normal, float planeDist)
    {
        var geo = new MeshGeometry3D();
        var posCount = mesh.Positions.Count;
        bool hasNormals = mesh.Normals.Count == posCount;

        var dists = new float[posCount];
        var centeredPos = new System.Numerics.Vector3[posCount];
        for (int i = 0; i < posCount; i++)
        {
            var p = mesh.Positions[i] - center;
            centeredPos[i] = p;
            dists[i] = System.Numerics.Vector3.Dot(p, normal) - planeDist;
        }

        var cutSegments = new List<(System.Numerics.Vector3, System.Numerics.Vector3)>();

        int AddVertex(System.Numerics.Vector3 p, System.Numerics.Vector3 n)
        {
            int idx = geo.Positions.Count;
            geo.Positions.Add(new Point3D(p.X, p.Y, p.Z));
            if (hasNormals)
            {
                geo.Normals.Add(new Vector3D(n.X, n.Y, n.Z));
            }
            return idx;
        }

        for (int i = 0; i + 2 < mesh.TriangleIndices.Count; i += 3)
        {
            int idxA = mesh.TriangleIndices[i];
            int idxB = mesh.TriangleIndices[i + 1];
            int idxC = mesh.TriangleIndices[i + 2];

            if (idxA < 0 || idxA >= posCount || idxB < 0 || idxB >= posCount || idxC < 0 || idxC >= posCount)
                continue;

            float dA = dists[idxA];
            float dB = dists[idxB];
            float dC = dists[idxC];

            bool inA = dA >= 0;
            bool inB = dB >= 0;
            bool inC = dC >= 0;

            int inCount = (inA ? 1 : 0) + (inB ? 1 : 0) + (inC ? 1 : 0);
            if (inCount == 3)
            {
                var pA = centeredPos[idxA];
                var pB = centeredPos[idxB];
                var pC = centeredPos[idxC];
                var nA = hasNormals ? mesh.Normals[idxA] : System.Numerics.Vector3.Zero;
                var nB = hasNormals ? mesh.Normals[idxB] : System.Numerics.Vector3.Zero;
                var nC = hasNormals ? mesh.Normals[idxC] : System.Numerics.Vector3.Zero;

                int vA = AddVertex(pA, nA);
                int vB = AddVertex(pB, nB);
                int vC = AddVertex(pC, nC);

                geo.TriangleIndices.Add(vA);
                geo.TriangleIndices.Add(vB);
                geo.TriangleIndices.Add(vC);
            }
            else if (inCount == 0)
            {
                continue;
            }
            else if (inCount == 1)
            {
                int insideIdx, out1Idx, out2Idx;
                if (inA) { insideIdx = idxA; out1Idx = idxB; out2Idx = idxC; }
                else if (inB) { insideIdx = idxB; out1Idx = idxC; out2Idx = idxA; }
                else { insideIdx = idxC; out1Idx = idxA; out2Idx = idxB; }

                float dIn = dists[insideIdx];
                float dOut1 = dists[out1Idx];
                float dOut2 = dists[out2Idx];

                float t1 = dIn / (dIn - dOut1);
                float t2 = dIn / (dIn - dOut2);

                var pIn = centeredPos[insideIdx];
                var pOut1 = centeredPos[out1Idx];
                var pOut2 = centeredPos[out2Idx];

                var pInt1 = pIn + (pOut1 - pIn) * t1;
                var pInt2 = pIn + (pOut2 - pIn) * t2;

                cutSegments.Add((pInt1, pInt2));

                var nIn = hasNormals ? mesh.Normals[insideIdx] : System.Numerics.Vector3.Zero;
                var nOut1 = hasNormals ? mesh.Normals[out1Idx] : System.Numerics.Vector3.Zero;
                var nOut2 = hasNormals ? mesh.Normals[out2Idx] : System.Numerics.Vector3.Zero;

                var nInt1 = hasNormals ? System.Numerics.Vector3.Normalize(nIn + (nOut1 - nIn) * t1) : System.Numerics.Vector3.Zero;
                var nInt2 = hasNormals ? System.Numerics.Vector3.Normalize(nIn + (nOut2 - nIn) * t2) : System.Numerics.Vector3.Zero;

                int v0 = AddVertex(pIn, nIn);
                int v1 = AddVertex(pInt1, nInt1);
                int v2 = AddVertex(pInt2, nInt2);

                geo.TriangleIndices.Add(v0);
                geo.TriangleIndices.Add(v1);
                geo.TriangleIndices.Add(v2);
            }
            else if (inCount == 2)
            {
                int in1Idx, in2Idx, outIdx;
                if (!inA) { outIdx = idxA; in1Idx = idxB; in2Idx = idxC; }
                else if (!inB) { outIdx = idxB; in1Idx = idxC; in2Idx = idxA; }
                else { outIdx = idxC; in1Idx = idxA; in2Idx = idxB; }

                float dIn1 = dists[in1Idx];
                float dIn2 = dists[in2Idx];
                float dOut = dists[outIdx];

                float t1 = dIn1 / (dIn1 - dOut);
                float t2 = dIn2 / (dIn2 - dOut);

                var pIn1 = centeredPos[in1Idx];
                var pIn2 = centeredPos[in2Idx];
                var pOut = centeredPos[outIdx];

                var pInt1 = pIn1 + (pOut - pIn1) * t1;
                var pInt2 = pIn2 + (pOut - pIn2) * t2;

                cutSegments.Add((pInt1, pInt2));

                var nIn1 = hasNormals ? mesh.Normals[in1Idx] : System.Numerics.Vector3.Zero;
                var nIn2 = hasNormals ? mesh.Normals[in2Idx] : System.Numerics.Vector3.Zero;
                var nOut = hasNormals ? mesh.Normals[outIdx] : System.Numerics.Vector3.Zero;

                var nInt1 = hasNormals ? System.Numerics.Vector3.Normalize(nIn1 + (nOut - nIn1) * t1) : System.Numerics.Vector3.Zero;
                var nInt2 = hasNormals ? System.Numerics.Vector3.Normalize(nIn2 + (nOut - nIn2) * t2) : System.Numerics.Vector3.Zero;

                int vIn1 = AddVertex(pIn1, nIn1);
                int vIn2 = AddVertex(pIn2, nIn2);
                int vInt1 = AddVertex(pInt1, nInt1);
                int vInt2 = AddVertex(pInt2, nInt2);

                geo.TriangleIndices.Add(vIn1);
                geo.TriangleIndices.Add(vIn2);
                geo.TriangleIndices.Add(vInt1);

                geo.TriangleIndices.Add(vIn2);
                geo.TriangleIndices.Add(vInt2);
                geo.TriangleIndices.Add(vInt1);
            }
        }

        geo.Freeze();
        WpfMesh = geo;

        // 生成截面边缘高亮线 (Section Cut Outlines)
        if (cutSegments.Count > 0)
        {
            var cutGeo = new MeshGeometry3D();
            int cutVBase = 0;
            var size = mesh.Max - mesh.Min;
            float maxDim = Math.Max(size.X, Math.Max(size.Y, size.Z));
            float cutThickness = Math.Max(maxDim * 0.0035f, 0.25f);
            foreach (var (cp0, cp1) in cutSegments)
            {
                AppendThickEdge(cutGeo, cp0, cp1, cutThickness, ref cutVBase);
            }
            cutGeo.Freeze();
            WpfSectionCutMesh = cutGeo;
        }
        else
        {
            WpfSectionCutMesh = null;
        }
    }

    private void BuildSectionPlaneMeshes(System.Numerics.Vector3 size, System.Numerics.Vector3 axisDir, float offset)
    {
        float maxDim = Math.Max(size.X, Math.Max(size.Y, size.Z));
        if (maxDim <= 0.001f) maxDim = 100.0f;

        System.Numerics.Vector3 uDir;
        System.Numerics.Vector3 vDir;
        float w;
        float h;

        switch (SectionAxis)
        {
            case SectionAxis.Y:
                uDir = new System.Numerics.Vector3(1, 0, 0);
                vDir = new System.Numerics.Vector3(0, 0, 1);
                w = Math.Max(size.X * 1.35f, maxDim * 0.6f);
                h = Math.Max(size.Z * 1.35f, maxDim * 0.6f);
                break;
            case SectionAxis.Z:
                uDir = new System.Numerics.Vector3(1, 0, 0);
                vDir = new System.Numerics.Vector3(0, 1, 0);
                w = Math.Max(size.X * 1.35f, maxDim * 0.6f);
                h = Math.Max(size.Y * 1.35f, maxDim * 0.6f);
                break;
            case SectionAxis.X:
            default:
                uDir = new System.Numerics.Vector3(0, 1, 0);
                vDir = new System.Numerics.Vector3(0, 0, 1);
                w = Math.Max(size.Y * 1.35f, maxDim * 0.6f);
                h = Math.Max(size.Z * 1.35f, maxDim * 0.6f);
                break;
        }

        // 剖切平面在 3D 空间的客观中心位置
        var planeCenter = axisDir * offset;
        var halfU = uDir * (w * 0.5f);
        var halfV = vDir * (h * 0.5f);

        var c0 = planeCenter - halfU - halfV;
        var c1 = planeCenter + halfU - halfV;
        var c2 = planeCenter + halfU + halfV;
        var c3 = planeCenter - halfU + halfV;

        // 1. 半透明剖切平面网格 (单面2个三角形，由 WPF BackMaterial 实现无重叠双面半透明渲染)
        var planeGeo = new MeshGeometry3D();
        planeGeo.Positions.Add(new Point3D(c0.X, c0.Y, c0.Z));
        planeGeo.Positions.Add(new Point3D(c1.X, c1.Y, c1.Z));
        planeGeo.Positions.Add(new Point3D(c2.X, c2.Y, c2.Z));
        planeGeo.Positions.Add(new Point3D(c3.X, c3.Y, c3.Z));

        var n3D = new Vector3D(axisDir.X, axisDir.Y, axisDir.Z);
        planeGeo.Normals.Add(n3D);
        planeGeo.Normals.Add(n3D);
        planeGeo.Normals.Add(n3D);
        planeGeo.Normals.Add(n3D);

        planeGeo.TriangleIndices.Add(0);
        planeGeo.TriangleIndices.Add(1);
        planeGeo.TriangleIndices.Add(2);
        planeGeo.TriangleIndices.Add(0);
        planeGeo.TriangleIndices.Add(2);
        planeGeo.TriangleIndices.Add(3);

        planeGeo.Freeze();
        WpfSectionPlaneMesh = planeGeo;

        // 2. 剖切平面外边框网格 (亮蓝色线框)
        var borderGeo = new MeshGeometry3D();
        int borderVBase = 0;
        float borderThickness = Math.Max(maxDim * 0.0035f, 0.2f);
        AppendThickEdge(borderGeo, c0, c1, borderThickness, ref borderVBase);
        AppendThickEdge(borderGeo, c1, c2, borderThickness, ref borderVBase);
        AppendThickEdge(borderGeo, c2, c3, borderThickness, ref borderVBase);
        AppendThickEdge(borderGeo, c3, c0, borderThickness, ref borderVBase);
        borderGeo.Freeze();
        WpfSectionPlaneBorderMesh = borderGeo;

        // 3. 中心操纵手柄网格 (箭头方向指示剖切操作方向，反向时自动掉头)
        var manipGeo = new MeshGeometry3D();
        int manipVBase = 0;

        var arrowDir = IsSectionInverted ? -axisDir : axisDir;
        var arrow3D = new Vector3D(arrowDir.X, arrowDir.Y, arrowDir.Z);

        float handleRadius = Math.Max(maxDim * 0.045f, 1.8f);
        float stemLength = Math.Max(maxDim * 0.12f, 4.0f);
        float arrowLength = Math.Max(maxDim * 0.06f, 2.5f);
        float arrowRadius = handleRadius * 0.55f;

        int segments = 12;

        // 箭头主轴杆
        var shaftEnd = planeCenter + arrowDir * stemLength;
        AppendThickEdge(manipGeo, planeCenter, shaftEnd, borderThickness * 1.5f, ref manipVBase);

        // 箭头圆锥体
        var arrowTip = shaftEnd + arrowDir * arrowLength;
        int tipIdx = manipGeo.Positions.Count;
        manipGeo.Positions.Add(new Point3D(arrowTip.X, arrowTip.Y, arrowTip.Z));
        manipGeo.Normals.Add(arrow3D);

        int baseCenterIdx = manipGeo.Positions.Count;
        manipGeo.Positions.Add(new Point3D(shaftEnd.X, shaftEnd.Y, shaftEnd.Z));
        manipGeo.Normals.Add(-arrow3D);

        int coneBaseStart = manipGeo.Positions.Count;
        for (int i = 0; i < segments; i++)
        {
            double angle = 2.0 * Math.PI * i / segments;
            var coneP = shaftEnd + (uDir * (float)Math.Cos(angle) + vDir * (float)Math.Sin(angle)) * arrowRadius;
            manipGeo.Positions.Add(new Point3D(coneP.X, coneP.Y, coneP.Z));
            manipGeo.Normals.Add(arrow3D);
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            // 锥侧面
            manipGeo.TriangleIndices.Add(tipIdx);
            manipGeo.TriangleIndices.Add(coneBaseStart + i);
            manipGeo.TriangleIndices.Add(coneBaseStart + next);

            // 锥底面
            manipGeo.TriangleIndices.Add(baseCenterIdx);
            manipGeo.TriangleIndices.Add(coneBaseStart + next);
            manipGeo.TriangleIndices.Add(coneBaseStart + i);
        }

        manipGeo.Freeze();
        WpfSectionManipulatorMesh = manipGeo;
    }
    private void BuildEdgeMesh(DisplayMesh mesh, System.Numerics.Vector3 center)
    {
        if (mesh.OutlineEdges == null || mesh.OutlineEdges.Count == 0 || mesh.Positions.Count == 0)
        {
            WpfEdgeMesh = null;
            return;
        }

        var edgeGeo = new MeshGeometry3D();
        var size = mesh.Max - mesh.Min;
        double maxDim = Math.Max(size.X, Math.Max(size.Y, size.Z));
        float thickness = (float)Math.Clamp(maxDim * 0.0012, 0.05, 5.0);

        int posCount = mesh.Positions.Count;
        int vertexBase = 0;

        foreach (var (start, end) in mesh.OutlineEdges)
        {
            if (start < 0 || start >= posCount || end < 0 || end >= posCount || start == end)
                continue;

            var p0 = mesh.Positions[start] - center;
            var p1 = mesh.Positions[end] - center;

            AppendThickEdge(edgeGeo, p0, p1, thickness, ref vertexBase);
        }

        edgeGeo.Freeze();
        WpfEdgeMesh = edgeGeo;
    }

    private void BuildClippedEdgeMesh(DisplayMesh mesh, System.Numerics.Vector3 center, System.Numerics.Vector3 normal, float planeDist)
    {
        if (mesh.OutlineEdges == null || mesh.OutlineEdges.Count == 0 || mesh.Positions.Count == 0)
        {
            WpfEdgeMesh = null;
            return;
        }

        var edgeGeo = new MeshGeometry3D();
        var size = mesh.Max - mesh.Min;
        double maxDim = Math.Max(size.X, Math.Max(size.Y, size.Z));
        float thickness = (float)Math.Clamp(maxDim * 0.0012, 0.05, 5.0);

        int posCount = mesh.Positions.Count;
        int vertexBase = 0;

        foreach (var (start, end) in mesh.OutlineEdges)
        {
            if (start < 0 || start >= posCount || end < 0 || end >= posCount || start == end)
                continue;

            var p0 = mesh.Positions[start] - center;
            var p1 = mesh.Positions[end] - center;

            float d0 = System.Numerics.Vector3.Dot(p0, normal) - planeDist;
            float d1 = System.Numerics.Vector3.Dot(p1, normal) - planeDist;

            if (d0 < 0 && d1 < 0) continue; // 全部剔除

            if (d0 >= 0 && d1 >= 0)
            {
                // 全部保留
                AppendThickEdge(edgeGeo, p0, p1, thickness, ref vertexBase);
            }
            else
            {
                // 截交线
                float t = d0 / (d0 - d1);
                var pInt = p0 + (p1 - p0) * t;
                if (d0 >= 0)
                    AppendThickEdge(edgeGeo, p0, pInt, thickness, ref vertexBase);
                else
                    AppendThickEdge(edgeGeo, pInt, p1, thickness, ref vertexBase);
            }
        }

        edgeGeo.Freeze();
        WpfEdgeMesh = edgeGeo;
    }

    private static void AppendThickEdge(MeshGeometry3D edgeGeo, System.Numerics.Vector3 p0, System.Numerics.Vector3 p1, float thickness, ref int vertexBase)
    {
        var dir = p1 - p0;
        float len = dir.Length();
        if (len < 1e-4f) return;

        var v = Math.Abs(dir.X) > 0.9f * len ? new System.Numerics.Vector3(0, 1, 0) : new System.Numerics.Vector3(1, 0, 0);
        var u = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(dir, v)) * thickness;
        var w = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(dir, u)) * thickness;

        edgeGeo.Positions.Add(new Point3D(p0.X - u.X, p0.Y - u.Y, p0.Z - u.Z));
        edgeGeo.Positions.Add(new Point3D(p0.X + u.X, p0.Y + u.Y, p0.Z + u.Z));
        edgeGeo.Positions.Add(new Point3D(p1.X + u.X, p1.Y + u.Y, p1.Z + u.Z));
        edgeGeo.Positions.Add(new Point3D(p1.X - u.X, p1.Y - u.Y, p1.Z - u.Z));

        edgeGeo.Positions.Add(new Point3D(p0.X - w.X, p0.Y - w.Y, p0.Z - w.Z));
        edgeGeo.Positions.Add(new Point3D(p0.X + w.X, p0.Y + w.Y, p0.Z + w.Z));
        edgeGeo.Positions.Add(new Point3D(p1.X + w.X, p1.Y + w.Y, p1.Z + w.Z));
        edgeGeo.Positions.Add(new Point3D(p1.X - w.X, p1.Y - w.Y, p1.Z - w.Z));

        edgeGeo.TriangleIndices.Add(vertexBase);
        edgeGeo.TriangleIndices.Add(vertexBase + 1);
        edgeGeo.TriangleIndices.Add(vertexBase + 2);
        edgeGeo.TriangleIndices.Add(vertexBase);
        edgeGeo.TriangleIndices.Add(vertexBase + 2);
        edgeGeo.TriangleIndices.Add(vertexBase + 3);

        edgeGeo.TriangleIndices.Add(vertexBase);
        edgeGeo.TriangleIndices.Add(vertexBase + 2);
        edgeGeo.TriangleIndices.Add(vertexBase + 1);
        edgeGeo.TriangleIndices.Add(vertexBase);
        edgeGeo.TriangleIndices.Add(vertexBase + 3);
        edgeGeo.TriangleIndices.Add(vertexBase + 2);

        edgeGeo.TriangleIndices.Add(vertexBase + 4);
        edgeGeo.TriangleIndices.Add(vertexBase + 5);
        edgeGeo.TriangleIndices.Add(vertexBase + 6);
        edgeGeo.TriangleIndices.Add(vertexBase + 4);
        edgeGeo.TriangleIndices.Add(vertexBase + 6);
        edgeGeo.TriangleIndices.Add(vertexBase + 7);

        edgeGeo.TriangleIndices.Add(vertexBase + 4);
        edgeGeo.TriangleIndices.Add(vertexBase + 6);
        edgeGeo.TriangleIndices.Add(vertexBase + 5);
        edgeGeo.TriangleIndices.Add(vertexBase + 4);
        edgeGeo.TriangleIndices.Add(vertexBase + 7);
        edgeGeo.TriangleIndices.Add(vertexBase + 6);

        vertexBase += 8;
    }

    private string GetDefaultFileName(string extension)
    {
        var currentPath = _docService.CurrentFilePath;
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var baseName = Path.GetFileNameWithoutExtension(currentPath);
            if (!string.IsNullOrWhiteSpace(baseName))
            {
                return $"{baseName}{extension}";
            }
        }
        return $"Model{extension}";
    }

    [RelayCommand]
    private void ExportObj()
    {
        if (Mesh == null) return;
        if (_dialogService.ShowSaveFileDialog(GetDefaultFileName(".obj"), "Wavefront OBJ (*.obj)|*.obj", out var savePath))
        {
            try
            {
                _exportService.ExportMeshToObj(Mesh, savePath);
                _dialogService.ShowInformation("导出成功", $"3D 网格已导出至:\n{savePath}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出失败", ex.Message);
            }
        }
    }

    [RelayCommand]
    private void ExportStl()
    {
        if (Mesh == null) return;
        if (_dialogService.ShowSaveFileDialog(GetDefaultFileName(".stl"), "STL 几何模型 (*.stl)|*.stl", out var savePath))
        {
            try
            {
                _exportService.ExportMeshToStl(Mesh, savePath);
                _dialogService.ShowInformation("导出成功", $"STL 模型已导出至:\n{savePath}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出失败", ex.Message);
            }
        }
    }

    [RelayCommand]
    private void ExportGlb()
    {
        if (Mesh == null) return;
        if (_dialogService.ShowSaveFileDialog(GetDefaultFileName(".glb"), "glTF 2.0 二进制模型 (*.glb)|*.glb", out var savePath))
        {
            try
            {
                _exportService.ExportMeshToGlb(Mesh, savePath);
                _dialogService.ShowInformation("导出成功", $"glTF 2.0 (GLB) 模型已导出至:\n{savePath}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出失败", ex.Message);
            }
        }
    }

    [RelayCommand]
    private void ExportParasolid()
    {
        if (_docService.CurrentFile == null) return;
        if (_dialogService.ShowSaveFileDialog(GetDefaultFileName(".x_b"), "Parasolid Transmit 二进制 (*.x_b)|*.x_b", out var savePath))
        {
            try
            {
                _exportService.ExportParasolid(_docService.CurrentFile, savePath, CurrentConfigIndex);
                _dialogService.ShowInformation("导出成功", $"Parasolid 几何数据已导出至:\n{savePath}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出失败", ex.Message);
            }
        }
    }
}
