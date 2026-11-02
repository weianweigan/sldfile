using System.Collections.Generic;

using System;



using System.IO;



using System.Threading.Tasks;



using System.Windows.Media.Media3D;



using SldFileViewer.Models;



using SldFileViewer.Services;



using SldFileViewer.ViewModels;



using Xunit;







namespace SldFileViewerTests;







public class MeasureToolIntegrationTests



{



    private class MockDialogService : IDialogService



    {



        public bool ShowOpenFileDialog(string filter, out string selectedPath) { selectedPath = string.Empty; return false; }



        public bool ShowSaveFileDialog(string defaultName, string filter, out string selectedPath) { selectedPath = string.Empty; return false; }



        public void ShowError(string title, string message) { }



        public void ShowInformation(string title, string message) { }



    }







[Fact]



    public void MeshGeometryHelper_CreatesThickLineAndSphere()



    {



        var lineGeo = new MeshGeometry3D();



        MeshGeometryHelper.AppendThickLine(lineGeo, new Point3D(0, 0, 0), new Point3D(100, 0, 0), 2.0);



        Assert.True(lineGeo.Positions.Count >= 8);



        Assert.True(lineGeo.TriangleIndices.Count >= 24);







var sphereGeo = new MeshGeometry3D();



        MeshGeometryHelper.AppendSphere(sphereGeo, new Point3D(10, 20, 30), 5.0, 6, 8);



        Assert.NotEmpty(sphereGeo.Positions);



        Assert.NotEmpty(sphereGeo.TriangleIndices);







var boxGeo = MeshGeometryHelper.CreateBoundingBoxWireframe(new Point3D(-50, -50, -50), new Point3D(50, 50, 50), 1.0);



        Assert.NotNull(boxGeo);



        Assert.True(boxGeo.Positions.Count >= 96);



        Assert.True(boxGeo.TriangleIndices.Count >= 288);



    }







[Fact]



    public void MeasureResult_CalculatesCorrectEuclideanAndDeltaDistances()



    {



        var p1 = new Point3D(10.0, 20.0, 30.0);



        var p2 = new Point3D(13.0, 24.0, 42.0);



        var res = new MeasureResult(p1, p2);







Assert.Equal(3.0, res.DeltaX, 3);



        Assert.Equal(4.0, res.DeltaY, 3);



        Assert.Equal(12.0, res.DeltaZ, 3);



        // sqrt(3^2 + 4^2 + 12^2) = sqrt(9 + 16 + 144) = sqrt(169) = 13.0



        Assert.Equal(13.0, res.Distance, 3);



    }







[Fact]



    public async Task MeshViewer_MeasureWorkflow_PointToPointAndBoundingBox()



