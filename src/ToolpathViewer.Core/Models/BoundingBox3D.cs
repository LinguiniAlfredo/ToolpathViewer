using System.Numerics;

namespace ToolpathViewer.Core.Models;

public readonly record struct BoundingBox3D
{
    public float MinX { get; init; }
    public float MinY { get; init; }
    public float MinZ { get; init; }
    public float MaxX { get; init; }
    public float MaxY { get; init; }
    public float MaxZ { get; init; }
    public bool IsEmpty { get; init; }

    public static BoundingBox3D Empty => new()
    {
        MinX = float.MaxValue,
        MinY = float.MaxValue,
        MinZ = float.MaxValue,
        MaxX = float.MinValue,
        MaxY = float.MinValue,
        MaxZ = float.MinValue,
        IsEmpty = true
    };

    public BoundingBox3D(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    {
        MinX = minX;
        MinY = minY;
        MinZ = minZ;
        MaxX = maxX;
        MaxY = maxY;
        MaxZ = maxZ;
        IsEmpty = false;
    }

    public float SizeX => IsEmpty ? 0f : MaxX - MinX;
    public float SizeY => IsEmpty ? 0f : MaxY - MinY;
    public float SizeZ => IsEmpty ? 0f : MaxZ - MinZ;

    public Vector3 Center => IsEmpty
        ? Vector3.Zero
        : new Vector3((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f, (MinZ + MaxZ) * 0.5f);

    public float MaxExtent => IsEmpty
        ? 1f
        : MathF.Max(SizeX, MathF.Max(SizeY, SizeZ));

    public BoundingBox3D Expand(in ToolpathPoint point)
    {
        if (IsEmpty)
        {
            return new BoundingBox3D(point.X, point.Y, point.Z, point.X, point.Y, point.Z);
        }

        return new BoundingBox3D(
            MathF.Min(MinX, point.X),
            MathF.Min(MinY, point.Y),
            MathF.Min(MinZ, point.Z),
            MathF.Max(MaxX, point.X),
            MathF.Max(MaxY, point.Y),
            MathF.Max(MaxZ, point.Z)
        );
    }
}
