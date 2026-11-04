using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using SldFileViewer.Models;
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

    // 测量标牌自由拖拽与引线连线状态
    private bool _isDraggingCallout;
    private FrameworkElement? _draggingBadgeElement;
    private Point _badgeMouseStartPos;
    private Vector _badgeOffsetStart;
    private Vector _calloutUserOffset = new(0, 0);

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

    private Window? _parentWindow;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (VM != null)
        {
            VM.PropertyChanged += OnViewModelPropertyChanged;
            VM.RequestStandardView += OnRequestStandardView;
            VM.RequestZoomToFit += OnRequestZoomToFit;
            VM.RequestZoomToComponent += OnRequestZoomToComponent;
            UpdateMesh(resetCamera: true);
            UpdateSectionVisuals();
        }

        _parentWindow = Window.GetWindow(this);
        if (_parentWindow != null)
        {
            _parentWindow.PreviewKeyDown += OnWindowPreviewKeyDown;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (VM != null)
        {
            VM.PropertyChanged -= OnViewModelPropertyChanged;
            VM.RequestStandardView -= OnRequestStandardView;
            VM.RequestZoomToFit -= OnRequestZoomToFit;
            VM.RequestZoomToComponent -= OnRequestZoomToComponent;
        }
        if (_parentWindow != null)
        {
            _parentWindow.PreviewKeyDown -= OnWindowPreviewKeyDown;
            _parentWindow = null;
        }
        if (Mouse.OverrideCursor == Cursors.SizeAll)
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (VM != null && VM.IsMeasureModeEnabled)
            {
                VM.CancelOrExitMeasure();
                e.Handled = true;
                return;
            }
            if (VM?.HighlightedComponentName != null)
            {
                VM.ClearComponentHighlight();
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.D1:
                case Key.NumPad1:
                    SetStandardView(StandardViewType.Front);
                    e.Handled = true;
                    return;
                case Key.D2:
                case Key.NumPad2:
                    SetStandardView(StandardViewType.Back);
                    e.Handled = true;
                    return;
                case Key.D3:
                case Key.NumPad3:
                    SetStandardView(StandardViewType.Left);
                    e.Handled = true;
                    return;
                case Key.D4:
                case Key.NumPad4:
                    SetStandardView(StandardViewType.Right);
                    e.Handled = true;
                    return;
                case Key.D5:
                case Key.NumPad5:
                    SetStandardView(StandardViewType.Top);
                    e.Handled = true;
                    return;
                case Key.D6:
                case Key.NumPad6:
                    SetStandardView(StandardViewType.Bottom);
                    e.Handled = true;
                    return;
                case Key.D7:
                case Key.NumPad7:
                    SetStandardView(StandardViewType.Isometric);
                    e.Handled = true;
                    return;
                case Key.D8:
                case Key.NumPad8:
                    SetStandardView(StandardViewType.NormalTo);
                    e.Handled = true;
                    return;
            }
        }
        else if (Keyboard.Modifiers == ModifierKeys.None)
        {
            if (e.Key == Key.F && !(FocusManager.GetFocusedElement(Window.GetWindow(this)) is TextBox))
            {
                ZoomToFit();
                e.Handled = true;
                return;
            }
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
        else if (e.PropertyName == nameof(MeshViewerViewModel.WpfMeasureLineMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfMeasureMarkersMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfMeasureXMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfMeasureYMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfMeasureZMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfBoundingBoxMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfHoverHighlightMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfSelectionHighlightMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfSelection1Mesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfSelection2Mesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.IsShowXyzMeasurements) ||
                 e.PropertyName == nameof(MeshViewerViewModel.IsMeasureModeEnabled))
        {
            if (VM?.IsMeasureModeEnabled != true) _calloutUserOffset = new(0, 0);
            UpdateMeasureVisuals();
        }
        else if (e.PropertyName == nameof(MeshViewerViewModel.MeasureStep))
        {
            if (VM?.MeasureStep == 0) _calloutUserOffset = new(0, 0);
        }
        else if (e.PropertyName == nameof(MeshViewerViewModel.WpfSectionPlaneMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfSectionPlaneBorderMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfSectionCutMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.WpfSectionManipulatorMesh) ||
                 e.PropertyName == nameof(MeshViewerViewModel.IsSectionViewEnabled))
        {
            UpdateSectionVisuals();
        }
        else if (e.PropertyName == nameof(MeshViewerViewModel.DisplayStyle))
        {
            UpdateMesh(resetCamera: false);
            UpdateEdgeVisual();
        }
        else if (e.PropertyName == nameof(MeshViewerViewModel.WpfComponentHighlightMesh))
        {
            UpdateComponentHighlightVisual();
        }
    }

    private void UpdateMesh(bool resetCamera)
    {
        ModelVisual.Content = null;
        if (VM?.WpfMesh == null)
        {
            UpdateEdgeVisual();
            UpdateSectionVisuals();
            UpdateComponentHighlightVisual();
            return;
        }

        var group = new Model3DGroup();

        // 仅在非线框模式下渲染实体表面
        if (VM.DisplayStyle != ModelDisplayStyle.Wireframe)
        {
            Material material;
            Material backMaterial;

            if (VM.DisplayStyle == ModelDisplayStyle.TechnicalWhite)
            {
                // 消除隐藏线白模 / 纯白工程纸感
                var whiteMat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(245, 247, 250)));
                material = whiteMat;
                backMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(225, 228, 233)));
            }
            else
            {
                // CAD 金属漆面着色材质
                var materialGroup = new MaterialGroup();
                materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(145, 158, 172))));
                materialGroup.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(105, 115, 125)), 35.0));
                material = materialGroup;
                backMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(125, 135, 148)));
            }

            var surfaceModel = new GeometryModel3D
            {
                Geometry = VM.WpfMesh,
                Material = material,
                BackMaterial = backMaterial
            };

            group.Children.Add(surfaceModel);
        }

        ModelVisual.Content = group;

        UpdateEdgeVisual();
        UpdateSectionVisuals();
        UpdateComponentHighlightVisual();

        if (resetCamera)
        {
            ResetCamera();
        }
    }

    private void UpdateEdgeVisual()
    {
        EdgeVisual.Content = null;
        if (VM == null || VM.WpfEdgeMesh == null) return;
        if (VM.DisplayStyle == ModelDisplayStyle.Shaded) return;
        if (!VM.ShowEdges && VM.DisplayStyle != ModelDisplayStyle.Wireframe) return;

        Color edgeColor = (VM.DisplayStyle == ModelDisplayStyle.Wireframe || VM.DisplayStyle == ModelDisplayStyle.TechnicalWhite)
            ? Color.FromRgb(15, 23, 42) // 纯黑高对比工程边线
            : Color.FromRgb(35, 39, 46); // 标准深色轮廓边线

        var edgeMaterial = new EmissiveMaterial(new SolidColorBrush(edgeColor));
        var edgeModel = new GeometryModel3D
        {
            Geometry = VM.WpfEdgeMesh,
            Material = edgeMaterial,
            BackMaterial = edgeMaterial
        };
        EdgeVisual.Content = edgeModel;
    }


        /// <summary>
    /// 将 3D 居中坐标点投影为 ViewportContainer 上的 2D 屏幕像素坐标 (支持正交投影相机)
    /// </summary>
    private Point? WorldToScreen(Point3D point3D)
    {
        if (Camera is not OrthographicCamera orthoCam) return null;
        double actualWidth = ViewportContainer.ActualWidth;
        double actualHeight = ViewportContainer.ActualHeight;
        if (actualWidth <= 0 || actualHeight <= 0) return null;

        // 正交相机参数
        Point3D camPos = orthoCam.Position;
        Vector3D lookDir = orthoCam.LookDirection;
        if (lookDir.Length < 1e-6) return null;
        lookDir.Normalize();

        Vector3D upDir = orthoCam.UpDirection;
        if (upDir.Length < 1e-6) upDir = new Vector3D(0, 1, 0);
        upDir.Normalize();

        Vector3D rightDir = Vector3D.CrossProduct(lookDir, upDir);
        if (rightDir.Length < 1e-6) rightDir = new Vector3D(1, 0, 0);
        rightDir.Normalize();

        upDir = Vector3D.CrossProduct(rightDir, lookDir);
        upDir.Normalize();

        Vector3D delta = point3D - camPos;

        double xWorld = Vector3D.DotProduct(delta, rightDir);
        double yWorld = Vector3D.DotProduct(delta, upDir);

        double camWidth = orthoCam.Width;
        if (camWidth <= 0) camWidth = 200.0;
        double camHeight = camWidth * (actualHeight / actualWidth);

        double screenX = (xWorld / camWidth + 0.5) * actualWidth;
        double screenY = (0.5 - yWorld / camHeight) * actualHeight;

        return new Point(screenX, screenY);
    }
    private void UpdateMeasureVisuals()
    {
        if (VM == null) return;

        // 1. 3D 尺寸主线 (带双向实心工程箭头)
        if (VM.WpfMeasureLineMesh != null)
        {
            var lineGroup = new MaterialGroup();
            lineGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 193, 7))));
            lineGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(255, 193, 7))));
            MeasureLineVisual.Content = new GeometryModel3D
            {
                Geometry = VM.WpfMeasureLineMesh,
                Material = lineGroup,
                BackMaterial = lineGroup
            };
        }
        else
        {
            MeasureLineVisual.Content = null;
        }

        // 2. 测量端点图钉球体
        if (VM.WpfMeasureMarkersMesh != null)
        {
            var markerGroup = new MaterialGroup();
            markerGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 87, 34))));
            markerGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(255, 87, 34))));
            MeasureMarkersVisual.Content = new GeometryModel3D
            {
                Geometry = VM.WpfMeasureMarkersMesh,
                Material = markerGroup,
                BackMaterial = markerGroup
            };
        }
        else
        {
            MeasureMarkersVisual.Content = null;
        }

        // 3. 模型外形包围盒线框
        if (VM.WpfBoundingBoxMesh != null)
        {
            var boxBrush = new SolidColorBrush(Color.FromArgb(200, 3, 169, 244));
            var boxMaterial = new EmissiveMaterial(boxBrush);
            BoundingBoxVisual.Content = new GeometryModel3D
            {
                Geometry = VM.WpfBoundingBoxMesh,
                Material = boxMaterial,
                BackMaterial = boxMaterial
            };
        }
        else
        {
            BoundingBoxVisual.Content = null;
        }

        // 3.1 X 轴正交尺寸引线 (工程红)
        if (VM.WpfMeasureXMesh != null)
        {
            var brush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            var mat = new MaterialGroup();
            mat.Children.Add(new DiffuseMaterial(brush));
            mat.Children.Add(new EmissiveMaterial(brush));
            MeasureXLineVisual.Content = new GeometryModel3D { Geometry = VM.WpfMeasureXMesh, Material = mat, BackMaterial = mat };
        }
        else
        {
            MeasureXLineVisual.Content = null;
        }

        // 3.2 Y 轴正交尺寸引线 (工程绿)
        if (VM.WpfMeasureYMesh != null)
        {
            var brush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            var mat = new MaterialGroup();
            mat.Children.Add(new DiffuseMaterial(brush));
            mat.Children.Add(new EmissiveMaterial(brush));
            MeasureYLineVisual.Content = new GeometryModel3D { Geometry = VM.WpfMeasureYMesh, Material = mat, BackMaterial = mat };
        }
        else
        {
            MeasureYLineVisual.Content = null;
        }

        // 3.3 Z 轴正交尺寸引线 (工程蓝)
        if (VM.WpfMeasureZMesh != null)
        {
            var brush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            var mat = new MaterialGroup();
            mat.Children.Add(new DiffuseMaterial(brush));
            mat.Children.Add(new EmissiveMaterial(brush));
            MeasureZLineVisual.Content = new GeometryModel3D { Geometry = VM.WpfMeasureZMesh, Material = mat, BackMaterial = mat };
        }
        else
        {
            MeasureZLineVisual.Content = null;
        }

        // 4. 鼠标悬停几何元素 (SolidWorks 浅天蓝高光微反射，单面正向渲染无重影)
        if (VM.WpfHoverHighlightMesh != null)
        {
            var hoverBrush = new SolidColorBrush(Color.FromArgb(160, 56, 189, 248));
            var hoverMat = new MaterialGroup();
            hoverMat.Children.Add(new DiffuseMaterial(hoverBrush));
            hoverMat.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(240, 249, 255)), 40.0));
            HoverHighlightVisual.Content = new GeometryModel3D
            {
                Geometry = VM.WpfHoverHighlightMesh,
                Material = hoverMat,
                BackMaterial = null
            };
        }
        else
        {
            HoverHighlightVisual.Content = null;
        }

        // 5. 实体 1 独立高亮 (SolidWorks 经典宝石蓝 #0284C7，立体工程质感)
        if (VM.WpfSelection1Mesh != null)
        {
            var blueBrush = new SolidColorBrush(Color.FromArgb(205, 2, 132, 199));
            var blueMat = new MaterialGroup();
            blueMat.Children.Add(new DiffuseMaterial(blueBrush));
            blueMat.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(224, 242, 254)), 45.0));
            Selection1Visual.Content = new GeometryModel3D
            {
                Geometry = VM.WpfSelection1Mesh,
                Material = blueMat,
                BackMaterial = null
            };
        }
        else
        {
            Selection1Visual.Content = null;
        }

        // 6. 实体 2 独立高亮 (SolidWorks 经典工程翠绿 #16A34A，立体工程质感)
        if (VM.WpfSelection2Mesh != null)
        {
            var greenBrush = new SolidColorBrush(Color.FromArgb(205, 22, 163, 74));
            var greenMat = new MaterialGroup();
            greenMat.Children.Add(new DiffuseMaterial(greenBrush));
            greenMat.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(220, 252, 231)), 45.0));
            Selection2Visual.Content = new GeometryModel3D
            {
                Geometry = VM.WpfSelection2Mesh,
                Material = greenMat,
                BackMaterial = null
            };
        }
        else
        {
            Selection2Visual.Content = null;
        }

        SelectionHighlightVisual.Content = null;

        UpdateCalloutPosition();
    }

    private void UpdateCalloutPosition()
    {
        if (VM == null || !VM.IsMeasureModeEnabled)
        {
            DimensionCallout.Visibility = Visibility.Collapsed;
            CalloutBadgeMain.Visibility = Visibility.Collapsed;
            CalloutBadgeX.Visibility = Visibility.Collapsed;
            CalloutBadgeY.Visibility = Visibility.Collapsed;
            CalloutBadgeZ.Visibility = Visibility.Collapsed;
            CalloutLeaderLine.Visibility = Visibility.Collapsed;
            CalloutLeaderAnchor.Visibility = Visibility.Collapsed;
            return;
        }

        Point? screenMid = null;
        Rect rectMain = Rect.Empty;

        // 0. 黄色主尺寸线中间的直线距离微型标牌就地吸附 (带用户拖拽偏移与防遮挡引线)
        if (VM.CalloutPointMain.HasValue && !string.IsNullOrEmpty(VM.MainLineDistanceText))
        {
            screenMid = WorldToScreen(VM.CalloutPointMain.Value);
            if (screenMid.HasValue)
            {
                CalloutBadgeMain.Visibility = Visibility.Visible;
                CalloutBadgeMain.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double bw = CalloutBadgeMain.DesiredSize.Width;
                double bh = CalloutBadgeMain.DesiredSize.Height;

                // 基础位置加上用户手动拖动的像素偏移量
                double posX = Math.Clamp(screenMid.Value.X + _calloutUserOffset.X - bw * 0.5, 5, Math.Max(5, ViewportContainer.ActualWidth - bw - 5));
                double posY = Math.Clamp(screenMid.Value.Y + _calloutUserOffset.Y - bh - 8, 5, Math.Max(5, ViewportContainer.ActualHeight - bh - 5));

                Canvas.SetLeft(CalloutBadgeMain, posX);
                Canvas.SetTop(CalloutBadgeMain, posY);
                rectMain = new Rect(posX, posY, bw, bh);

                // 拖离原始中点超过 12 像素时，显示引出虚线连回 3D 尺寸中点
                if (_calloutUserOffset.Length > 12.0)
                {
                    CalloutLeaderLine.Visibility = Visibility.Visible;
                    CalloutLeaderLine.X1 = posX + bw * 0.5;
                    CalloutLeaderLine.Y1 = posY + bh * 0.5;
                    CalloutLeaderLine.X2 = screenMid.Value.X;
                    CalloutLeaderLine.Y2 = screenMid.Value.Y;

                    CalloutLeaderAnchor.Visibility = Visibility.Visible;
                    Canvas.SetLeft(CalloutLeaderAnchor, screenMid.Value.X - 2.5);
                    Canvas.SetTop(CalloutLeaderAnchor, screenMid.Value.Y - 2.5);
                }
                else
                {
                    CalloutLeaderLine.Visibility = Visibility.Collapsed;
                    CalloutLeaderAnchor.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                CalloutBadgeMain.Visibility = Visibility.Collapsed;
                CalloutLeaderLine.Visibility = Visibility.Collapsed;
                CalloutLeaderAnchor.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            CalloutBadgeMain.Visibility = Visibility.Collapsed;
            CalloutLeaderLine.Visibility = Visibility.Collapsed;
            CalloutLeaderAnchor.Visibility = Visibility.Collapsed;
        }

        // 1. 主尺寸透明卡片就地吸附
        if (VM.HasCallout && VM.CalloutAnchorPoint.HasValue && VM.IsShowViewportCallout)
        {
            var centeredAnchor = VM.ToCenteredCoordinates(VM.CalloutAnchorPoint.Value);
            var screenPt = WorldToScreen(centeredAnchor);

            if (screenPt.HasValue)
            {
                DimensionCallout.Visibility = Visibility.Visible;
                DimensionCallout.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double w = DimensionCallout.DesiredSize.Width > 0 ? DimensionCallout.DesiredSize.Width : 160;
                double h = DimensionCallout.DesiredSize.Height > 0 ? DimensionCallout.DesiredSize.Height : 50;

                double posX = Math.Clamp(screenPt.Value.X - w * 0.5, 10, Math.Max(10, ViewportContainer.ActualWidth - w - 10));
                double posY = Math.Clamp(screenPt.Value.Y - h - 18, 10, Math.Max(10, ViewportContainer.ActualHeight - h - 10));

                Canvas.SetLeft(DimensionCallout, posX);
                Canvas.SetTop(DimensionCallout, posY);
            }
            else
            {
                DimensionCallout.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            DimensionCallout.Visibility = Visibility.Collapsed;
        }

        // 2. XYZ 三向正交工程尺寸标牌（智能避让防重叠布局，绝不允许文字互相覆盖）
        if (VM.IsShowXyzMeasurements)
        {
            var placedBadges = new List<Rect>();
            if (!rectMain.IsEmpty) placedBadges.Add(rectMain);

            void LayoutComponentBadge(Border badge, bool hasVal, Point3D? pt3D)
            {
                if (!hasVal || !pt3D.HasValue)
                {
                    badge.Visibility = Visibility.Collapsed;
                    return;
                }

                var screenPt = WorldToScreen(pt3D.Value);
                if (!screenPt.HasValue)
                {
                    badge.Visibility = Visibility.Collapsed;
                    return;
                }

                badge.Visibility = Visibility.Visible;
                badge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double bw = badge.DesiredSize.Width;
                double bh = badge.DesiredSize.Height;

                // 默认计划位置：在线段中点上方
                double x = Math.Clamp(screenPt.Value.X - bw * 0.5, 5, Math.Max(5, ViewportContainer.ActualWidth - bw - 5));
                double y = Math.Clamp(screenPt.Value.Y - bh - 8, 5, Math.Max(5, ViewportContainer.ActualHeight - bh - 5));
                var curRect = new Rect(x, y, bw, bh);

                // 智能防重叠检测：若与主标牌或已放置标牌发生重叠（如平行于某轴导致坐标相同）
                bool hasCollision = false;
                foreach (var r in placedBadges)
                {
                    var exp = r;
                    exp.Inflate(6, 4);
                    if (exp.IntersectsWith(curRect))
                    {
                        hasCollision = true;
                        break;
                    }
                }

                if (hasCollision)
                {
                    // 策略 1：向下错开放置在线段下方
                    double altY = Math.Clamp(screenPt.Value.Y + 8, 5, Math.Max(5, ViewportContainer.ActualHeight - bh - 5));
                    var altRect = new Rect(x, altY, bw, bh);
                    bool altCollision = false;
                    foreach (var r in placedBadges)
                    {
                        var exp = r; exp.Inflate(6, 4);
                        if (exp.IntersectsWith(altRect)) { altCollision = true; break; }
                    }

                    if (!altCollision)
                    {
                        y = altY;
                        curRect = altRect;
                    }
                    else
                    {
                        // 策略 2：在主标牌或基准点右侧堆叠错开排列
                        double stackX = Math.Clamp(x + bw + 10, 5, Math.Max(5, ViewportContainer.ActualWidth - bw - 5));
                        double stackY = Math.Clamp(y, 5, Math.Max(5, ViewportContainer.ActualHeight - bh - 5));
                        curRect = new Rect(stackX, stackY, bw, bh);
                        x = stackX;
                        y = stackY;
                    }
                }

                Canvas.SetLeft(badge, x);
                Canvas.SetTop(badge, y);
                placedBadges.Add(curRect);
            }

            LayoutComponentBadge(CalloutBadgeX, VM.HasDeltaX, VM.CalloutPointX);
            LayoutComponentBadge(CalloutBadgeY, VM.HasDeltaY, VM.CalloutPointY);
            LayoutComponentBadge(CalloutBadgeZ, VM.HasDeltaZ, VM.CalloutPointZ);
        }
        else
        {
            CalloutBadgeX.Visibility = Visibility.Collapsed;
            CalloutBadgeY.Visibility = Visibility.Collapsed;
            CalloutBadgeZ.Visibility = Visibility.Collapsed;
        }
    }

    // ── 标牌鼠标交互事件：支持自由拖拽移动与双击归位 ──
    private void OnCalloutBadgeMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                // 双击标牌：瞬间归位
                _calloutUserOffset = new Vector(0, 0);
                UpdateCalloutPosition();
                e.Handled = true;
                return;
            }

            if (sender is FrameworkElement elem)
            {
                _isDraggingCallout = true;
                _draggingBadgeElement = elem;
                _badgeMouseStartPos = e.GetPosition(ViewportContainer);
                _badgeOffsetStart = _calloutUserOffset;
                elem.CaptureMouse();
                e.Handled = true;
            }
        }
    }

    private void OnCalloutBadgeMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingCallout && _draggingBadgeElement != null)
        {
            Point curMouse = e.GetPosition(ViewportContainer);
            Vector delta = curMouse - _badgeMouseStartPos;
            _calloutUserOffset = _badgeOffsetStart + delta;
            UpdateCalloutPosition();
            e.Handled = true;
        }
    }

    private void OnCalloutBadgeMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingCallout && e.ChangedButton == MouseButton.Left)
        {
            _isDraggingCallout = false;
            if (_draggingBadgeElement != null)
            {
                _draggingBadgeElement.ReleaseMouseCapture();
                _draggingBadgeElement = null;
            }
            e.Handled = true;
        }
    }

    private bool HitTestMeshEntity(Point mousePos, out Models.MeasureElement? foundElement)
    {
        foundElement = null;
        if (VM?.WpfMesh == null || VM.Mesh == null) return false;

        Point3D? foundHit = null;
        RayMeshGeometry3DHitTestResult? bestMeshHit = null;

        VisualTreeHelper.HitTest(MainViewport, null, result =>
        {
            if (result is RayMeshGeometry3DHitTestResult meshHit)
            {
                if (meshHit.VisualHit == ModelVisual)
                {
                    foundHit = meshHit.PointHit;
                    bestMeshHit = meshHit;
                    return HitTestResultBehavior.Stop;
                }
            }
            return HitTestResultBehavior.Continue;
        }, new PointHitTestParameters(mousePos));

        if (foundHit.HasValue && bestMeshHit != null)
        {
            var rawPoint = foundHit.Value;
            var meshGeo = bestMeshHit.MeshHit;
            int i0 = bestMeshHit.VertexIndex1;
            int i1 = bestMeshHit.VertexIndex2;
            int i2 = bestMeshHit.VertexIndex3;

            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= meshGeo.Positions.Count || i1 >= meshGeo.Positions.Count || i2 >= meshGeo.Positions.Count)
            {
                return false;
            }

            // 0. 优先检测圆柱表面 (Cylinder Face Snapping) - 实现整筒圆柱面完整高亮与精准测距
            var cylSurfaceElem = Services.MeshGeometryHelper.DetectCylinderSurface(VM.Mesh, i0, i1, i2, p => p);
            if (cylSurfaceElem != null)
            {
                foundElement = cylSurfaceElem;
                return true;
            }

            // 0.1 优先检测圆孔/圆弧边缘回路 (Circle Edge Loop Snapping)
            // 严格要求：命中点 rawPoint 到圆周的垂直距离必须在近距离范围内，防止点在空旷平面上隔空抓取远处圆孔
            int[] candidateVerts = new[] { i0, i1, i2 };
            foreach (int vi in candidateVerts)
            {
                var circleElem = Services.MeshGeometryHelper.DetectCircleFromOutlineEdges(VM.Mesh, vi, p => p);
                if (circleElem != null)
                {
                    // 检查命中的三维坐标点是否在圆周附近 (距离圆周不超过半径的 25% 或 3mm)
                    var cDiff = rawPoint - VM.ToCenteredCoordinates(circleElem.CenterPoint);
                    double distToCenter = cDiff.Length;
                    double radialDist = Math.Abs(distToCenter - circleElem.Radius);
                    if (radialDist <= Math.Max(3.0, circleElem.Radius * 0.25))
                    {
                        foundElement = circleElem;
                        return true;
                    }
                }
            }

// 0.2 移除旧版 DetectCylinderFromPart，防止把整个包含平面的 Part 误高亮

            Point3D v0 = meshGeo.Positions[i0];
            Point3D v1 = meshGeo.Positions[i1];
            Point3D v2 = meshGeo.Positions[i2];

            Point? s0 = WorldToScreen(v0);
            Point? s1 = WorldToScreen(v1);
            Point? s2 = WorldToScreen(v2);

            // 1. 次优吸附尖锐角点 (Vertex Snapping, 12 像素阈值)
            double snapVertexPixels = 12.0;
            if (s0.HasValue && (mousePos - s0.Value).Length <= snapVertexPixels)
            {
                foundElement = new Models.MeasureElement(VM.ToAbsoluteCoordinates(v0));
                return true;
            }
            if (s1.HasValue && (mousePos - s1.Value).Length <= snapVertexPixels)
            {
                foundElement = new Models.MeasureElement(VM.ToAbsoluteCoordinates(v1));
                return true;
            }
            if (s2.HasValue && (mousePos - s2.Value).Length <= snapVertexPixels)
            {
                foundElement = new Models.MeasureElement(VM.ToAbsoluteCoordinates(v2));
                return true;
            }

            // 2. 边线吸附 (Edge Snapping, 8 像素垂距阈值)
            double snapEdgePixels = 8.0;
            if (s0.HasValue && s1.HasValue && DistanceToLineSegment(mousePos, s0.Value, s1.Value) <= snapEdgePixels)
            {
                foundElement = new Models.MeasureElement(VM.ToAbsoluteCoordinates(v0), VM.ToAbsoluteCoordinates(v1));
                return true;
            }
            if (s1.HasValue && s2.HasValue && DistanceToLineSegment(mousePos, s1.Value, s2.Value) <= snapEdgePixels)
            {
                foundElement = new Models.MeasureElement(VM.ToAbsoluteCoordinates(v1), VM.ToAbsoluteCoordinates(v2));
                return true;
            }
            if (s2.HasValue && s0.HasValue && DistanceToLineSegment(mousePos, s2.Value, s0.Value) <= snapEdgePixels)
            {
                foundElement = new Models.MeasureElement(VM.ToAbsoluteCoordinates(v2), VM.ToAbsoluteCoordinates(v0));
                return true;
            }

            // 3. 拾取表面 (智能共面多边形聚合，彻底杜绝矩形只高亮半个直角三角形的缺陷)
            Vector3D refNormal = Vector3D.CrossProduct(v1 - v0, v2 - v0);
            if (refNormal.Length > 1e-6) refNormal.Normalize();

            double refD = Vector3D.DotProduct(refNormal, new Vector3D(v0.X, v0.Y, v0.Z));
            List<Point3D> faceAbsVerts = new List<Point3D>();
            double totalArea = 0.0;
            Point3D weightedCenter = new Point3D(0, 0, 0);

            var pos = meshGeo.Positions;
            var indices = meshGeo.TriangleIndices;
            int triCount = indices.Count / 3;

            for (int t = 0; t < triCount; t++)
            {
                int ti0 = indices[t * 3];
                int ti1 = indices[t * 3 + 1];
                int ti2 = indices[t * 3 + 2];
                if (ti0 >= pos.Count || ti1 >= pos.Count || ti2 >= pos.Count) continue;

                Point3D tp0 = pos[ti0];
                Point3D tp1 = pos[ti1];
                Point3D tp2 = pos[ti2];

                Vector3D triNormal = Vector3D.CrossProduct(tp1 - tp0, tp2 - tp0);
                double triArea = triNormal.Length * 0.5;
                if (triNormal.Length > 1e-6) triNormal.Normalize();

                double dot = Vector3D.DotProduct(triNormal, refNormal);
                if (Math.Abs(dot) > 0.95)
                {
                    Point3D triCenter = new Point3D((tp0.X + tp1.X + tp2.X) / 3.0, (tp0.Y + tp1.Y + tp2.Y) / 3.0, (tp0.Z + tp1.Z + tp2.Z) / 3.0);
                    double distToPlane = Math.Abs(Vector3D.DotProduct(refNormal, new Vector3D(triCenter.X, triCenter.Y, triCenter.Z)) - refD);
                    if (distToPlane < 0.35)
                    {
                        totalArea += triArea;
                        weightedCenter = new Point3D(weightedCenter.X + triCenter.X * triArea,
                                                    weightedCenter.Y + triCenter.Y * triArea,
                                                    weightedCenter.Z + triCenter.Z * triArea);

                        faceAbsVerts.Add(VM.ToAbsoluteCoordinates(tp0));
                        faceAbsVerts.Add(VM.ToAbsoluteCoordinates(tp1));
                        faceAbsVerts.Add(VM.ToAbsoluteCoordinates(tp2));
                    }
                }
            }

            if (totalArea > 1e-6)
            {
                weightedCenter = new Point3D(weightedCenter.X / totalArea, weightedCenter.Y / totalArea, weightedCenter.Z / totalArea);
            }
            else
            {
                totalArea = 0.5 * Vector3D.CrossProduct(v1 - v0, v2 - v0).Length;
                weightedCenter = new Point3D((v0.X + v1.X + v2.X) / 3.0, (v0.Y + v1.Y + v2.Y) / 3.0, (v0.Z + v1.Z + v2.Z) / 3.0);
                faceAbsVerts.Add(VM.ToAbsoluteCoordinates(v0));
                faceAbsVerts.Add(VM.ToAbsoluteCoordinates(v1));
                faceAbsVerts.Add(VM.ToAbsoluteCoordinates(v2));
            }

            foundElement = new Models.MeasureElement(0, VM.ToAbsoluteCoordinates(weightedCenter), refNormal, totalArea, faceAbsVerts);
            return true;
        }

        return false;
    }

    private static double DistanceToLineSegment(Point p, Point a, Point b)
    {
        Vector ab = b - a;
        double abLenSq = ab.LengthSquared;
        if (abLenSq < 1e-8) return (p - a).Length;
        double t = Vector.Multiply(p - a, ab) / abLenSq;
        t = Math.Clamp(t, 0.0, 1.0);
        Point proj = a + ab * t;
        return (p - proj).Length;
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
        UpdateCalloutPosition();
    }

    private void OnResetViewClick(object sender, RoutedEventArgs e)
    {
        ResetCamera();
    }

    private void OnRequestStandardView(StandardViewType viewType) => SetStandardView(viewType);
    private void OnRequestZoomToFit() => ZoomToFit();

    public void SetStandardView(StandardViewType viewType)
    {
        if (VM == null) return;
        VM.CurrentStandardView = viewType;

        if (viewType == StandardViewType.NormalTo)
        {
            ApplyNormalToView();
            return;
        }

        _target = new Point3D(0, 0, 0);

        switch (viewType)
        {
            case StandardViewType.Isometric:
                _theta = 45.0;
                _phi = 55.0;
                UpdateCameraPosition();
                break;

            case StandardViewType.Front: // 前视 / 主视 (XY 面，朝 -Z 观察)
                _theta = 0.0;
                _phi = 90.0;
                UpdateCameraPosition();
                break;

            case StandardViewType.Back: // 后视 (XY 面，朝 +Z 观察)
                _theta = 180.0;
                _phi = 90.0;
                UpdateCameraPosition();
                break;

            case StandardViewType.Left: // 左视 (YZ 面，朝 +X 观察)
                _theta = -90.0;
                _phi = 90.0;
                UpdateCameraPosition();
                break;

            case StandardViewType.Right: // 右视 (YZ 面，朝 -X 观察)
                _theta = 90.0;
                _phi = 90.0;
                UpdateCameraPosition();
                break;

            case StandardViewType.Top: // 俯视 / 上视 (自 +Y 向下看，XZ 面)
                _theta = 0.0;
                _phi = 0.0001;
                Camera.Position = new Point3D(_target.X, _target.Y + _cameraDistance, _target.Z);
                Camera.LookDirection = new Vector3D(0, -_cameraDistance, 0);
                Camera.UpDirection = new Vector3D(0, 0, -1);
                Camera.Width = _cameraWidth;
                UpdateCalloutPosition();
                break;

            case StandardViewType.Bottom: // 仰视 / 下视 (自 -Y 向上看)
                _theta = 0.0;
                _phi = 179.9999;
                Camera.Position = new Point3D(_target.X, _target.Y - _cameraDistance, _target.Z);
                Camera.LookDirection = new Vector3D(0, _cameraDistance, 0);
                Camera.UpDirection = new Vector3D(0, 0, 1);
                Camera.Width = _cameraWidth;
                UpdateCalloutPosition();
                break;
        }
    }

    private static Vector3D? GetElementNormal(MeasureElement? elem)
    {
        if (elem == null) return null;
        if (elem.Type == MeasureElementType.Face && elem.FaceNormal.LengthSquared > 1e-6)
            return elem.FaceNormal;
        if (elem.Type == MeasureElementType.Circle && elem.AxisDirection.LengthSquared > 1e-6)
            return elem.AxisDirection;
        return null;
    }

    private void ApplyNormalToView()
    {
        Vector3D? normal = GetElementNormal(VM?.SelectedElement1)
                        ?? GetElementNormal(VM?.SelectedElement2)
                        ?? GetElementNormal(VM?.HoveredElement);

        if (!normal.HasValue || normal.Value.LengthSquared < 1e-6)
        {
            SetStandardView(StandardViewType.Isometric);
            return;
        }

        var n = normal.Value;
        n.Normalize();

        Camera.Position = new Point3D(_target.X + n.X * _cameraDistance, _target.Y + n.Y * _cameraDistance, _target.Z + n.Z * _cameraDistance);
        Camera.LookDirection = new Vector3D(-n.X * _cameraDistance, -n.Y * _cameraDistance, -n.Z * _cameraDistance);

        Vector3D up = Math.Abs(n.Y) < 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(0, 0, -1);
        Vector3D right = Vector3D.CrossProduct(Camera.LookDirection, up);
        if (right.LengthSquared < 1e-6) right = new Vector3D(1, 0, 0);
        right.Normalize();
        Camera.UpDirection = Vector3D.CrossProduct(right, Camera.LookDirection);
        Camera.UpDirection.Normalize();

        _phi = Math.Acos(Math.Clamp(n.Y, -1.0, 1.0)) * 180.0 / Math.PI;
        _theta = Math.Atan2(n.X, n.Z) * 180.0 / Math.PI;

        UpdateCalloutPosition();
    }

    public void ZoomToFit()
    {
        _target = new Point3D(0, 0, 0);
        if (VM?.Mesh != null && VM.Mesh.VertexCount > 0)
        {
            var size = VM.Mesh.Max - VM.Mesh.Min;
            double maxDim = Math.Max(size.X, Math.Max(size.Y, size.Z));
            if (maxDim <= 0.001) maxDim = 100.0;
            _cameraDistance = Math.Max(maxDim * 2.5, 100.0);
            _cameraWidth = maxDim * 1.8;
        }
        UpdateCameraPosition();
    }

    private void OnRequestZoomToComponent()
    {
        if (VM?.SelectedAssemblyComponent == null || VM.Mesh == null) return;
        var (center, min, max, _) = VM.FindComponentBounds(VM.SelectedAssemblyComponent);
        _target = new Point3D(center.X, center.Y, center.Z);
        var size = max - min;
        double maxDim = Math.Max(size.X, Math.Max(size.Y, size.Z));
        if (maxDim > 0.01)
        {
            _cameraWidth = maxDim * 2.2;
        }
        UpdateCameraPosition();
    }

    private void UpdateComponentHighlightVisual()
    {
        ComponentHighlightVisual.Content = null;
        if (VM?.WpfComponentHighlightMesh == null) return;

        var matGroup = new MaterialGroup();
        matGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(140, 2, 132, 199))));
        matGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(90, 56, 189, 248))));

        var model = new GeometryModel3D
        {
            Geometry = VM.WpfComponentHighlightMesh,
            Material = matGroup,
            BackMaterial = matGroup
        };
        ComponentHighlightVisual.Content = model;
    }

    private void OnViewsButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.ContextMenu != null)
        {
            fe.ContextMenu.PlacementTarget = fe;
            fe.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            fe.ContextMenu.IsOpen = true;
        }
    }

    private void OnDisplayStyleButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.ContextMenu != null)
        {
            fe.ContextMenu.PlacementTarget = fe;
            fe.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            fe.ContextMenu.IsOpen = true;
        }
    }

    private void OnZoomToFitClick(object sender, RoutedEventArgs e)
    {
        ZoomToFit();
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

        // 3D 测量实体拾取判定 (SolidWorks 经典左键点选特征)
        if (e.ChangedButton == MouseButton.Left && VM != null && VM.IsMeasureModeEnabled)
        {
            if (HitTestMeshEntity(_lastMousePos, out var selectedElem) && selectedElem != null)
            {
                VM.HandleSelectElement(selectedElem);
                e.Handled = true;
                return;
            }
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

        // 0. 3D 尺寸测量悬停预高亮与光标切换
        if (VM?.IsMeasureModeEnabled == true && !_isRotating && !_isPanning && !_isZooming && !_isDraggingSection)
        {
            if (HitTestMeshEntity(currentPos, out var hoverElem) && hoverElem != null)
            {
                Mouse.OverrideCursor = Cursors.Cross;
                VM.HandleHoverElement(hoverElem);
                HoverTooltip.Visibility = Visibility.Visible;
                HoverTooltipText.Text = hoverElem.ToString();
                HoverTooltip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double tw = HoverTooltip.DesiredSize.Width;
                double th = HoverTooltip.DesiredSize.Height;
                Canvas.SetLeft(HoverTooltip, Math.Clamp(currentPos.X + 16, 5, ViewportContainer.ActualWidth - tw - 5));
                Canvas.SetTop(HoverTooltip, Math.Clamp(currentPos.Y + 16, 5, ViewportContainer.ActualHeight - th - 5));
            }
            else
            {
                if (Mouse.OverrideCursor == Cursors.Cross) Mouse.OverrideCursor = null;
                VM.HandleHoverElement(null);
                HoverTooltip.Visibility = Visibility.Collapsed;
            }
        }

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
            UpdateCalloutPosition();
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
        if (Mouse.OverrideCursor == Cursors.Cross) Mouse.OverrideCursor = null;
        HoverTooltip.Visibility = Visibility.Collapsed;
        if (VM?.IsMeasureModeEnabled == true) VM.HandleHoverElement(null);
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
        UpdateCalloutPosition();
        e.Handled = true;
    }
}