    {



        var docService = new SwDocumentService();



        var exportService = new ExportService();



        var dialogService = new MockDialogService();



        var meshVm = new MeshViewerViewModel(docService, exportService, dialogService);







string modelPath = ViewerIntegrationTests.ResolveModelPath("cube.SLDPRT");



        await docService.OpenAsync(modelPath);



        meshVm.LoadMesh(0);







Assert.True(meshVm.HasMesh);



        Assert.NotNull(meshVm.Mesh);







// 1. 切换包围盒显示



        Assert.False(meshVm.IsBoundingBoxVisible);



        Assert.Null(meshVm.WpfBoundingBoxMesh);



        meshVm.ToggleBoundingBoxCommand.Execute(null);



        Assert.True(meshVm.IsBoundingBoxVisible);



        Assert.NotNull(meshVm.WpfBoundingBoxMesh);



        meshVm.ToggleBoundingBoxCommand.Execute(null);



        Assert.False(meshVm.IsBoundingBoxVisible);



        Assert.Null(meshVm.WpfBoundingBoxMesh);







// 2. 启用测量模式



        meshVm.IsMeasureModeEnabled = true;



        Assert.Equal(0, meshVm.MeasureStep);



        Assert.False(meshVm.HasMeasureResult);







// 3. 点击起点 (例如位于中心化坐标 -5, -5, -5)



        var p1 = new Point3D(-5.0, -5.0, -5.0);



        meshVm.HandleModelPointClicked(p1);



        Assert.Equal(1, meshVm.MeasureStep);



        Assert.False(meshVm.HasMeasureResult);



        Assert.NotNull(meshVm.WpfMeasureMarkersMesh);



        Assert.Null(meshVm.WpfMeasureLineMesh);







// 4. 模拟鼠标悬停预览 (橡皮筋连线)



        var hoverPt = new Point3D(0.0, 0.0, 0.0);



        meshVm.HandleModelPointHover(hoverPt);



        Assert.NotNull(meshVm.WpfMeasureLineMesh);







// 5. 点击终点 (例如位于 5, 5, 5)



        var p2 = new Point3D(5.0, 5.0, 5.0);



        meshVm.HandleModelPointClicked(p2);



        Assert.Equal(2, meshVm.MeasureStep);



        Assert.True(meshVm.HasMeasureResult);



        Assert.NotNull(meshVm.WpfMeasureLineMesh);



        Assert.NotNull(meshVm.WpfMeasureMarkersMesh);







// 直线距离 sqrt(10^2 + 10^2 + 10^2) = 17.32 mm



        Assert.Contains("17.32", meshVm.MeasureDistanceText);



        Assert.Contains("10.00", meshVm.MeasureDeltaXText);



        Assert.Contains("10.00", meshVm.MeasureDeltaYText);



        Assert.Contains("10.00", meshVm.MeasureDeltaZText);







// 6. 清空测量



        meshVm.ClearMeasureCommand.Execute(null);



        Assert.Equal(0, meshVm.MeasureStep);



        Assert.False(meshVm.HasMeasureResult);



        Assert.Null(meshVm.WpfMeasureLineMesh);



        Assert.Null(meshVm.WpfMeasureMarkersMesh);







docService.Close();



    }











    [Fact]



    public void MeasureResult_EdgeAndFaceMeasurements_CalculateCorrectMetrics()



    {



        // 1. 单选边线：长度为 50.0 mm



        var edgeElem = new MeasureElement(new Point3D(0, 0, 0), new Point3D(50, 0, 0));



        var edgeRes = new MeasureResult(edgeElem);



        Assert.Equal(50.0, edgeRes.Value, 3);



        Assert.Contains("50.00", edgeRes.MainText);



        Assert.Equal(new Point3D(25, 0, 0), edgeRes.CalloutAnchorPoint);







        // 2. 单选平面：100 x 50 的矩形面 (由2个三角形构成)



        var faceVerts = new List<Point3D>



        {



            new Point3D(0, 0, 0), new Point3D(100, 0, 0), new Point3D(100, 50, 0),



            new Point3D(0, 0, 0), new Point3D(100, 50, 0), new Point3D(0, 50, 0)



        };



        var faceElem1 = new MeasureElement(0, new Point3D(50, 25, 0), new Vector3D(0, 0, 1), 5000.0, faceVerts);



        var faceRes = new MeasureResult(faceElem1);



        Assert.Equal(5000.0, faceRes.Value, 3);



        Assert.Contains("5000.00", faceRes.MainText);







        // 3. 面到面测距：两个平行平面，间距 80.0 mm



        var faceElem2 = new MeasureElement(1, new Point3D(50, 25, 80), new Vector3D(0, 0, 1), 5000.0, faceVerts);



        var parallelFacesRes = new MeasureResult(faceElem1, faceElem2);



        Assert.Equal(80.0, parallelFacesRes.Value, 3);



        Assert.Contains("80.00", parallelFacesRes.MainText);



        Assert.Contains("平行面间距", parallelFacesRes.MainText);







        // 4. 点到面测距：点 (50, 25, 30) 到平面 Z=0 的垂直法向距离为 30.0 mm



        var ptElem = new MeasureElement(new Point3D(50, 25, 30));



        var ptToFaceRes = new MeasureResult(ptElem, faceElem1);



        Assert.Equal(30.0, ptToFaceRes.Value, 3);



        Assert.Contains("30.00", ptToFaceRes.MainText);







        // 5. 点到线测距：点 (20, 15, 0) 到 X轴线段 (0,0,0) -> (100,0,0) 的垂距为 15.0 mm



        var lineElem = new MeasureElement(new Point3D(0, 0, 0), new Point3D(100, 0, 0));



        var ptToLineRes = new MeasureResult(new MeasureElement(new Point3D(20, 15, 0)), lineElem);



        Assert.Equal(15.0, ptToLineRes.Value, 3);



        Assert.Contains("15.00", ptToLineRes.MainText);



    }







