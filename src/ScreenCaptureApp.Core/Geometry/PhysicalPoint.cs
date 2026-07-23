namespace ScreenCaptureApp.Core.Geometry;

/// <summary>A point in signed physical desktop pixels.</summary>
public readonly record struct PhysicalPoint(int X, int Y)
{
    public static PhysicalPoint operator +(PhysicalPoint point, PhysicalVector vector) =>
        new(checked(point.X + vector.X), checked(point.Y + vector.Y));

    public static PhysicalVector operator -(PhysicalPoint left, PhysicalPoint right) =>
        new(checked(left.X - right.X), checked(left.Y - right.Y));
}

/// <summary>A displacement measured in physical desktop pixels.</summary>
public readonly record struct PhysicalVector(int X, int Y)
{
    public static readonly PhysicalVector Zero = new(0, 0);

    public static PhysicalVector operator *(PhysicalVector vector, int factor) =>
        new(checked(vector.X * factor), checked(vector.Y * factor));
}
