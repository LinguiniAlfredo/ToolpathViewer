using OpenTK.Mathematics;
using ToolpathViewer.Core.Models;

namespace ToolpathViewer.Rendering.Camera;

public enum ViewPreset
{
    Isometric,
    Top,
    Front,
    Right
}

public sealed class Camera3D
{
    private const float MinDistance = 0.01f;
    private const float MaxDistance = 10000f;
    public const float MinPitch = -89.9f;
    public const float MaxPitch = 89.9f;

    public Vector3 Target { get; set; } = Vector3.Zero;
    public float Distance { get; set; } = 25f;
    public float Pitch { get; set; } = 35.264f; // Default Isometric
    public float Yaw { get; set; } = 45.0f;
    public float FieldOfViewDegrees { get; set; } = 45f;
    public float AspectRatio { get; set; } = 1.0f;
    public float NearPlane { get; set; } = 0.01f;
    public float FarPlane { get; set; } = 10000f;

    public Vector3 Position => CalculatePosition();
    public Vector3 Right => CalculateRight();
    public Vector3 Up => CalculateUp();

    public Matrix4 GetViewMatrix()
    {
        Vector3 eye = Position;
        Vector3 up = CalculateUp();
        return Matrix4.LookAt(eye, Target, up);
    }

    public Matrix4 GetProjectionMatrix()
    {
        float fovRad = MathHelper.DegreesToRadians(FieldOfViewDegrees);
        return Matrix4.CreatePerspectiveFieldOfView(fovRad, AspectRatio, NearPlane, FarPlane);
    }

    public Matrix4 GetViewProjectionMatrix() => GetViewMatrix() * GetProjectionMatrix();

    public (Vector3 RayOrigin, Vector3 RayDirection) ScreenPointToRay(
        float screenX, float screenY, int viewportWidth, int viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return (Position, -Vector3.UnitZ);
        }

        float ndcX = (2.0f * screenX) / viewportWidth - 1.0f;
        float ndcY = 1.0f - (2.0f * screenY) / viewportHeight;

        Matrix4 invVp = Matrix4.Invert(GetViewProjectionMatrix());

        Vector4 nearWorld = Vector4.TransformRow(new Vector4(ndcX, ndcY, -1.0f, 1.0f), invVp);
        Vector4 farWorld = Vector4.TransformRow(new Vector4(ndcX, ndcY, 1.0f, 1.0f), invVp);

        Vector3 nearPoint = nearWorld.Xyz / nearWorld.W;
        Vector3 farPoint = farWorld.Xyz / farWorld.W;

        Vector3 rayOrigin = nearPoint;
        Vector3 rayDir = Vector3.Normalize(farPoint - nearPoint);