    [Fact]



    public async Task MeshViewer_EntitySelectionAndInViewportCalloutWorkflow()



    {



        var docService = new SwDocumentService();



        var exportService = new ExportService();



        var dialogService = new MockDialogService();



        var meshVm = new MeshViewerViewModel(docService, exportService, dialogService);







        string modelPath = ViewerIntegrationTests.ResolveModelPath("cube.SLDPRT");



        await docService.OpenAsync(modelPath);



        meshVm.LoadMesh(0);







        meshVm.IsMeasureModeEnabled = true;







        // 1. 悬停边线预高亮动作



        var edge = new MeasureElement(new Point3D(-50, -50, -50), new Point3D(50, -50, -50));



        meshVm.HandleHoverElement(edge);



        Assert.NotNull(meshVm.HoveredElement);



        Assert.NotNull(meshVm.WpfHoverHighlightMesh);







        // 2. 选中该边线 -> 单选实体就地在视口展示长度标注



        meshVm.HandleSelectElement(edge);



        Assert.NotNull(meshVm.SelectedElement1);



        Assert.True(meshVm.HasCallout);



        Assert.Contains("100.00", meshVm.CalloutMainText);



        Assert.NotNull(meshVm.WpfSelectionHighlightMesh);







        // 3. 悬停并选中上表面 Z=50 -> 测量边线到底面/平面的距离



        var topFace = new MeasureElement(1, new Point3D(0, 0, 50), new Vector3D(0, 0, 1), 10000.0, new List<Point3D>());



        meshVm.HandleSelectElement(topFace);



        Assert.NotNull(meshVm.SelectedElement2);



        Assert.True(meshVm.HasMeasureResult);



        Assert.True(meshVm.HasCallout);



        // 边线 Z=-50 到表面 Z=50 的法向垂直距离为 100.00 mm



        Assert.Contains("100.00", meshVm.CalloutMainText);







        docService.Close();



    }




    [Fact]
    public async Task SolidWorks_DualEntity_SelectionAndRemoval_Workflow()
    {
        var docService = new SwDocumentService();
        var exportService = new ExportService();
        var dialogService = new MockDialogService();
        var vm = new MeshViewerViewModel(docService, exportService, dialogService);

        string modelPath = ViewerIntegrationTests.ResolveModelPath("cube.SLDPRT");
        await docService.OpenAsync(modelPath);
        vm.LoadMesh(0);

        vm.IsMeasureModeEnabled = true;

        // 1. 选取实体 1 (边线)
        var edge1 = new MeasureElement(new Point3D(0, 0, 0), new Point3D(50, 0, 0));
        vm.HandleSelectElement(edge1);

        Assert.Equal(1, vm.MeasureStep);
        Assert.NotNull(vm.SelectedElement1);
        Assert.Null(vm.SelectedElement2);
        Assert.NotNull(vm.WpfSelection1Mesh);
        Assert.Null(vm.WpfSelection2Mesh);
        Assert.Contains("50.00", vm.Element1Description);

        // 2. 选取实体 2 (平行边线)
        var edge2 = new MeasureElement(new Point3D(0, 100, 0), new Point3D(50, 100, 0));
        vm.HandleSelectElement(edge2);

        Assert.Equal(2, vm.MeasureStep);
        Assert.NotNull(vm.SelectedElement1);
        Assert.NotNull(vm.SelectedElement2);
        Assert.NotNull(vm.WpfSelection1Mesh);
        Assert.NotNull(vm.WpfSelection2Mesh);
        Assert.Contains("50.00", vm.Element2Description);
        Assert.True(vm.HasMeasureResult);
        Assert.Contains("100.00", vm.MeasureDistanceText);

        // 3. 移除实体 1，实体 2 应该被提升为实体 1
        vm.RemoveSelection1Command.Execute(null);
        Assert.Equal(1, vm.MeasureStep);
        Assert.NotNull(vm.SelectedElement1);
        Assert.Null(vm.SelectedElement2);
        Assert.NotNull(vm.WpfSelection1Mesh);
        Assert.Null(vm.WpfSelection2Mesh);

        // 4. 全部清空
        vm.ClearMeasureCommand.Execute(null);
        Assert.Equal(0, vm.MeasureStep);
        Assert.Null(vm.SelectedElement1);
        Assert.Null(vm.SelectedElement2);
        Assert.Null(vm.WpfSelection1Mesh);
        Assert.Null(vm.WpfSelection2Mesh);

        docService.Close();
    }

