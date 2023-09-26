﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CanvasTests
{
    //Points="-5,350 100,320 120,310 160,330 180,320 650,320 650,500 -5,500"
    public class Point2d
    {
        public Point2d(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }

        public Point2d()
        {
            this.X = 0;
            this.Y = 0;
        }

        public double X { get; set; }
        public double Y { get; set; }

        public double Distance(Point2d point)
        {
            var dx = this.X - point.X;
            var dy = this.Y - point.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public double Distance(Point point)
        {
            var dx = this.X - point.X;
            var dy = this.Y - point.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public Point2d Sub(Point2d p2)
        {
            return new Point2d(this.X - p2.X, this.Y - p2.Y);
        }

        static public implicit operator Point(Point2d point)
        {
            return new Point(point.X, point.Y);
        }

        public static implicit operator Point2d(Point v)
        {
            return new Point2d(v.X, v.Y);
        }

        public override string ToString()
        {
            return $"{this.X} {this.Y}";
        }
    }

    public class TopoLineProfile
    {
        public List<Point2d> Points { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool pressed = false;
        private bool renderDragCursor;
        private Point2d cursorPos = new Point2d();

        private Shape selectedShape;
        private Point2d selectedShapePos;
        private bool isPanning;
        private Point2d prevPanning = new Point2d();
        private Point2d panningOffset = new Point2d();

        private float zoom = 1;

        private TopoLineProfile topography = new TopoLineProfile();
        private double pointSize = 8;
        private Point2d dragCursorWorld = new Point2d();
        private Point2d prevPointScreen = new Point2d();

        public MainWindow()
        {
            InitializeComponent();

            topography.Points = new List<Point2d>
            {
                new Point2d(-5, 350),
                new Point2d(100, 320),
                new Point2d(120, 310),
                new Point2d(160, 330),
                new Point2d(180, 320),
                new Point2d(650, 320),
                new Point2d(650, 500),
                new Point2d(-5, 500),
            };

            this.Update();
        }

        private bool IsCloseTo(Point2d p0, Point2d p1, double eps = 8)
        {
            if (Math.Abs(p0.X - p1.X) > eps) return false;
            if (Math.Abs(p0.Y - p1.Y) > eps) return false;

            return true;
        }

        private double ScreenToWorld(double n, double panningDiff, float zoom)
        {
            return (n - panningDiff) * zoom;
        }

        private double WorldToScreen(double n, double panningDiff, float zoom)
        {
            return (n / zoom) + panningDiff;
        }

        private Point2d ScreenToWorld(Point p, Point panningDiff, float zoom)
        {
            var x = ScreenToWorld(p.X, panningDiff.X, zoom);
            var y = ScreenToWorld(p.Y, panningDiff.Y, zoom);
            return new Point2d(x, y);
        }

        private Point2d WorldToScreen(Point p, Point panningDiff, float zoom)
        {
            var x = WorldToScreen(p.X, panningDiff.X, zoom);
            var y = WorldToScreen(p.Y, panningDiff.Y, zoom);
            return new Point2d(x, y);
        }

        private void Update()
        {
            Canvas.Children.Clear();

            this.Canvas.Children.Add(this.Terrain);

            var transformedPoints = new List<Point>();
            foreach (var point in topography.Points)
            {
                var transfPoint = this.WorldToScreen(point, this.panningOffset, this.zoom);
                transformedPoints.Add(transfPoint);
            }
            this.Terrain.Points = new PointCollection(transformedPoints);


            foreach (Point2d point in topography.Points)
            {
                var transfPoint = this.WorldToScreen(point, this.panningOffset, zoom);
                var rendererPoint = this.CreateRendererPoint(transfPoint, point, this.pointSize);
                this.Canvas.Children.Add(rendererPoint);
            }

            if (renderDragCursor)
            {
                var transfPoint = this.WorldToScreen(this.dragCursorWorld, this.panningOffset, this.zoom);
                var dragPoint = this.CreateRendererPoint(transfPoint, this.dragCursorWorld, this.pointSize);
                dragPoint.Tag = "drag";
                this.Canvas.Children.Add(dragPoint);
            }
        }

        public Ellipse CreateRendererPoint(Point2d rendererPoint, Point2d originalPoint, double pointSize)
        {
            var ellipse = new Ellipse();

            ellipse.StrokeThickness = 4;
            ellipse.Stroke = new SolidColorBrush(Colors.Transparent);
            ellipse.Fill = new SolidColorBrush(Colors.Black);
            ellipse.Width = pointSize;
            ellipse.Height = pointSize;
            ellipse.SetValue(Canvas.LeftProperty, rendererPoint.X);
            ellipse.SetValue(Canvas.TopProperty, rendererPoint.Y);
            ellipse.Margin = new Thickness(-pointSize / 2);

            ellipse.ToolTip = $"Position: {originalPoint.X} {originalPoint.Y}";
            ToolTipService.SetBetweenShowDelay(ellipse, 0);
            ToolTipService.SetInitialShowDelay(ellipse, 0);
            ToolTipService.SetShowDuration(ellipse, 999999);

            ellipse.MouseDown += Window_MouseDown;
            ellipse.MouseUp += Window_MouseUp;
            ellipse.MouseEnter += Polygon_MouseEnter;
            ellipse.MouseLeave += Polygon_MouseLeave;

            return ellipse;
        }

        private void Polygon_MouseEnter(object sender, MouseEventArgs e)
        {
            if (e.OriginalSource is Shape shape)
            {
                shape.Stroke = new SolidColorBrush(Colors.Black);
            }
        }

        private void Polygon_MouseLeave(object sender, MouseEventArgs e)
        {
            if (e.OriginalSource is Shape shape)
            {
                shape.Stroke = new SolidColorBrush(Colors.Transparent);
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.isPanning = false;
            this.Cursor = Cursors.Arrow;
            this.selectedShape = null;
            this.pressed = false;
            this.renderDragCursor = false;
            this.Update();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            this.cursorPos = e.GetPosition(this);

            if (this.pressed)
            {
                this.dragCursorWorld = this.ScreenToWorld(this.cursorPos, this.panningOffset, zoom);
                if (this.selectedShape != null)
                {
                    this.selectedShapePos.X = this.dragCursorWorld.X;
                    this.selectedShapePos.Y = this.dragCursorWorld.Y;
                }
                this.Update();
            }


            if (this.isPanning)
            {
                this.Pan();
            }

            Debug.WriteLine("Mouse moved");
        }

        private void Pan()
        {
            this.Update();
            this.panningOffset.X += this.cursorPos.X - this.prevPanning.X;
            this.panningOffset.Y += this.cursorPos.Y - this.prevPanning.Y;
            this.prevPanning = this.cursorPos;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            this.selectedShape = null;
            this.pressed = false;
            this.isPanning = false;
            this.Cursor = Cursors.Arrow;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                this.isPanning = true;
                this.prevPanning = e.GetPosition(this);
                this.Cursor = Cursors.SizeAll;

                if (e.ClickCount == 2)
                {
                    Debug.WriteLine("Zoom to fit");
                }
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.cursorPos = e.GetPosition(this);
                this.pressed = true;
                this.dragCursorWorld = this.ScreenToWorld(this.cursorPos, this.panningOffset, zoom);
                //this.renderDragCursor = true;
                this.Update();

                if (e.OriginalSource is Ellipse shape)
                {
                    if (shape.Tag?.ToString() != "drag")
                    {
                        this.selectedShape = shape;
                        this.selectedShapePos = this.topography.Points.OrderBy(p => p.Distance(dragCursorWorld)).FirstOrDefault();
                    }
                }
            }

        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) 
            {
                if (zoom < 0.01) return;
                zoom *= 0.95f;
            }
            else
            {
                zoom *= 1.05f;
            }

            //Debug.WriteLine("Zoom");

            this.Update();

            var ellipe1 = this.CreateRendererPoint(this.prevPointScreen, prevPointScreen, this.pointSize);
            ellipe1.IsEnabled = false;
            ellipe1.Fill = new SolidColorBrush(Colors.Red);
            this.Canvas.Children.Add(ellipe1);

            this.prevPointScreen = this.cursorPos;
        }
    }
}
