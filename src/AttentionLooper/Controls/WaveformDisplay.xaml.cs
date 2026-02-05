using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AttentionLooper.Controls;

public partial class WaveformDisplay : UserControl
{
    public static readonly DependencyProperty PeaksProperty =
        DependencyProperty.Register(nameof(Peaks), typeof(float[]), typeof(WaveformDisplay),
            new PropertyMetadata(null, OnPeaksChanged));

    public float[]? Peaks
    {
        get => (float[]?)GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public WaveformDisplay()
    {
        InitializeComponent();
    }

    private static void OnPeaksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((WaveformDisplay)d).Render();
    }

    private void WaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Render();
    }

    private void Render()
    {
        WaveformCanvas.Children.Clear();

        var peaks = Peaks;
        if (peaks == null || peaks.Length == 0) return;

        double width = WaveformCanvas.ActualWidth;
        double height = WaveformCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double midY = height / 2.0;
        double barWidth = width / peaks.Length;

        var fillBrush = TryFindResource("WaveformBrush") as SolidColorBrush
            ?? new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x8A));
        var glowBrush = TryFindResource("WaveformGlowBrush") as SolidColorBrush
            ?? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x5C));

        // Build upper and lower point collections for the mirrored waveform
        var upperPoints = new PointCollection(peaks.Length + 2);
        var lowerPoints = new PointCollection(peaks.Length + 2);

        for (int i = 0; i < peaks.Length; i++)
        {
            double x = i * barWidth + barWidth / 2.0;
            double peakHeight = peaks[i] * midY * 0.9;

            upperPoints.Add(new Point(x, midY - peakHeight));
            lowerPoints.Add(new Point(x, midY + peakHeight));
        }

        // Close the upper polygon
        var upperPolygon = new Polygon
        {
            Fill = fillBrush,
            Opacity = 0.8,
            Points = new PointCollection(upperPoints.Count + 2)
        };
        upperPolygon.Points.Add(new Point(0, midY));
        foreach (var p in upperPoints) upperPolygon.Points.Add(p);
        upperPolygon.Points.Add(new Point(width, midY));

        // Close the lower polygon
        var lowerPolygon = new Polygon
        {
            Fill = glowBrush,
            Opacity = 0.6,
            Points = new PointCollection(lowerPoints.Count + 2)
        };
        lowerPolygon.Points.Add(new Point(0, midY));
        foreach (var p in lowerPoints) lowerPolygon.Points.Add(p);
        lowerPolygon.Points.Add(new Point(width, midY));

        WaveformCanvas.Children.Add(upperPolygon);
        WaveformCanvas.Children.Add(lowerPolygon);

        // Center line
        var centerLine = new Line
        {
            X1 = 0, Y1 = midY,
            X2 = width, Y2 = midY,
            Stroke = fillBrush,
            StrokeThickness = 0.5,
            Opacity = 0.4
        };
        WaveformCanvas.Children.Add(centerLine);
    }
}