    [Fact]
    public async Task SolidWorks_XyzMeasurementsToggle_Workflow()
    {
        var docService = new SwDocumentService();
        var exportService = new ExportService();
        var dialogService = new MockDialogService();
        var vm = new MeshViewerViewModel(docService, exportService, dialogService);

        string modelPath = ViewerIntegrationTests.ResolveModelPath("cube.SLDPRT");
        await docService.OpenAsync(modelPath);
        vm.LoadMesh(0);

        vm.IsMeasureModeEnabled = true;
        Assert.True(vm.IsShowXyzMeasurements);

        // 模拟已选择两个空间点 (0,0,0) -> (10, 20, 30)
        var p1 = new MeasureElement(new Point3D(0, 0, 0));
        var p2 = new MeasureElement(new Point3D(10, 20, 30));
        vm.HandleSelectElement(p1);
        vm.HandleSelectElement(p2);

        Assert.True(vm.HasMeasureResult);
        Assert.True(vm.HasDeltaX);
        Assert.True(vm.HasDeltaY);
        Assert.True(vm.HasDeltaZ);
        Assert.NotNull(vm.WpfMeasureXMesh);
        Assert.NotNull(vm.WpfMeasureYMesh);
        Assert.NotNull(vm.WpfMeasureZMesh);

        // 切换关闭 XYZ 测量
        vm.IsShowXyzMeasurements = false;
        Assert.False(vm.HasDeltaX);
        Assert.False(vm.HasDeltaY);
        Assert.False(vm.HasDeltaZ);
        Assert.Null(vm.WpfMeasureXMesh);
        Assert.Null(vm.WpfMeasureYMesh);
        Assert.Null(vm.WpfMeasureZMesh);

        // 重新开启 XYZ 测量
        vm.IsShowXyzMeasurements = true;
        Assert.True(vm.HasDeltaX);
        Assert.True(vm.HasDeltaY);
        Assert.True(vm.HasDeltaZ);
        Assert.NotNull(vm.WpfMeasureXMesh);
        Assert.NotNull(vm.WpfMeasureYMesh);
        Assert.NotNull(vm.WpfMeasureZMesh);

        docService.Close();
    }

