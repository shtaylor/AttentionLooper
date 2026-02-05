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

    private static readonly DependencyProperty WaveformFillProperty =
        DependencyProperty.Register(nameof(WaveformFill), typeof(Brush), typeof(WaveformDisplay),
            new PropertyMetadata(null, OnBrushChanged));

    private static readonly DependencyProperty WaveformGlowProperty =
        DependencyProperty.Register(nameof(WaveformGlow), typeof(Brush), typeof(WaveformDisplay),
            new PropertyMetadata(null, OnBrushChanged));

    public static readonly DependencyProperty PlaybackProgressProperty =
        DependencyProperty.Register(nameof(PlaybackProgress), typeof(double), typeof(WaveformDisplay),
            new PropertyMetadata(-1.0, OnPlaybackProgressChanged));

    // Animation constants
    private const double MaxStretch = 0.6;
    private const double Sigma = 0.10;
    private const double TwoSigmaSquared = 2.0 * Sigma * Sigma;
    private const double SpringOmega = 22.0;     // natural frequency (rad/s)
    private const double SpringDamping = 0.35;    // < 1.0 = underdamped (bouncy)
    private const double SettleEpsilon = 0.001;

    // Precomputed spring constants
    private static readonly double SpringSigma = SpringDamping * SpringOmega;
    private static readonly double SpringOmegaD = SpringOmega * Math.Sqrt(1.0 - SpringDamping * SpringDamping);

    // Geometry references
    private Polygon? _upperPolygon;
    private Polygon? _lowerPolygon;
    private Line? _centerLine;

    // Animation state
    private double[] _sliceScales = [];
    private double[] _sliceVelocities = [];
    private bool _isAnimating;
    private TimeSpan _lastFrameTime;

    public float[]? Peaks
    {
        get => (float[]?)GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    private Brush? WaveformFill
    {
        get => (Brush?)GetValue(WaveformFillProperty);
        set => SetValue(WaveformFillProperty, value);
    }

    private Brush? WaveformGlow
    {
        get => (Brush?)GetValue(WaveformGlowProperty);
        set => SetValue(WaveformGlowProperty, value);
    }

    public double PlaybackProgress
    {
        get => (double)GetValue(PlaybackProgressProperty);
        set => SetValue(PlaybackProgressProperty, value);
    }

    public WaveformDisplay()
    {
        InitializeComponent();
        SetResourceReference(WaveformFillProperty, "WaveformBrush");
        SetResourceReference(WaveformGlowProperty, "WaveformGlowBrush");
    }

    private static void OnPeaksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((WaveformDisplay)d).RebuildGeometry();
    }

    private static void OnBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((WaveformDisplay)d).RebuildGeometry();
    }

    private static void OnPlaybackProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (WaveformDisplay)d;
        var oldVal = (double)e.OldValue;
        var newVal = (double)e.NewValue;

        // Start animating when transitioning from idle to active
        if (oldVal < 0 && newVal >= 0)
            self.StartAnimation();
    }

    private void WaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RebuildGeometry();
    }

    private void RebuildGeometry()
    {
        WaveformCanvas.Children.Clear();
        _upperPolygon = null;
        _lowerPolygon = null;
        _centerLine = null;

        var peaks = Peaks;
        if (peaks == null || peaks.Length == 0) return;

        double width = WaveformCanvas.ActualWidth;
        double height = WaveformCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double midY = height / 2.0;
        double barWidth = width / peaks.Length;

        var fillBrush = WaveformFill ?? new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x8A));
        var glowBrush = WaveformGlow ?? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x5C));

        // Initialize scale arrays
        _sliceScales = new double[peaks.Length];
        _sliceVelocities = new double[peaks.Length];
        Array.Fill(_sliceScales, 1.0);

        // Build upper polygon: start at (0, midY), peaks, end at (width, midY)
        var upperPoints = new PointCollection(peaks.Length + 2);
        upperPoints.Add(new Point(0, midY));
        for (int i = 0; i < peaks.Length; i++)
        {
            double x = i * barWidth + barWidth / 2.0;
            double peakHeight = peaks[i] * midY * 0.9;
            upperPoints.Add(new Point(x, midY - peakHeight));
        }
        upperPoints.Add(new Point(width, midY));

        _upperPolygon = new Polygon
        {
            Fill = fillBrush,
            Opacity = 0.8,
            Points = upperPoints
        };

        // Build lower polygon
        var lowerPoints = new PointCollection(peaks.Length + 2);
        lowerPoints.Add(new Point(0, midY));
        for (int i = 0; i < peaks.Length; i++)
        {
            double x = i * barWidth + barWidth / 2.0;
            double peakHeight = peaks[i] * midY * 0.9;
            lowerPoints.Add(new Point(x, midY + peakHeight));
        }
        lowerPoints.Add(new Point(width, midY));

        _lowerPolygon = new Polygon
        {
            Fill = glowBrush,
            Opacity = 0.6,
            Points = lowerPoints
        };

        _centerLine = new Line
        {
            X1 = 0, Y1 = midY,
            X2 = width, Y2 = midY,
            Stroke = fillBrush,
            StrokeThickness = 0.5,
            Opacity = 0.4
        };

        WaveformCanvas.Children.Add(_upperPolygon);
        WaveformCanvas.Children.Add(_lowerPolygon);
        WaveformCanvas.Children.Add(_centerLine);
    }

    private void StartAnimation()
    {
        if (_isAnimating) return;
        _isAnimating = true;
        _lastFrameTime = TimeSpan.Zero;
        CompositionTarget.Rendering += OnCompositionTargetRendering;
    }

    private void StopAnimation()
    {
        if (!_isAnimating) return;
        _isAnimating = false;
        CompositionTarget.Rendering -= OnCompositionTargetRendering;
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        var args = (RenderingEventArgs)e;
        var now = args.RenderingTime;

        if (_lastFrameTime == TimeSpan.Zero)
        {
            _lastFrameTime = now;
            return;
        }

        // Skip duplicate frames (WPF can fire Rendering with same timestamp)
        if (now == _lastFrameTime) return;

        double dt = (now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;

        // Clamp dt to avoid huge jumps (e.g. when window was minimized)
        if (dt > 0.1) dt = 0.1;

        UpdateSliceScales(dt);
    }

    private void UpdateSliceScales(double deltaTime)
    {
        var peaks = Peaks;
        if (peaks == null || peaks.Length == 0) return;
        if (_upperPolygon == null || _lowerPolygon == null) return;
        if (_sliceScales.Length != peaks.Length) return;

        double width = WaveformCanvas.ActualWidth;
        double height = WaveformCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double midY = height / 2.0;
        double barWidth = width / peaks.Length;
        double progress = PlaybackProgress;
        bool isIdle = progress < 0;
        bool allSettled = true;

        var upperPoints = _upperPolygon.Points;
        var lowerPoints = _lowerPolygon.Points;

        for (int i = 0; i < peaks.Length; i++)
        {
            double slicePos = (double)i / peaks.Length;
            double target;

            if (!isIdle)
            {
                double diff = slicePos - progress;
                target = 1.0 + MaxStretch * Math.Exp(-(diff * diff) / TwoSigmaSquared);
            }
            else
            {
                target = 1.0;
            }

            _sliceScales[i] = SpringStep(_sliceScales[i], target, ref _sliceVelocities[i], deltaTime);

            double x = i * barWidth + barWidth / 2.0;
            double peakHeight = peaks[i] * midY * 0.9 * _sliceScales[i];

            // Points[0] is (0, midY), peaks are at indices 1..N, Points[N+1] is (width, midY)
            upperPoints[i + 1] = new Point(x, midY - peakHeight);
            lowerPoints[i + 1] = new Point(x, midY + peakHeight);

            if (Math.Abs(_sliceScales[i] - 1.0) > SettleEpsilon || Math.Abs(_sliceVelocities[i]) > SettleEpsilon)
                allSettled = false;
        }

        if (isIdle && allSettled)
        {
            // Snap to rest
            for (int i = 0; i < peaks.Length; i++)
            {
                _sliceScales[i] = 1.0;
                _sliceVelocities[i] = 0.0;

                double x = i * barWidth + barWidth / 2.0;
                double peakHeight = peaks[i] * midY * 0.9;
                upperPoints[i + 1] = new Point(x, midY - peakHeight);
                lowerPoints[i + 1] = new Point(x, midY + peakHeight);
            }
            StopAnimation();
        }
    }

    /// <summary>
    /// Exact analytical solution for an underdamped harmonic oscillator.
    /// Unconditionally stable regardless of deltaTime.
    /// </summary>
    private static double SpringStep(double current, double target, ref double velocity, double dt)
    {
        double d = current - target;
        double decay = Math.Exp(-SpringSigma * dt);
        double cosW = Math.Cos(SpringOmegaD * dt);
        double sinW = Math.Sin(SpringOmegaD * dt);

        double b = (velocity + SpringSigma * d) / SpringOmegaD;

        double newDisplacement = decay * (d * cosW + b * sinW);
        velocity = decay * ((-SpringSigma * d + SpringOmegaD * b) * cosW
                          + (-SpringSigma * b - SpringOmegaD * d) * sinW);

        return target + newDisplacement;
    }
}
