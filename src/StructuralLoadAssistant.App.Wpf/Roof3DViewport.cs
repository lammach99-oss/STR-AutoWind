 using System.Collections.Immutable;
 using System.Windows;
 using System.Windows.Controls;
 using System.Windows.Input;
 using System.Windows.Media;
 using System.Windows.Media.Media3D;
 using StructuralLoadAssistant.Domain;
 using StructuralLoadAssistant.RoofMapping;
 
 namespace StructuralLoadAssistant.App.Wpf;
 
 public sealed class Roof3DViewport : Border
 {
     private readonly Viewport3D viewport = new();
     private readonly ModelVisual3D rootVisual = new();
     private readonly ModelVisual3D lightVisual = new();
     private readonly ModelVisual3D meshVisual = new();
     private readonly ModelVisual3D arrowVisual = new();
     private readonly PerspectiveCamera camera = new();
 
     private Point lastMousePos;
     private bool isDragging;
     private double cameraDistance = 25.0;
     private double cameraTheta = 45.0; // horizontal angle in degrees
     private double cameraPhi = 35.0;   // vertical angle in degrees
     private Point3D targetCenter = new(0, 0, 0);
 
     public event Action<string>? ShellClicked;
     public bool ShowLocal3Arrows { get; set; } = true;
 
     private readonly Dictionary<GeometryModel3D, string> modelToShellId = new();
 
     public Roof3DViewport()
     {
         Background = new SolidColorBrush(Color.FromRgb(240, 243, 246));
         BorderBrush = Brushes.LightGray;
         BorderThickness = new Thickness(1);
         ClipToBounds = true;
 
         camera.FieldOfView = 45;
         viewport.Camera = camera;
         viewport.Children.Add(rootVisual);
 
         rootVisual.Children.Add(lightVisual);
         rootVisual.Children.Add(meshVisual);
         rootVisual.Children.Add(arrowVisual);
 
         SetupLights();
         UpdateCamera();
 
         Child = viewport;
 
         viewport.MouseLeftButtonDown += OnMouseDown;
         viewport.MouseMove += OnMouseMove;
         viewport.MouseLeftButtonUp += OnMouseUp;
         viewport.MouseWheel += OnMouseWheel;
     }
 
     private void SetupLights()
     {
         var group = new Model3DGroup();
         group.Children.Add(new AmbientLight(Color.FromRgb(120, 120, 120)));
         group.Children.Add(new DirectionalLight(Color.FromRgb(200, 200, 200), new Vector3D(-1, -2, -3)));
         group.Children.Add(new DirectionalLight(Color.FromRgb(80, 80, 80), new Vector3D(1, 2, 1)));
         lightVisual.Content = group;
     }
 
     public void ResetCamera()
     {
         cameraTheta = 45.0;
         cameraPhi = 35.0;
         cameraDistance = 25.0;
         UpdateCamera();
     }
 
     public void UpdateGeometry(IReadOnlyList<RoofShell3D> shells, string selectedShellId, IReadOnlyDictionary<string, string>? shellZones = null)
     {
         modelToShellId.Clear();
         var meshGroup = new Model3DGroup();
         var arrowGroup = new Model3DGroup();
 
         if (shells == null || shells.Count == 0)
         {
             meshVisual.Content = meshGroup;
             arrowVisual.Content = arrowGroup;
             return;
         }
 
         // Compute bounding box
         double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
         double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
 
         foreach (var shell in shells)
         {
             foreach (var v in shell.Vertices)
             {
                 if (v.X < minX) minX = v.X;
                 if (v.X > maxX) maxX = v.X;
                 if (v.Y < minY) minY = v.Y;
                 if (v.Y > maxY) maxY = v.Y;
                 if (v.Z < minZ) minZ = v.Z;
                 if (v.Z > maxZ) maxZ = v.Z;
             }
         }
 
         targetCenter = new Point3D((minX + maxX) * 0.5, (minY + maxY) * 0.5, (minZ + maxZ) * 0.5);
         double span = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
         if (span > 1e-4) cameraDistance = Math.Max(10, span * 2.2);
 
         foreach (var shell in shells)
         {
             if (shell.Vertices.Length < 3) continue;
 
             var mesh = new MeshGeometry3D();
             foreach (var v in shell.Vertices)
             {
                 mesh.Positions.Add(new Point3D(v.X, v.Y, v.Z));
             }
 
             var indices = shell.TriangleIndices;
             if (indices.IsDefaultOrEmpty || indices.Length == 0)
             {
                 // Simple fan
                 for (int i = 1; i < shell.Vertices.Length - 1; i++)
                 {
                     mesh.TriangleIndices.Add(0);
                     mesh.TriangleIndices.Add(i);
                     mesh.TriangleIndices.Add(i + 1);
                 }
             }
             else
             {
                 foreach (int idx in indices) mesh.TriangleIndices.Add(idx);
             }
 
             bool isSelected = shell.ShellId == selectedShellId;
             Color baseColor = GetShellColor(shell, shellZones);
             if (isSelected)
             {
                 baseColor = Color.FromRgb(255, 200, 50); // Vibrant highlight
             }
 
             var brush = new SolidColorBrush(baseColor);
             var material = new MaterialGroup();
             material.Children.Add(new DiffuseMaterial(brush));
             if (isSelected)
             {
                 material.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(80, 60, 0))));
             }
 
             // Two-sided material so underside and reversed shells are rendered properly
             var backBrush = new SolidColorBrush(Color.FromRgb((byte)(baseColor.R * 0.8), (byte)(baseColor.G * 0.8), (byte)(baseColor.B * 0.8)));
             var backMaterial = new DiffuseMaterial(backBrush);
 
             var geomModel = new GeometryModel3D(mesh, material)
             {
                 BackMaterial = backMaterial
             };
 
             meshGroup.Children.Add(geomModel);
             modelToShellId[geomModel] = shell.ShellId;
 
             // Local 3 arrows
             if (ShowLocal3Arrows && shell.Local3.Length > 0.5)
             {
                 var centroid = new Point3D(
                     shell.Vertices.Average(v => v.X),
                     shell.Vertices.Average(v => v.Y),
                     shell.Vertices.Average(v => v.Z));
 
                 double arrowLen = Math.Max(0.5, span * 0.08);
                 var arrowTip = new Point3D(
                     centroid.X + shell.Local3.X * arrowLen,
                     centroid.Y + shell.Local3.Y * arrowLen,
                     centroid.Z + shell.Local3.Z * arrowLen);
 
                 var arrowModel = CreateArrow(centroid, arrowTip, isSelected ? Colors.Red : Colors.DarkBlue);
                 arrowGroup.Children.Add(arrowModel);
             }
         }
 
         meshVisual.Content = meshGroup;
         arrowVisual.Content = arrowGroup;
         UpdateCamera();
     }
 
     private static Color GetShellColor(RoofShell3D shell, IReadOnlyDictionary<string, string>? shellZones)
     {
         if (shellZones != null && shellZones.TryGetValue(shell.ShellId, out var zone) && !string.IsNullOrWhiteSpace(zone))
         {
             if (zone.Contains("Edge", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(121, 201, 188); // Teal
             if (zone.Contains("Corner", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(235, 130, 110); // Coral
             if (zone.Contains("Field", StringComparison.OrdinalIgnoreCase) || zone.Contains("Interior", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(167, 188, 233); // Blue
             if (zone.Contains("Exclude", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(180, 180, 180); // Gray
             if (!zone.Equals("Unmapped", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(170, 210, 150);
         }
 
         // Color based on plane hash or default warm amber
         int hash = Math.Abs(shell.RoofPlaneId.GetHashCode());
         byte r = (byte)(180 + (hash % 55));
         byte g = (byte)(180 + ((hash / 10) % 50));
         byte b = (byte)(190 + ((hash / 100) % 50));
         return Color.FromRgb(r, g, b);
     }
 
     private static GeometryModel3D CreateArrow(Point3D start, Point3D end, Color color)
     {
         var mesh = new MeshGeometry3D();
         Vector3D dir = end - start;
         double len = dir.Length;
         if (len < 1e-6) return new GeometryModel3D();
 
         dir.Normalize();
         Vector3D up = Math.Abs(dir.Z) < 0.9 ? new Vector3D(0, 0, 1) : new Vector3D(1, 0, 0);
         Vector3D right = Vector3D.CrossProduct(dir, up);
         right.Normalize();
         up = Vector3D.CrossProduct(right, dir);
         up.Normalize();
 
         double shaftRadius = len * 0.03;
         int segments = 6;
         for (int i = 0; i < segments; i++)
         {
             double angle1 = 2 * Math.PI * i / segments;
             double angle2 = 2 * Math.PI * (i + 1) / segments;
 
             Vector3D offset1 = (right * Math.Cos(angle1) + up * Math.Sin(angle1)) * shaftRadius;
             Vector3D offset2 = (right * Math.Cos(angle2) + up * Math.Sin(angle2)) * shaftRadius;
 
             int bIdx = mesh.Positions.Count;
             mesh.Positions.Add(start + offset1);
             mesh.Positions.Add(start + offset2);
             mesh.Positions.Add(end + offset2);
             mesh.Positions.Add(end + offset1);
 
             mesh.TriangleIndices.Add(bIdx);
             mesh.TriangleIndices.Add(bIdx + 1);
             mesh.TriangleIndices.Add(bIdx + 2);
             mesh.TriangleIndices.Add(bIdx);
             mesh.TriangleIndices.Add(bIdx + 2);
             mesh.TriangleIndices.Add(bIdx + 3);
         }
 
         var mat = new DiffuseMaterial(new SolidColorBrush(color));
         return new GeometryModel3D(mesh, mat);
     }
 
     private void UpdateCamera()
     {
         double radTheta = cameraTheta * Math.PI / 180.0;
         double radPhi = cameraPhi * Math.PI / 180.0;
 
         double x = targetCenter.X + cameraDistance * Math.Cos(radPhi) * Math.Sin(radTheta);
         double y = targetCenter.Y - cameraDistance * Math.Cos(radPhi) * Math.Cos(radTheta);
         double z = targetCenter.Z + cameraDistance * Math.Sin(radPhi);
 
         camera.Position = new Point3D(x, y, z);
         camera.LookDirection = targetCenter - camera.Position;
         camera.UpDirection = new Vector3D(0, 0, 1);
     }
 
     private void OnMouseDown(object sender, MouseButtonEventArgs e)
     {
         if (e.LeftButton == MouseButtonState.Pressed)
         {
             lastMousePos = e.GetPosition(this);
             isDragging = true;
             viewport.CaptureMouse();
 
             // Hit test for shell selection
             var hit = VisualTreeHelper.HitTest(viewport, lastMousePos) as RayMeshGeometry3DHitTestResult;
             if (hit?.ModelHit is GeometryModel3D geom && modelToShellId.TryGetValue(geom, out var shellId))
             {
                 ShellClicked?.Invoke(shellId);
             }
         }
     }
 
     private void OnMouseMove(object sender, MouseEventArgs e)
     {
         if (isDragging && e.LeftButton == MouseButtonState.Pressed)
         {
             var currentPos = e.GetPosition(this);
             double deltaX = currentPos.X - lastMousePos.X;
             double deltaY = currentPos.Y - lastMousePos.Y;
 
             cameraTheta += deltaX * 0.5;
             cameraPhi = Math.Clamp(cameraPhi + deltaY * 0.5, -85.0, 85.0);
 
             UpdateCamera();
             lastMousePos = currentPos;
         }
     }
 
     private void OnMouseUp(object sender, MouseButtonEventArgs e)
     {
         if (isDragging)
         {
             isDragging = false;
             viewport.ReleaseMouseCapture();
         }
     }
 
     private void OnMouseWheel(object sender, MouseWheelEventArgs e)
     {
         double factor = e.Delta > 0 ? 0.88 : 1.14;
         cameraDistance = Math.Clamp(cameraDistance * factor, 2.0, 200.0);
         UpdateCamera();
     }
 }
 