    [Fact]
    public void SolidWorks_ArrowheadDimensionLine_And_FaceHighlightOffset()
    {
        // 1. 测试双向工程箭头尺寸线
        var dimMesh = MeshGeometryHelper.CreateDimensionLineMesh(new Point3D(0, 0, 0), new Point3D(100, 0, 0), 1.0, 3.0);
        Assert.NotNull(dimMesh);
        // 包含主线管及两端双向实心工程箭头锥体
        Assert.True(dimMesh.Positions.Count > 16);
        Assert.True(dimMesh.TriangleIndices.Count > 40);

        // 2. 测试面高亮网格带有法向微外推与法线
        var verts = new List<Point3D>
        {
            new Point3D(0, 0, 0), new Point3D(10, 0, 0), new Point3D(10, 10, 0),
            new Point3D(0, 0, 0), new Point3D(10, 10, 0), new Point3D(0, 10, 0)
        };
        var normal = new Vector3D(0, 0, 1);
        var faceElem = new MeasureElement(0, new Point3D(5, 5, 0), normal, 100.0, verts);
        var faceMesh = MeshGeometryHelper.CreateElementHighlightMesh(faceElem, 1.0, 2.0);

        Assert.NotNull(faceMesh);
        // 包含面片正向顶点与锐利工程边界外框线顶点
        Assert.True(faceMesh.Positions.Count >= 6);
        // 顶点应沿法向微外推 (Z > 0)
        Assert.True(faceMesh.Positions[0].Z > 0.01);
        // 含有法向量
        Assert.NotEmpty(faceMesh.Normals);
    }

    [Fact]
    public void MeshGeometryHelper_Fit3DCircle_CalculatesExactCenterAndRadius()
    {
        // 构造倾斜空间圆：中心 (10, 20, 30), 半径 15.0
        var centerExpected = new Point3D(10.0, 20.0, 30.0);
        double radiusExpected = 15.0;

        var normal = new Vector3D(0.57735, 0.57735, 0.57735);
        normal.Normalize();

        Vector3D u = new Vector3D(1, -1, 0);
        u.Normalize();
        Vector3D v = Vector3D.CrossProduct(normal, u);
        v.Normalize();

        var pts = new List<Point3D>();
        for (int i = 0; i < 24; i++)
        {
            double angle = i * 2.0 * Math.PI / 24.0;
            Point3D pt = centerExpected + u * (radiusExpected * Math.Cos(angle)) + v * (radiusExpected * Math.Sin(angle));
            pts.Add(pt);
        }

        bool ok = MeshGeometryHelper.Fit3DCircle(pts, out Point3D center, out Vector3D norm, out double radius, out double relErr);
        Assert.True(ok);
        Assert.True(relErr < 0.001);
        Assert.Equal(centerExpected.X, center.X, 2);
        Assert.Equal(centerExpected.Y, center.Y, 2);
        Assert.Equal(centerExpected.Z, center.Z, 2);
        Assert.Equal(radiusExpected, radius, 2);
    }

    [Fact]
    public void MeasureResult_CircleAndTwoCircles_CalculatesCorrectDistances()
    {
        // 1. 单圆测试
        var c1 = new MeasureElement(new Point3D(0, 0, 0), new Vector3D(0, 0, 1), 6.0, new List<Point3D>());
        var res1 = new MeasureResult(c1);
        Assert.Equal(12.0, res1.Diameter);
        Assert.Equal(6.0, res1.Radius);
        Assert.Contains("⌀ 12.00 mm", res1.MainText);

        // 2. 双圆测试：圆 1 半径 6 (直径 12)，圆 2 半径 4 (直径 8)，两圆心距离 (0,0,0) 到 (30,40,0) = 50.0
        var c2 = new MeasureElement(new Point3D(30, 40, 0), new Vector3D(0, 0, 1), 4.0, new List<Point3D>());
        var res2 = new MeasureResult(c1, c2);

        Assert.Equal(50.0, res2.CenterDistance!.Value, 3);
        Assert.Equal(30.0, res2.DeltaX, 3);
        Assert.Equal(40.0, res2.DeltaY, 3);
        Assert.Equal(0.0, res2.DeltaZ, 3);
        // 最小间距 = 50 - 6 - 4 = 40.0
        Assert.Equal(40.0, res2.MinDistance!.Value, 3);
        // 最大间距 = 50 + 6 + 4 = 60.0
        Assert.Equal(60.0, res2.MaxDistance!.Value, 3);
        Assert.Contains("中心距: 50.00 mm", res2.MainText);
        Assert.Contains("最小间距: 40.00 mm", res2.SubText);
        Assert.Contains("最大间距: 60.00 mm", res2.SubText);
    }

