namespace PDFEditorApp.Models;

/// <summary>Điểm 2D (double).</summary>
public readonly record struct PointD(double X, double Y);

/// <summary>
/// Hình chữ nhật trong hệ "view": đơn vị point (1/72 inch), gốc ở góc trên-trái, trục Y hướng xuống.
/// </summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double Area => Width * Height;

    public bool Contains(double x, double y) => x >= X && x <= Right && y >= Y && y <= Bottom;

    public RectD Inflate(double amount) =>
        new(X - amount, Y - amount, Width + (2 * amount), Height + (2 * amount));

    public static RectD FromPoints(IEnumerable<PointD> points)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in points)
        {
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }

        return minX > maxX ? default : new RectD(minX, minY, maxX - minX, maxY - minY);
    }
}

/// <summary>Khung bao trong hệ tọa độ trang PDF (trục Y hướng lên).</summary>
public readonly record struct PdfBounds(double Left, double Bottom, double Right, double Top)
{
    public IEnumerable<PointD> Corners()
    {
        yield return new PointD(Left, Bottom);
        yield return new PointD(Right, Bottom);
        yield return new PointD(Right, Top);
        yield return new PointD(Left, Top);
    }
}

/// <summary>
/// Ma trận affine theo quy ước PDF: x' = A·x + C·y + E ; y' = B·x + D·y + F.
/// </summary>
public readonly record struct Affine(double A, double B, double C, double D, double E, double F)
{
    public static Affine Identity => new(1, 0, 0, 1, 0, 0);

    public double Determinant => (A * D) - (B * C);

    public PointD Transform(double x, double y) => new((A * x) + (C * y) + E, (B * x) + (D * y) + F);

    public PointD Transform(PointD p) => Transform(p.X, p.Y);

    /// <summary>Biến đổi vector (bỏ phần tịnh tiến).</summary>
    public PointD TransformVector(double x, double y) => new((A * x) + (C * y), (B * x) + (D * y));

    /// <summary>Ma trận "áp dụng this rồi tới <paramref name="next"/>".</summary>
    public Affine Then(Affine next) => new(
        (next.A * A) + (next.C * B),
        (next.B * A) + (next.D * B),
        (next.A * C) + (next.C * D),
        (next.B * C) + (next.D * D),
        (next.A * E) + (next.C * F) + next.E,
        (next.B * E) + (next.D * F) + next.F);

    public static Affine Translation(double dx, double dy) => new(1, 0, 0, 1, dx, dy);

    /// <summary>Co giãn quanh điểm (cx, cy).</summary>
    public static Affine ScaleAt(double sx, double sy, double cx, double cy) => new(sx, 0, 0, sy, cx - (sx * cx), cy - (sy * cy));

    public Affine Invert()
    {
        var det = Determinant;
        if (Math.Abs(det) < 1e-12)
        {
            throw new InvalidOperationException("Matrix is not invertible.");
        }

        return new Affine(
            D / det,
            -B / det,
            -C / det,
            A / det,
            ((C * F) - (D * E)) / det,
            ((B * E) - (A * F)) / det);
    }
}
