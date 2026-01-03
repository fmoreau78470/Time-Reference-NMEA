using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TimeReference.App
{
    public partial class GaugeControl : UserControl
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(GaugeControl), new PropertyMetadata(0.0, OnValueChanged));
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(double), typeof(GaugeControl), new PropertyMetadata(0.0, OnPropertyChanged));
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(double), typeof(GaugeControl), new PropertyMetadata(100.0, OnPropertyChanged));
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(GaugeControl), new PropertyMetadata("TITLE", (d, e) => ((GaugeControl)d).TxtTitle.Text = (string)e.NewValue));
        public static readonly DependencyProperty UnitProperty = DependencyProperty.Register("Unit", typeof(string), typeof(GaugeControl), new PropertyMetadata("", (d, e) => ((GaugeControl)d).TxtUnit.Text = (string)e.NewValue));
        
        // Limits for zones (Limit1 is boundary between Zone1/2, Limit2 between Zone2/3)
        public static readonly DependencyProperty Limit1Property = DependencyProperty.Register("Limit1", typeof(double), typeof(GaugeControl), new PropertyMetadata(33.0, OnPropertyChanged));
        public static readonly DependencyProperty Limit2Property = DependencyProperty.Register("Limit2", typeof(double), typeof(GaugeControl), new PropertyMetadata(66.0, OnPropertyChanged));
        
        // If true: Green -> Orange -> Red (e.g. HDOP). If false: Red -> Orange -> Green (e.g. SNR)
        public static readonly DependencyProperty IsInvertedProperty = DependencyProperty.Register("IsInverted", typeof(bool), typeof(GaugeControl), new PropertyMetadata(false, OnPropertyChanged));

        public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
        public double Limit1 { get => (double)GetValue(Limit1Property); set => SetValue(Limit1Property, value); }
        public double Limit2 { get => (double)GetValue(Limit2Property); set => SetValue(Limit2Property, value); }
        public bool IsInverted { get => (bool)GetValue(IsInvertedProperty); set => SetValue(IsInvertedProperty, value); }

        private const double StartAngle = 135;
        private const double EndAngle = 405;
        private const double Radius = 80;
        private const double StrokeThickness = 12;
        private const double CenterX = 100;
        private const double CenterY = 100;

        public GaugeControl()
        {
            InitializeComponent();
            Loaded += (s, e) => DrawGauge();
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (GaugeControl)d;
            ctrl.UpdateNeedle();
            ctrl.TxtValue.Text = ctrl.Value.ToString("F1");
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GaugeControl)d).DrawGauge();
        }

        private void UpdateNeedle()
        {
            double range = Maximum - Minimum;
            if (range <= 0) return;

            double val = Math.Clamp(Value, Minimum, Maximum);
            double percent = (val - Minimum) / range;
            double totalAngle = EndAngle - StartAngle;
            double angle = StartAngle + (percent * totalAngle);

            NeedleRotate.Angle = angle;
        }

        private void DrawGauge()
        {
            ArcCanvas.Children.Clear();

            // Colors
            Brush c1 = IsInverted ? Brushes.LimeGreen : Brushes.Red;
            Brush c2 = Brushes.Orange;
            Brush c3 = IsInverted ? Brushes.Red : Brushes.LimeGreen;

            // Calculate angles for limits
            double range = Maximum - Minimum;
            if (range <= 0) return;

            double angleStart = StartAngle;
            double angleLim1 = ValueToAngle(Limit1);
            double angleLim2 = ValueToAngle(Limit2);
            double angleEnd = EndAngle;

            // Draw 3 arcs
            DrawArcSegment(angleStart, angleLim1, c1);
            DrawArcSegment(angleLim1, angleLim2, c2);
            DrawArcSegment(angleLim2, angleEnd, c3);

            UpdateNeedle();
        }

        private double ValueToAngle(double val)
        {
            double range = Maximum - Minimum;
            double clamped = Math.Clamp(val, Minimum, Maximum);
            double percent = (clamped - Minimum) / range;
            return StartAngle + (percent * (EndAngle - StartAngle));
        }

        private void DrawArcSegment(double startAngle, double endAngle, Brush color)
        {
            if (Math.Abs(endAngle - startAngle) < 0.1) return;

            Path path = new Path
            {
                Stroke = color,
                StrokeThickness = StrokeThickness,
                StrokeEndLineCap = PenLineCap.Flat,
                StrokeStartLineCap = PenLineCap.Flat
            };

            PathGeometry geom = new PathGeometry();
            PathFigure fig = new PathFigure();
            
            // Convert angles to radians
            double startRad = (startAngle * Math.PI) / 180.0;
            double endRad = (endAngle * Math.PI) / 180.0;

            Point pStart = new Point(
                CenterX + Radius * Math.Cos(startRad),
                CenterY + Radius * Math.Sin(startRad));

            Point pEnd = new Point(
                CenterX + Radius * Math.Cos(endRad),
                CenterY + Radius * Math.Sin(endRad));

            fig.StartPoint = pStart;
            fig.Segments.Add(new ArcSegment
            {
                Point = pEnd,
                Size = new Size(Radius, Radius),
                IsLargeArc = (endAngle - startAngle) > 180,
                SweepDirection = SweepDirection.Clockwise,
                RotationAngle = 0
            });

            geom.Figures.Add(fig);
            path.Data = geom;
            ArcCanvas.Children.Add(path);
        }
    }
}