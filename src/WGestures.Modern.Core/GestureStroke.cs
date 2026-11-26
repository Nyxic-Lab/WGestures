namespace WGestures.Modern.Core;

/// <summary>A drawing-pad helper; the running gesture recognizer retains its own thresholds.</summary>
public sealed class GestureStroke
{
    private const string Directions = "↑↗→↘↓↙←↖";
    private double _x, _y;
    private readonly double _threshold;
    private readonly bool _eight;
    private readonly List<char> _path = new();

    public GestureStroke(double x, double y, bool eightDirections, double threshold = 18)
    {
        _x = x; _y = y; _eight = eightDirections; _threshold = threshold;
    }

    public string Path => new(_path.ToArray());
    public bool Move(double x, double y)
    {
        double dx = x - _x, dy = y - _y;
        if (dx * dx + dy * dy < _threshold * _threshold) return false;
        _x = x; _y = y;
        int sectors = _eight ? 8 : 4;
        var angle = Math.Atan2(dx, -dy);
        int sector = ((int)Math.Round(angle / (2 * Math.PI / sectors), MidpointRounding.AwayFromZero) + sectors) % sectors;
        char direction = Directions[_eight ? sector : sector * 2];
        if (_path.Count >= 32 || (_path.Count != 0 && _path[^1] == direction)) return false;
        _path.Add(direction);
        return true;
    }
}