    [Fact]
    public void FitCircleOnAxisPlane_WithCylinderPoints_FitsAccurately()
    {
        var pts = new List<Point3D>();
        double expectedR = 15.0;
        Point3D expectedCenter = new Point3D(10, 20, 0);
        Vector3D axis = new Vector3D(0, 0, 1);

        for (int i = 0; i < 16; i++)
        {
            double angle = i * (2.0 * Math.PI / 16.0);
            double z = (i % 3) * 5.0; // 模拟不同高度
            pts.Add(new Point3D(expectedCenter.X + expectedR * Math.Cos(angle), expectedCenter.Y + expectedR * Math.Sin(angle), z));
        }

        bool success = MeshGeometryHelper.FitCircleOnAxisPlane(pts, axis, new Point3D(10, 20, 0), out Point3D c, out double r, out double relErr);
        Assert.True(success);
        Assert.Equal(expectedR, r, 2);
        Assert.Equal(expectedCenter.X, c.X, 2);
        Assert.Equal(expectedCenter.Y, c.Y, 2);
        Assert.True(relErr < 0.01);
    }

    [Fact]
    public void DetectCylinderSurface_WithCylinderMesh_IdentifiesCylinderAndAllFaceVertices()
    {
        // 构造一个由 12 段圆弧组成的真实 CAD 圆柱筒网格 (12*2 = 24 个共用顶点焊接三角形)
        var posList = new List<System.Numerics.Vector3>();
        var triList = new List<int>();
        double r = 20.0;
        int segments = 12;

        for (int i = 0; i < segments; i++)
        {
            double a = i * (2.0 * Math.PI / segments);
            posList.Add(new System.Numerics.Vector3((float)(r * Math.Cos(a)), (float)(r * Math.Sin(a)), 0f));
            posList.Add(new System.Numerics.Vector3((float)(r * Math.Cos(a)), (float)(r * Math.Sin(a)), 10f));
        }

        for (int i = 0; i < segments; i++)
        {
            int b0 = i * 2;
            int t0 = i * 2 + 1;
            int nextI = (i + 1) % segments;
            int b1 = nextI * 2;
            int t1 = nextI * 2 + 1;

            triList.Add(b0); triList.Add(b1); triList.Add(t1);
            triList.Add(b0); triList.Add(t1); triList.Add(t0);
        }

        var mesh = new SldFile.DisplayMesh
        {
            Positions = posList,
            TriangleIndices = triList
        };

        // 从第 0 个三角形命中测试
        int i0 = triList[0], i1 = triList[1], i2 = triList[2];
        var elem = MeshGeometryHelper.DetectCylinderSurface(mesh, i0, i1, i2, p => p);
        Assert.NotNull(elem);
        Assert.Equal(MeasureElementType.Cylinder, elem.Type);
        Assert.Equal(r, elem.Radius, 1);
        Assert.Equal(r * 2.0, elem.Diameter, 1);
        // 整个圆柱面的所有三角形顶点均被聚类收集 (24 个三角形 * 3 = 72 个顶点)
        Assert.Equal(24 * 3, elem.FaceVertices.Count);
    }

    [Fact]
    public void DetectCylinderSurface_WithPlanarMesh_ReturnsNullToPreventFalsePositive()
    {
        // 构造一个纯平面的四边形 (2 个共面三角形)
        var posList = new List<System.Numerics.Vector3>
        {
            new System.Numerics.Vector3(0, 0, 0),
            new System.Numerics.Vector3(10, 0, 0),
            new System.Numerics.Vector3(10, 10, 0),
            new System.Numerics.Vector3(0, 10, 0)
        };
        var triList = new List<int>
        {
            0, 1, 2,
            0, 2, 3
        };

        var mesh = new SldFile.DisplayMesh
        {
            Positions = posList,
            TriangleIndices = triList
        };

        // 命中平面三角形，必须返回 null (绝不能把纯平面当成圆柱)
        var elem = MeshGeometryHelper.DetectCylinderSurface(mesh, 0, 1, 2, p => p);
        Assert.Null(elem);
    }

