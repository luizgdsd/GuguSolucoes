using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GuguSolucoes.Desktop.UI;

internal sealed class ThemedProgressBar : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private int _cornerRadius = 5;
    private Color _trackColor = Color.FromArgb(28, 38, 52);
    private Color _fillColor = Color.FromArgb(53, 194, 217);
    private Color _borderColor = Color.FromArgb(48, 65, 89);
    private readonly System.Windows.Forms.Timer _indeterminateTimer;
    private bool _isIndeterminate;
    private int _indeterminateOffset;

    public ThemedProgressBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        Size = new Size(160, 10);

        _indeterminateTimer = new System.Windows.Forms.Timer { Interval = 28 };
        _indeterminateTimer.Tick += (_, _) =>
        {
            _indeterminateOffset += 8;
            if (_indeterminateOffset > Width + 40)
            {
                _indeterminateOffset = -40;
            }

            Invalidate();
        };
    }

    [DefaultValue(0)]
    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < _minimum)
            {
                _maximum = _minimum;
            }

            if (_value < _minimum)
            {
                _value = _minimum;
            }

            Invalidate();
        }
    }

    [DefaultValue(100)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(value, _minimum);
            if (_value > _maximum)
            {
                _value = _maximum;
            }

            Invalidate();
        }
    }

    [DefaultValue(0)]
    public int Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, _minimum, _maximum);
            if (clamped == _value)
            {
                return;
            }

            if (_isIndeterminate)
            {
                IsIndeterminate = false;
            }

            _value = clamped;
            Invalidate();
        }
    }

    [DefaultValue(5)]
    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(2, value);
            Invalidate();
        }
    }

    public Color TrackColor
    {
        get => _trackColor;
        set
        {
            _trackColor = value;
            Invalidate();
        }
    }

    public Color FillColor
    {
        get => _fillColor;
        set
        {
            _fillColor = value;
            Invalidate();
        }
    }

    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            Invalidate();
        }
    }

    [DefaultValue(false)]
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            if (_isIndeterminate == value)
            {
                return;
            }

            _isIndeterminate = value;
            _indeterminateOffset = -40;
            if (_isIndeterminate)
            {
                _indeterminateTimer.Start();
            }
            else
            {
                _indeterminateTimer.Stop();
            }

            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = ClientRectangle;
        if (rect.Width <= 1 || rect.Height <= 1)
        {
            return;
        }

        rect.Width -= 1;
        rect.Height -= 1;

        using (var trackPath = StyleManager.CreateRoundedPath(rect, _cornerRadius))
        using (var trackBrush = new SolidBrush(_trackColor))
        using (var borderPen = new Pen(_borderColor, 1F))
        {
            e.Graphics.FillPath(trackBrush, trackPath);
            e.Graphics.DrawPath(borderPen, trackPath);
        }

        if (_isIndeterminate)
        {
            var chunkWidth = Math.Max(22, rect.Width / 4);
            var chunkRect = new Rectangle(rect.X + _indeterminateOffset, rect.Y, chunkWidth, rect.Height);
            var visibleRect = Rectangle.Intersect(rect, chunkRect);
            if (visibleRect.Width > 0)
            {
                var radius = Math.Min(_cornerRadius, Math.Max(2, visibleRect.Width / 2));
                using var glowPath = StyleManager.CreateRoundedPath(visibleRect, radius);
                using var brush = new SolidBrush(_fillColor);
                e.Graphics.FillPath(brush, glowPath);
            }

            return;
        }

        if (_maximum <= _minimum || _value <= _minimum)
        {
            return;
        }

        var percent = (float)(_value - _minimum) / (_maximum - _minimum);
        var fillWidth = (int)Math.Round(rect.Width * percent);
        if (fillWidth <= 0)
        {
            return;
        }

        var fillRect = new Rectangle(rect.X, rect.Y, Math.Max(1, fillWidth), rect.Height);
        var fillRadius = Math.Min(_cornerRadius, Math.Max(2, fillRect.Width / 2));
        using var fillPath = StyleManager.CreateRoundedPath(fillRect, fillRadius);
        using var fillBrush = new SolidBrush(_fillColor);
        e.Graphics.FillPath(fillBrush, fillPath);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _indeterminateTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
