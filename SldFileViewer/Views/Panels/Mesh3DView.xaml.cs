using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using SldFileViewer.ViewModels;

namespace SldFileViewer.Views.Panels;

public partial class Mesh3DView : UserControl
{
    private Point _lastMousePos;
    private bool _isRotating;
    private bool _isPanning;
    private bool _isZooming;

    // 剖切平面拖动交互状态
    private bool _isDraggingSection;
    private Point _dragStartMousePos;
    private double _dragStartPercent;

    private double _cameraDistance = 250.0;
    private double _cameraWidth = 200.0;
    private double _phi = 55.0; // Angle from Y axis
    private double _theta = 45.0; // Azimuth angle
    private Point3D _target = new(0, 0, 0);

    public Mesh3DView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private MeshViewerViewModel? VM => DataContext as MeshViewerViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (VM != null)
        {
            VM.PropertyChanged += OnViewModelPropertyChanged;
            UpdateMesh(resetCamera: true);
            UpdateSectionVisuals();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (VM != null)
        {
            VM.PropertyChanged -= OnViewModelPropertyChanged;
        }
        if (Mouse.OverrideCursor == Cursors.SizeAll)
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MeshViewerViewModel.Mesh))
        {
            UpdateMesh(resetCamera: true);
        }
        else if (e.PropertyName == nameof(MeshViewerViewModel.WpfMesh))
        {
            // 剖切重新裁剪网格时不重置相机视角，保持视角稳定连续
            UpdateMesh(resetCamera: false);
        }
        else if (e.PropertyName == nameof(MeshViewerViewModel.ShowEdges) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfEdgeMesh))
        {
            UpdateEdgeVisual();
        }
        else if (e.PropertyName == nameof(MeshViewerViewModel.WpfSectionPlaneMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfSectionPlaneBorderMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfSectionCutMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfSectionManipulatorMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.IsSectionViewEnabled))
        {
            UpdateSectionVisuals();
        }
    }

    private void UpdateMesh(bool resetCamera)
    {
        ModelVisual.Content = null;
        if (VM?.WpfMesh == null)
        {
            UpdateEdgeVisual();
            UpdateSectionVisuals();
            return;
        }

        var group = new Model3DGroup();

        // CAD 金属漆面着色材质
        var materialGroup = new MaterialGroup();
        materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(150, 168, 185))));
        materialGroup.Children.Add(new SpecularMaterial(Brushes.White, 30.0));

        var backMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(110, 120, 135)));

        var surfaceModel = new GeometryModel3D
        {
            Geometry = VM.WpfMesh,
            Material = materialGroup,
            BackMaterial = backMaterial
        };

        group.Children.Add(surfaceModel);
        ModelVisual.Content = group;

        UpdateEdgeVisual();
        UpdateSectionVisuals();

        if (resetCamera)
        {
            ResetCamera();
        }
    }

    private void UpdateEdgeVisual()
    {
        EdgeVisual.Content = null;
        if (VM == null || !VM.ShowEdges || VM.WpfEdgeMesh == null) return;

        var edgeMaterial = new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(35, 39, 46)));
        var edgeModel = new GeometryModel3D
        {
            Geometry = VM.WpfEdgeMesh,
            Material = edgeMaterial,
            BackMaterial = edgeMaterial
        };
        EdgeVisual.Content = edgeModel;
    }

    private void UpdateSectionVisuals()
    {
        if (VM == null || !VM.IsSectionViewEnabled)
        {
            SectionPlaneVisual.Content = null;
            SectionBorderVisual.Content = null;
            SectionCutVisual.Content = null;
            SectionManipulatorVisual.Content = null;
            if (Mouse.OverrideCursor == Cursors.SizeAll)
            {
                Mouse.OverrideCursor = null;
            }
            return;
        }

        // 1. 半透明剖切基准面 (SolidWorks 经典冰蓝半透明面，显式 Brush.Opacity 确保 Alpha 混合)
        if (VM.WpfSectionPlaneMesh != null)
        {
            var planeBrush = new SolidColorBrush(Color.FromRgb(30, 150, 255))
            {
                Opacity = 0.28
            };
            planeBrush.Freeze();

            var planeMat = new MaterialGroup();
            planeMat.Children.Add(new DiffuseMaterial(planeBrush));
            planeMat.Children.Add(new EmissiveMaterial(planeBrush));

            SectionPlaneVisual.Content = new GeometryModel3D
            {
                Geometry = VM.WpfSectionPlaneMesh,
                Material = planeMat,
                BackMaterial = planeMat
            };
        }
        else
        {
            SectionPlaneVisual.Content = null;
        }

        // 2. 剖切平面外边框线 (亮蓝色线框)
        if (VM.WpfSectionPlaneBorderMesh != null)
        {
            var borderMat = new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(0, 120, 215)));
            SectionBorderVisual.Content = new GeometryModel3D
            {
                Geometry = VM.WpfSectionPlaneBorderMesh,
                Material = borderMat,
                BackMaterial = borderMat
            };
        }
        else
        {
            SectionBorderVisual.Content = null;
        }

        // 3. 截面边缘交线 (高对比度金橙色高亮线)
        if (VM.WpfSectionCutMesh != null)
        {
            var cutMat = new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(255, 120, 0)));
            SectionCutVisual.Content = new GeometryModel3D
            {
                Geometry = VM.WpfSectionCutMesh,
                Material = cutMat,
                BackMaterial = cutMat
            };
        }
        else
        {
            SectionCutVisual.Content = null;
        }

        // 4. 中心操纵手柄与箭头 (亮金橙色立体材质)
        if (VM.WpfSectionManipulatorMesh != null)
        {
            var handleMat = new MaterialGroup();
            handleMat.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 170, 0))));
            handleMat.Children.Add(new SpecularMaterial(Brushes.White, 45.0));

            SectionManipulatorVisual.Content = new GeometryModel3D
            {
                Geometry = VM.WpfSectionManipulatorMesh,
                Material = handleMat,
                BackMaterial = handleMat
            };
        }
        else
        {
            SectionManipulatorVisual.Content = null;
        }
    }

    private bool HitTestSectionVisual(Point point)
    {
        if (VM == null || !VM.IsSectionViewEnabled) return false;
        if (SectionPlaneVisual.Content == null && SectionManipulatorVisual.Content == null) return false;

        bool hit = false;
        PointHitTestParameters hitParams = new(point);
        VisualTreeHelper.HitTest(MainViewport, null, hitResult =>
        {
            if (hitResult is RayMeshGeometry3DHitTestResult meshHit)
            {
                var visual = meshHit.VisualHit as ModelVisual3D;
                if (visual == SectionPlaneVisual ||
                    visual == SectionManipulatorVisual ||
                    visual == SectionBorderVisual)
                {
                    hit = true;
                    return HitTestResultBehavior.Stop;
                }
            }
            return HitTestResultBehavior.Continue;
        }, hitParams);

        return hit;
    }

    private void ResetCamera()
    {
        _theta = 45.0;
        _phi = 55.0;
        _target = new Point3D(0, 0, 0);

        if (VM?.Mesh != null && VM.Mesh.VertexCount > 0)
        {
            var size = VM.Mesh.Max - VM.Mesh.Min;
            double maxDim = Math.Max(size.X, Math.Max(size.Y, size.Z));
            if (maxDim <= 0.001) maxDim = 100.0;

            // Camera is placed safely outside the bounding box
            _cameraDistance = Math.Max(maxDim * 2.5, 100.0);
            _cameraWidth = maxDim * 1.8;
        }
        else
        {
            _cameraDistance = 250.0;
            _cameraWidth = 200.0;
        }

        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        double phiRad = _phi * Math.PI / 180.0;
        double thetaRad = _theta * Math.PI / 180.0;

        double x = _cameraDistance * Math.Sin(phiRad) * Math.Sin(thetaRad);
        double y = _cameraDistance * Math.Cos(phiRad);
        double z = _cameraDistance * Math.Sin(phiRad) * Math.Cos(thetaRad);

        Camera.Position = new Point3D(_target.X + x, _target.Y + y, _target.Z + z);
        Camera.LookDirection = new Vector3D(-x, -y, -z);
        Camera.UpDirection = new Vector3D(0, 1, 0);
        Camera.Width = _cameraWidth;
    }

    private void OnResetViewClick(object sender, RoutedEventArgs e)
    {
        ResetCamera();
    }

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        _lastMousePos = e.GetPosition(ViewportContainer);

        // 双击中键或右键 -> 全图复位 (SolidWorks 经典习惯)
        if (e.ClickCount == 2 && (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right))
        {
            ResetCamera();
            e.Handled = true;
            return;
        }

        // 剖切平面鼠标左键直接拖动判定
        if (e.ChangedButton == MouseButton.Left && VM != null && VM.IsSectionViewEnabled)
        {
            if (HitTestSectionVisual(_lastMousePos))
            {
                _isDraggingSection = true;
                _dragStartMousePos = _lastMousePos;
                _dragStartPercent = VM.SectionOffsetPercent;
                ViewportContainer.CaptureMouse();
                e.Handled = true;
                return;
            }
        }

        if (e.ChangedButton == MouseButton.Middle)
        {
            // SolidWorks 控制键位：
            // Ctrl + 中键拖拽 = 平移
            // Shift + 中键拖拽 = 缩放
            // 中键拖拽 = 旋转
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                _isPanning = true;
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                _isZooming = true;
            else
                _isRotating = true;

            ViewportContainer.CaptureMouse();
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Left)
        {
            _isRotating = true;
            ViewportContainer.CaptureMouse();
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Right)
        {
            _isPanning = true;
            ViewportContainer.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        Point currentPos = e.GetPosition(ViewportContainer);
        Vector delta = currentPos - _lastMousePos;

        // 1. 正在拖动剖切平面
        if (_isDraggingSection && VM != null)
        {
            Vector totalDelta = currentPos - _dragStartMousePos;
            double viewWidth = ViewportContainer.ActualWidth > 0 ? ViewportContainer.ActualWidth : 800;
            double worldPerPixel = Camera.Width / viewWidth;

            Vector3D look = Camera.LookDirection;
            if (look.Length > 1e-5) look.Normalize();

            Vector3D up = Camera.UpDirection;
            if (up.Length > 1e-5) up.Normalize();

            Vector3D right = Vector3D.CrossProduct(look, up);
            if (right.Length > 1e-5) right.Normalize();

            up = Vector3D.CrossProduct(right, look);
            if (up.Length > 1e-5) up.Normalize();

            // 鼠标在相机正交投影平面上的 3D 位移
            Vector3D mouseWorldDelta = right * (totalDelta.X * worldPerPixel) - up * (totalDelta.Y * worldPerPixel);

            // 获取当前剖切轴向物理向量 (客观基准轴)
            var axisDir = VM.GetCurrentSectionAxisDir();
            Vector3D axisVec = new Vector3D(axisDir.X, axisDir.Y, axisDir.Z);

            // 沿基准轴向的物理位移 (mm)
            double deltaDistance = Vector3D.DotProduct(mouseWorldDelta, axisVec);

            double halfDim = VM.GetCurrentAxisHalfDim();
            if (halfDim > 1e-4)
            {
                double deltaPercent = (deltaDistance / halfDim) * 100.0;
                double newPercent = Math.Clamp(_dragStartPercent + deltaPercent, -100.0, 100.0);
                VM.SectionOffsetPercent = newPercent;
            }

            e.Handled = true;
            return;
        }

        // 2. 悬停检测：当鼠标悬浮在剖切面上时显示拖动光标
        if (!_isRotating && !_isPanning && !_isZooming && VM != null && VM.IsSectionViewEnabled)
        {
            if (HitTestSectionVisual(currentPos))
            {
                Mouse.OverrideCursor = Cursors.SizeAll;
            }
            else if (Mouse.OverrideCursor == Cursors.SizeAll)
            {
                Mouse.OverrideCursor = null;
            }
        }

        // 3. 视图缩放、旋转与平移
        if (_isZooming)
        {
            double factor = Math.Pow(1.008, -delta.Y);
            _cameraWidth = Math.Clamp(_cameraWidth * factor, 0.5, 100000.0);
            Camera.Width = _cameraWidth;
            _lastMousePos = currentPos;
            e.Handled = true;
        }
        else if (_isRotating)
        {
            _theta -= delta.X * 0.4;
            _phi = Math.Clamp(_phi - delta.Y * 0.4, 5.0, 175.0);
            UpdateCameraPosition();
            _lastMousePos = currentPos;
            e.Handled = true;
        }
        else if (_isPanning)
        {
            double viewWidth = ViewportContainer.ActualWidth > 0 ? ViewportContainer.ActualWidth : 800;
            double panFactor = _cameraWidth / viewWidth;

            Vector3D look = Camera.LookDirection;
            if (look.Length > 1e-5)
            {
                look.Normalize();

                Vector3D right = Vector3D.CrossProduct(look, Camera.UpDirection);
                if (right.Length > 1e-5)
                {
                    right.Normalize();

                    Vector3D up = Vector3D.CrossProduct(right, look);
                    if (up.Length > 1e-5)
                    {
                        up.Normalize();
                        _target += right * (-delta.X * panFactor) + up * (delta.Y * panFactor);
                        UpdateCameraPosition();
                    }
                }
            }

            _lastMousePos = currentPos;
            e.Handled = true;
        }
    }

    private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingSection)
        {
            _isDraggingSection = false;
            ViewportContainer.ReleaseMouseCapture();
            if (Mouse.OverrideCursor == Cursors.SizeAll)
            {
                Mouse.OverrideCursor = null;
            }
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Middle)
        {
            _isRotating = false;
            _isZooming = false;
        }
        if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle)
        {
            _isPanning = false;
        }

        if (!_isRotating && !_isPanning && !_isZooming && !_isDraggingSection)
        {
            ViewportContainer.ReleaseMouseCapture();
        }
        e.Handled = true;
    }

    private void OnViewportMouseLeave(object sender, MouseEventArgs e)
    {
        if (Mouse.OverrideCursor == Cursors.SizeAll && !_isDraggingSection)
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        double factor = e.Delta > 0 ? 0.85 : 1.18;

        double minWidth = 0.5;
        double maxWidth = 100000.0;
        if (VM?.Mesh != null && VM.Mesh.VertexCount > 0)
        {
            var size = VM.Mesh.Max - VM.Mesh.Min;
            double maxDim = Math.Max(size.X, Math.Max(size.Y, size.Z));
            minWidth = Math.Max(maxDim * 0.05, 0.5);
            maxWidth = Math.Max(maxDim * 30.0, 5000.0);
        }

        _cameraWidth = Math.Clamp(_cameraWidth * factor, minWidth, maxWidth);
        Camera.Width = _cameraWidth;
        e.Handled = true;
    }
}