        [Fact]
    public void CancelOrExitMeasure_FirstEscClearsCurrentActiveMeasure_KeepsModeOpen()
    {
        var docService = new SwDocumentService();
        var exportService = new ExportService();
        var dialogService = new MockDialogService();
        var vm = new MeshViewerViewModel(docService, exportService, dialogService);

        vm.Mesh = new SldFile.DisplayMesh
        {
            Min = new System.Numerics.Vector3(0, 0, 0),
            Max = new System.Numerics.Vector3(100, 100, 100),
            Positions = new List<System.Numerics.Vector3> { new System.Numerics.Vector3(0, 0, 0) }
        };
        vm.IsMeasureModeEnabled = true;
        vm.HandleSelectElement(new MeasureElement(new Point3D(10, 20, 30)));

        Assert.Equal(1, vm.MeasureStep);
        Assert.NotNull(vm.SelectedElement1);

        // 第一次按 Esc：取消当前待测项，清空已选，保留测量工具开启
        vm.CancelOrExitMeasure();

        Assert.True(vm.IsMeasureModeEnabled);
        Assert.Equal(0, vm.MeasureStep);
        Assert.Null(vm.SelectedElement1);
        Assert.Contains("测量已取消", vm.MeasureStatusText);
    }

    [Fact]
    public void CancelOrExitMeasure_SecondEscWhenEmpty_ClosesMeasureMode()
    {
        var docService = new SwDocumentService();
        var exportService = new ExportService();
        var dialogService = new MockDialogService();
        var vm = new MeshViewerViewModel(docService, exportService, dialogService);

        vm.Mesh = new SldFile.DisplayMesh
        {
            Min = new System.Numerics.Vector3(0, 0, 0),
            Max = new System.Numerics.Vector3(100, 100, 100),
            Positions = new List<System.Numerics.Vector3> { new System.Numerics.Vector3(0, 0, 0) }
        };
        vm.IsMeasureModeEnabled = true;
        Assert.Equal(0, vm.MeasureStep);
        Assert.Null(vm.SelectedElement1);
        Assert.Null(vm.SelectedElement2);

        // 空闲状态下按 Esc：直接关闭测量模式
        vm.CancelOrExitMeasure();

        Assert.False(vm.IsMeasureModeEnabled);
    }

    [Fact]
    public void CancelOrExitMeasure_CompletedMeasure_FirstEscClearsResult_SecondEscExits()
    {
        var docService = new SwDocumentService();
        var exportService = new ExportService();
        var dialogService = new MockDialogService();
        var vm = new MeshViewerViewModel(docService, exportService, dialogService);

        vm.Mesh = new SldFile.DisplayMesh
        {
            Min = new System.Numerics.Vector3(0, 0, 0),
            Max = new System.Numerics.Vector3(100, 100, 100),
            Positions = new List<System.Numerics.Vector3> { new System.Numerics.Vector3(0, 0, 0) }
        };
        vm.IsMeasureModeEnabled = true;
        vm.HandleSelectElement(new MeasureElement(new Point3D(0, 0, 0)));
        vm.HandleSelectElement(new MeasureElement(new Point3D(100, 0, 0)));

        Assert.Equal(2, vm.MeasureStep);
        Assert.True(vm.HasMeasureResult);

        // 第一次 Esc：清空测量结果并恢复待测
        vm.CancelOrExitMeasure();

        Assert.True(vm.IsMeasureModeEnabled);
        Assert.Equal(0, vm.MeasureStep);
        Assert.False(vm.HasMeasureResult);
        Assert.Null(vm.SelectedElement1);
        Assert.Null(vm.SelectedElement2);

        // 第二次 Esc：退出测量模式
        vm.CancelOrExitMeasure();

        Assert.False(vm.IsMeasureModeEnabled);
    }

