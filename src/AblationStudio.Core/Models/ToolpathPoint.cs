using System.Numerics;

namespace AblationStudio.Core.Models;

public readonly record struct ToolpathPoint(float X, float Y, float Z)
{
    public static readonly ToolpathPoint Zero = new(0f, 0f, 0f);

    public Vector3 ToVector3() => new(X, Y, Z);

    public float DistanceTo(in ToolpathPoint other)
    {
        float dx = other.X - X;
        float dy = other.Y - Y;
        float dz = other.Z - Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
}
