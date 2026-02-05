using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AttentionLooper.Controls;

public partial class TimeInputControl : UserControl
{
    private string _digitBuffer = "";
    private bool _cursorVisible;
    private readonly DispatcherTimer _cursorTimer;

    public static readonly DependencyProperty TimeSpanValueProperty =
        DependencyProperty.Register(
            nameof(TimeSpanValue),
            typeof(TimeSpan?),
            typeof(TimeInputControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTimeSpanValueChanged));

    public TimeSpan? TimeSpanValue
    {
        get => (TimeSpan?)GetValue(TimeSpanValueProperty);
        set => SetValue(TimeSpanValueProperty, value);
    }

    public TimeInputControl()
    {
        InitializeComponent();

        _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _cursorTimer.Tick += (_, _) =>
        {
            _cursorVisible = !_cursorVisible;
            UpdateDisplay();
        };

        UpdateDisplay();
    }

    private static void OnTimeSpanValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TimeInputControl)d;
        if (e.NewValue is TimeSpan ts)
        {
            int totalSeconds = (int)ts.TotalSeconds;
            int h = totalSeconds / 3600;
            int m = (totalSeconds % 3600) / 60;
            int s = totalSeconds % 60;
            var digits = $"{h:D2}{m:D2}{s:D2}".TrimStart('0');
            control._digitBuffer = digits;
        }
        else
        {
            control._digitBuffer = "";
        }
        control.UpdateDisplay();
    }

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        _cursorVisible = true;
        _cursorTimer.Start();
        UpdateDisplay();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        _cursorTimer.Stop();
        _cursorVisible = false;
        UpdateDisplay();
    }

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        e.Handled = true;
        foreach (char c in e.Text)
        {
            if (c >= '0' && c <= '9' && _digitBuffer.Length < 6)
            {
                _digitBuffer += c;
            }
        }
        ResetCursorBlink();
        UpdateDisplay();
        PushValueToBinding();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Back)
        {
            e.Handled = true;
            if (_digitBuffer.Length > 0)
            {
                _digitBuffer = _digitBuffer[..^1];
            }
            ResetCursorBlink();
            UpdateDisplay();
            PushValueToBinding();
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            _digitBuffer = "";
            ResetCursorBlink();
            UpdateDisplay();
            PushValueToBinding();
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        e.Handled = true;
    }

    private void ResetCursorBlink()
    {
        _cursorVisible = true;
        _cursorTimer.Stop();
        _cursorTimer.Start();
    }

    private void UpdateDisplay()
    {
        var padded = _digitBuffer.PadLeft(6, '_');
        var chars = new char[8];
        chars[0] = padded[0];
        chars[1] = padded[1];
        chars[2] = ':';
        chars[3] = padded[2];
        chars[4] = padded[3];
        chars[5] = ':';
        chars[6] = padded[4];
        chars[7] = padded[5];

        // When focused, replace the first underscore from the right with a blinking cursor
        if (IsFocused && _digitBuffer.Length < 6)
        {
            // Find the cursor position: the rightmost empty slot
            // In the padded string, underscores are at indices 0..(5 - _digitBuffer.Length)
            // The cursor goes at index (5 - _digitBuffer.Length) in padded, which maps to a chars index
            int padIdx = 5 - _digitBuffer.Length;
            // Map padded index to chars index (accounting for colons at chars[2] and chars[5])
            int charIdx = padIdx < 2 ? padIdx : padIdx < 4 ? padIdx + 1 : padIdx + 2;
            chars[charIdx] = _cursorVisible ? '_' : ' ';
        }

        DisplayText.Text = new string(chars);
    }

    private void PushValueToBinding()
    {
        if (_digitBuffer.Length == 0)
        {
            TimeSpanValue = null;
            return;
        }

        var padded = _digitBuffer.PadLeft(6, '0');
        int h = int.Parse(padded[..2]);
        int m = int.Parse(padded[2..4]);
        int s = int.Parse(padded[4..6]);

        TimeSpanValue = new TimeSpan(h, m, s);
    }
}