    [Fact]
    public void DetectCylinderSurface_WithCylinderAndConnectedPlane_DoesNotLeakToPlane()
    {
        // 构造一个圆柱筒体，并在顶部连接一个垂直法兰平面
        // 验证 DetectCylinderSurface 仅收集圆柱面顶点，完全不包含平面顶点
        int segments = 16;
        double r = 10.0;
        double h = 20.0;
        var posList = new List<System.Numerics.Vector3>();
        var triList = new List<int>();

        // 1. 圆柱侧壁顶点
        for (int i = 0; i < segments; i++)
        {
            double angle = i * 2.0 * Math.PI / segments;
            float x = (float)(r * Math.Cos(angle));
            float y = (float)(r * Math.Sin(angle));
            posList.Add(new System.Numerics.Vector3(x, y, 0));
            posList.Add(new System.Numerics.Vector3(x, y, (float)h));
        }

        for (int i = 0; i < segments; i++)
        {
            int b0 = i * 2;
            int t0 = i * 2 + 1;
            int nextI = (i + 1) % segments;
            int b1 = nextI * 2;
            int t1 = nextI * 2 + 1;

            triList.Add(b0); triList.Add(b1); triList.Add(t1);
            triList.Add(b0); triList.Add(t1); triList.Add(t0);
        }

        // 2. 在 z = h 处连接一个垂直平面 (法向为 (0, 0, 1))
        int pBase = posList.Count;
        posList.Add(new System.Numerics.Vector3(-30, -30, (float)h));
        posList.Add(new System.Numerics.Vector3(30, -30, (float)h));
        posList.Add(new System.Numerics.Vector3(30, 30, (float)h));
        posList.Add(new System.Numerics.Vector3(-30, 30, (float)h));

        triList.Add(pBase); triList.Add(pBase + 1); triList.Add(pBase + 2);
        triList.Add(pBase); triList.Add(pBase + 2); triList.Add(pBase + 3);

        var mesh = new SldFile.DisplayMesh
        {
            Positions = posList,
            TriangleIndices = triList
        };

        // 从圆柱面第 0 个三角形命中
        var elem = MeshGeometryHelper.DetectCylinderSurface(mesh, triList[0], triList[1], triList[2], p => p);
        Assert.NotNull(elem);
        Assert.Equal(MeasureElementType.Cylinder, elem.Type);

        // 确保 FaceVertices 中完全没有平面的 4 个顶点 (其 x 或 y 绝对值达到 30)
        foreach (var v in elem.FaceVertices)
        {
            Assert.True(Math.Abs(v.X) <= r + 1.0, $"Leak detected: v.X={v.X} exceeds cylinder radius");
            Assert.True(Math.Abs(v.Y) <= r + 1.0, $"Leak detected: v.Y={v.Y} exceeds cylinder radius");
        }
    }

    [Fact]
    public void DetectCircleFromOutlineEdges_WithRectangleLoop_RejectsAndReturnsNull()
    {
        // 构造一个矩形端面的闭合外轮廓回路 (4 个角为 90 度直角)
        // 验证 DetectCircleFromOutlineEdges 绝不将其误判为圆孔
        var posList = new List<System.Numerics.Vector3>
        {
            new System.Numerics.Vector3(0, 0, 0),
            new System.Numerics.Vector3(40, 0, 0),
            new System.Numerics.Vector3(80, 0, 0),
            new System.Numerics.Vector3(80, 15, 0),
            new System.Numerics.Vector3(80, 30, 0),
            new System.Numerics.Vector3(40, 30, 0),
            new System.Numerics.Vector3(0, 30, 0),
            new System.Numerics.Vector3(0, 15, 0)
        };

        var outlineEdges = new List<(int, int)>
        {
            (0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (6, 7), (7, 0)
        };

        var mesh = new SldFile.DisplayMesh
        {
            Positions = posList,
            OutlineEdges = outlineEdges
        };

        var elem = MeshGeometryHelper.DetectCircleFromOutlineEdges(mesh, 0, p => p);
        Assert.Null(elem);
    }
}
