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
    private const float MinPitch = -89.9f;
    private const float MaxPitch = 89.9f;

    public Vector3 Target { get; set; } = Vector3.Zero;
    public float Distance { get; set; } = 15f;
    public float Pitch { get; set; } = 35.264f; // Default Isometric
    public float Yaw { get; set; } = 45.0f;
    public float FieldOfViewDegrees { get; set; } = 45f;
    public float AspectRatio { get; set; } = 1.0f;
    public float NearPlane { get; set; } = 0.01f;
    public float FarPlane { get; set; } = 10000f;

    public Vector3 Position => CalculatePosition();

    public Matrix4 GetViewMatrix()
    {
        Vector3 eye = Position;
        Vector3 up = Vector3.UnitZ;

        Vector3 dir = Target - eye;
        if (dir.LengthSquared > 1e-6f)
        {
            dir = Vector3.Normalize(dir);
            // If viewing straight down or straight up, use Y as the up vector to prevent gimbal lock / degenerate lookAt
            if (MathF.Abs(Vector3.Dot(dir, Vector3.UnitZ)) > 0.99f)
            {
                up = dir.Z < 0f ? Vector3.UnitY : -Vector3.UnitY;
            }
        }

        return Matrix4.LookAt(eye, Target, up);
    }

    public Matrix4 GetProjectionMatrix()
    {
        float fovRad = MathHelper.DegreesToRadians(FieldOfViewDegrees);
        return Matrix4.CreatePerspectiveFieldOfView(fovRad, AspectRatio, NearPlane, FarPlane);
    }

    public Matrix4 GetViewProjectionMatrix() => GetViewMatrix() * GetProjectionMatrix();

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

        Vector3 forward = Target - Position;
        if (forward.LengthSquared < 1e-6f)
        {
            return;
        }
        forward = Vector3.Normalize(forward);

        Vector3 right = Vector3.Cross(forward, Vector3.UnitZ);
        if (right.LengthSquared < 1e-6f)
        {
            right = Vector3.Cross(forward, Vector3.UnitY);
            if (right.LengthSquared < 1e-6f)
            {
                right = Vector3.UnitX;
            }
            else
            {
                right = Vector3.Normalize(right);
            }
        }
        else
        {
            right = Vector3.Normalize(right);
        }

        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));

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
            Distance = 15f;
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

    public void SetPreset(ViewPreset preset)
    {
        switch (preset)
        {
            case ViewPreset.Isometric:
                Yaw = 45f;
                Pitch = 35.264f;
                break;

            case ViewPreset.Top:
                Yaw = 0f;
                Pitch = 89.9f;
                break;

            case ViewPreset.Front:
                Yaw = 0f;
                Pitch = 0f;
                break;

            case ViewPreset.Right:
                Yaw = 90f;
                Pitch = 0f;
                break;
        }
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