        return (rayOrigin, rayDir);
    }

    public bool IntersectRayPlaneZ(
        Vector3 rayOrigin, Vector3 rayDir, float planeZ, out Vector3 hitPoint)
    {
        if (MathF.Abs(rayDir.Z) < 1e-6f)
        {
            hitPoint = Vector3.Zero;
            return false;
        }

        float t = (planeZ - rayOrigin.Z) / rayDir.Z;
        if (t < 0f)
        {
            hitPoint = Vector3.Zero;
            return false;
        }

        hitPoint = rayOrigin + t * rayDir;
        hitPoint.Z = planeZ;
        return true;
    }

    public void Orbit(float deltaYawDegrees, float deltaPitchDegrees)
    {
        Yaw = (Yaw + deltaYawDegrees) % 360.0f;
        if (Yaw < 0f)
        {
            Yaw += 360.0f;
        }

        Pitch = Math.Clamp(Pitch + deltaPitchDegrees, MinPitch, MaxPitch);
    }

    public void Pan(float deltaScreenX, float deltaScreenY, int viewportWidth, int viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        Vector3 right = CalculateRight();
        Vector3 up = CalculateUp();

        float fovRad = MathHelper.DegreesToRadians(FieldOfViewDegrees);
        float worldHeight = 2.0f * Distance * MathF.Tan(fovRad * 0.5f);
        float worldWidth = worldHeight * AspectRatio;

        float moveX = -(deltaScreenX / viewportWidth) * worldWidth;
        float moveY = (deltaScreenY / viewportHeight) * worldHeight;

        Target += right * moveX + up * moveY;
    }

    public void Zoom(float deltaWheel)
    {
        // Smooth exponential zoom
        float factor = MathF.Pow(1.15f, -deltaWheel);
        Distance = Math.Clamp(Distance * factor, MinDistance, MaxDistance);
    }

    public void FitToBounds(BoundingBox3D bbox)
    {
        if (bbox.IsEmpty)
        {
            Target = Vector3.Zero;
            Distance = 25f;
            Pitch = 35.264f;
            Yaw = 45.0f;
            return;
        }

        Target = new Vector3(bbox.Center.X, bbox.Center.Y, bbox.Center.Z);

        float radius = bbox.MaxExtent * 0.5f;
        if (radius < 0.1f)
        {
            radius = 1.0f;
        }

        float fovRad = MathHelper.DegreesToRadians(FieldOfViewDegrees);
        float requiredDistance = (radius * 1.6f) / MathF.Sin(fovRad * 0.5f);

        Distance = Math.Clamp(requiredDistance, 1.0f, MaxDistance);
        NearPlane = Math.Max(0.01f, Distance * 0.01f);
        FarPlane = Math.Max(100f, Distance * 10f);
    }

    public static (float Yaw, float Pitch) GetPresetAngles(ViewPreset preset) => preset switch
    {
        ViewPreset.Isometric => (45f, 35.264f),
        ViewPreset.Top => (0f, 89.9f),
        ViewPreset.Front => (0f, 0f),
        ViewPreset.Right => (90f, 0f),
        _ => (45f, 35.264f)
    };

    public static float ShortestAngleDistance(float fromDegrees, float toDegrees)
    {
        float diff = (toDegrees - fromDegrees) % 360f;
        if (diff > 180f)
        {
            diff -= 360f;
        }
        else if (diff < -180f)
        {
            diff += 360f;
        }

        return diff;
    }

    public static float InterpolateAngle(float fromDegrees, float toDegrees, float t)
    {
        float diff = ShortestAngleDistance(fromDegrees, toDegrees);
        float result = (fromDegrees + diff * t) % 360f;
        if (result < 0f)
        {
            result += 360f;
        }

        return result;
    }

    public static float EaseInOutCubic(float t)
    {
        float clamped = Math.Clamp(t, 0f, 1f);
        return clamped < 0.5f
            ? 4f * clamped * clamped * clamped
            : 1f - MathF.Pow(-2f * clamped + 2f, 3f) * 0.5f;
    }

    public void SetPreset(ViewPreset preset)
    {
        var (yaw, pitch) = GetPresetAngles(preset);
        Yaw = yaw;
        Pitch = pitch;
    }

    public Vector3 CalculateRight()
    {
        float yawRad = MathHelper.DegreesToRadians(Yaw);
        return new Vector3(MathF.Cos(yawRad), MathF.Sin(yawRad), 0f);
    }

    public Vector3 CalculateUp()
    {
        float pitchRad = MathHelper.DegreesToRadians(Pitch);
        float yawRad = MathHelper.DegreesToRadians(Yaw);

        float cosPitch = MathF.Cos(pitchRad);
        float sinPitch = MathF.Sin(pitchRad);
        float cosYaw = MathF.Cos(yawRad);
        float sinYaw = MathF.Sin(yawRad);

        return new Vector3(-sinPitch * sinYaw, sinPitch * cosYaw, cosPitch);
    }

    private Vector3 CalculatePosition()
    {
        float pitchRad = MathHelper.DegreesToRadians(Pitch);
        float yawRad = MathHelper.DegreesToRadians(Yaw);

        float cosPitch = MathF.Cos(pitchRad);
        float sinPitch = MathF.Sin(pitchRad);
        float cosYaw = MathF.Cos(yawRad);
        float sinYaw = MathF.Sin(yawRad);

        // Z-up coordinate system (standard in CNC / 3D printing / CAM)
        var offset = new Vector3(
            Distance * cosPitch * sinYaw,
            Distance * -cosPitch * cosYaw,
            Distance * sinPitch
        );

        return Target + offset;
    }
}
