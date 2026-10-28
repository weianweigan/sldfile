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
            UpdateMesh();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (VM != null)
        {
            VM.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MeshViewerViewModel.WpfMesh))
        {
            UpdateMesh();
        }
    }

    private void UpdateMesh()
    {
        ModelVisual.Content = null;
        if (VM?.WpfMesh == null) return;

        var group = new Model3DGroup();

        // Shaded surface material (CAD metallic paint look)
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

        ResetCamera();
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

        if (e.ChangedButton == MouseButton.Left)
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

        if (_isRotating)
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
            look.Normalize();

            Vector3D right = Vector3D.CrossProduct(look, Camera.UpDirection);
            right.Normalize();

            Vector3D up = Vector3D.CrossProduct(right, look);
            up.Normalize();

            _target += right * (-delta.X * panFactor) + up * (delta.Y * panFactor);
            UpdateCameraPosition();
            _lastMousePos = currentPos;
            e.Handled = true;
        }
    }

    private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) _isRotating = false;
        if (e.ChangedButton == MouseButton.Right) _isPanning = false;

        if (!_isRotating && !_isPanning)
        {
            ViewportContainer.ReleaseMouseCapture();
        }
        e.Handled = true;
    }

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        double factor = e.Delta > 0 ? 0.85 : 1.18;

        // In CAD orthographic projection, zoom changes Camera.Width, not camera distance.
        // Camera distance stays safely outside bounding box so camera never moves inside the model!
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
