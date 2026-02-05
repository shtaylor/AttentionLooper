using System.Globalization;

namespace AttentionLooper.Models;

public static class TimeParser
{
    public static bool TryParseFlexibleTime(string s, out TimeSpan time, out string error)
    {
        time = default;
        error = "";

        s = s.Trim();
        if (s.Length == 0)
        {
            error = "Empty string.";
            return false;
        }

        if (IsAllDigits(s))
        {
            if (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out int minutes) || minutes < 0)
            {
                error = "Minutes must be a non-negative integer.";
                return false;
            }

            time = TimeSpan.FromMinutes(minutes);
            if (time <= TimeSpan.Zero)
            {
                error = "Period must be > 0.";
                return false;
            }
            return true;
        }

        var parts = s.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 3)
        {
            error = "Use \"m\", \"m:ss\", or \"h:mm:ss\".";
            return false;
        }

        bool ok;
        int h = 0, m = 0, sec = 0;

        if (parts.Length == 2)
        {
            ok = int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out m)
                 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out sec);
        }
        else
        {
            ok = int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out h)
                 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out m)
                 && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out sec);
        }

        if (!ok || h < 0 || m < 0 || sec < 0)
        {
            error = "Time components must be non-negative integers.";
            return false;
        }

        if (m >= 60 || sec >= 60)
        {
            error = "Minutes and seconds must be < 60 in colon formats.";
            return false;
        }

        time = new TimeSpan(h, m, sec);
        if (time <= TimeSpan.Zero)
        {
            error = "Period must be > 0.";
            return false;
        }
        return true;
    }

    public static bool IsAllDigits(string s)
    {
        foreach (char c in s)
            if (c < '0' || c > '9')
                return false;
        return true;
    }
}
