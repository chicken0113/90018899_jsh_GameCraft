using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using Vortice.Direct2D1;
using Vortice.Mathematics;

sealed class StageMap : IDisposable
{
    private const string AssetDirectory = "resource/hot_heavy";
    public const float Scale = 2;

    public sealed record Point(float X, float Y);
    public sealed record EnemySpawn(float X, float Y, float Angle, string Kind, bool Stationary,
        bool Patrol, Point[]? Route);

    public sealed record Door(float X, float Y, bool Vertical, bool Locked)
    {
        private const float Width = 32;
        private const float HalfThickness = 3;
        private const float OpenAngle = MathF.PI / 2;
        private const float HoldDuration = 1.1f;

        private float _target;
        private float _holdTime;
        private float _previousAngle;
        private bool _playerSwing;

        public float Angle { get; private set; }
        public Vector2 Hinge => new(X, Y);
        public bool PlayerSwinging => _playerSwing && Angle != _previousAngle;

        public void Reset()
        {
            Angle = _target = _holdTime = _previousAngle = 0;
            _playerSwing = false;
        }

        public bool Blocks(Vector2 point, float radius = 0) => BlocksAt(point, radius, Angle);

        private bool BlocksAt(Vector2 point, float radius, float angle)
        {
            Vector2 axis = Vertical ? new(-MathF.Sin(angle), MathF.Cos(angle))
                : new(MathF.Cos(angle), MathF.Sin(angle));
            Vector2 relative = point - Hinge;
            float along = Math.Clamp(Vector2.Dot(relative, axis), 0, Width);
            float reach = radius + HalfThickness;
            return Vector2.DistanceSquared(relative, axis * along) <= reach * reach;
        }

        public void Push(Vector2 from, Vector2 to, bool player)
        {
            if (Locked) return;

            float across = Vertical ? to.X - from.X : to.Y - from.Y;
            if (MathF.Abs(across) < 0.001f) return;

            _holdTime = HoldDuration;
            if (_target != 0 && _target != Angle) return;

            // A shove during closing reopens the door to the same side.
            if (MathF.Abs(Angle) >= 0.1f)
            {
                _target = MathF.CopySign(OpenAngle, Angle);
                return;
            }

            _target = MathF.CopySign(OpenAngle, Vertical ? -across : across);
            _playerSwing = player;
        }

        public void Update(float dt, Vector2 player, IReadOnlyList<Vector2> enemies)
        {
            _previousAngle = Angle;
            if (Locked) return;

            if (Angle == _target && _holdTime > 0)
            {
                _holdTime = MathF.Max(0, _holdTime - dt);
                if (_holdTime == 0)
                {
                    _target = 0;
                    _playerSwing = false;
                }
            }

            if (Angle == _target) return;

            float step = (_target == 0 ? 5 : 14) * dt;
            float remaining = _target - Angle;
            float next = MathF.Abs(remaining) <= step ? _target : Angle + MathF.CopySign(step, remaining);
            if (_target == 0 && (BlocksAt(player, 3, next) ||
                enemies.Any(enemy => BlocksAt(enemy, 3, next))))
                return;

            Angle = next;
        }

        public bool HitByPlayerSwing(Vector2 position)
        {
            if (!PlayerSwinging) return false;

            int steps = (int)MathF.Ceiling(MathF.Abs(Angle - _previousAngle) / 0.1f);
            for (int step = 1; step <= steps; step++)
            {
                float angle = _previousAngle + (Angle - _previousAngle) * step / steps;
                if (BlocksAt(position, 5, angle))
                    return true;
            }
            return false;
        }
    }

    public sealed record Exit(float X, float Y, int Target, int Dx, int Dy,
        float OffsetX, float OffsetY, float Min, float Max)
    {
        public bool Crossed(Vector2 from, Vector2 to)
        {
            float start = Dx != 0 ? (from.X - X) * Dx : (from.Y - Y) * Dy;
            float end = Dx != 0 ? (to.X - X) * Dx : (to.Y - Y) * Dy;
            if (start > 0 || end <= 0) return false;
            Vector2 crossing = Vector2.Lerp(from, to, -start / (end - start));
            float along = Dx != 0 ? crossing.Y - Y : crossing.X - X;
            return along >= Min && along <= Max;
        }
    }

    public sealed record Area(int Width, int Height, string Texture, string Walk, string Shot,
        Door[] Doors, Exit[] Exits, EnemySpawn[] Enemies);

    private sealed record Sprite(int X, int Y, int Width, int Height, int OriginX, int OriginY);
    private sealed record StageData(Point Start, Area[] Rooms, Dictionary<string, Sprite> Sprites);

    private readonly StageData _data;
    private readonly G2Texture _atlas;
    private readonly G2Texture[] _backgrounds;
    private readonly byte[][] _walk;
    private readonly byte[][] _shot;

    public Point Start => _data.Start;
    public Area[] Areas => _data.Rooms;

    public StageMap()
    {
        string json = File.ReadAllText(Path.Combine(AssetDirectory, "stage.json"));
        _data = JsonSerializer.Deserialize<StageData>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) ?? throw new InvalidDataException("Hot & Heavy stage data is missing.");

        _walk = Areas.Select(area => Decode(area.Walk, area.Width * area.Height)).ToArray();
        _shot = Areas.Select(area => Decode(area.Shot, area.Width * area.Height)).ToArray();
        _atlas = new G2Texture(Path.Combine(AssetDirectory, "sprites.png"));
        List<G2Texture> backgrounds = new();
        try
        {
            foreach (Area area in Areas)
                backgrounds.Add(new G2Texture(Path.Combine(AssetDirectory, area.Texture)));
            _backgrounds = backgrounds.ToArray();
        }
        catch
        {
            foreach (G2Texture texture in backgrounds) texture.Dispose();
            _atlas.Dispose();
            throw;
        }
    }

    private static byte[] Decode(string packed, int length)
    {
        using MemoryStream bytes = new(Convert.FromBase64String(packed));
        using ZLibStream stream = new(bytes, CompressionMode.Decompress);
        byte[] mask = new byte[length];
        stream.ReadExactly(mask);
        return mask;
    }

    public void Reset()
    {
        foreach (Area area in Areas)
            foreach (Door door in area.Doors)
                door.Reset();
    }

    public bool CanWalk(int area, Vector2 position) => Inside(area, position, _walk);

    public bool TryStep(int area, Vector2 from, Vector2 to, bool player)
    {
        if (!CanWalk(area, to)) return false;

        foreach (Door door in Areas[area].Doors)
        {
            if (!door.Blocks(to, 2)) continue;
            door.Push(from, to, player);
            return false;
        }

        return true;
    }

    public bool CanShoot(int area, Vector2 position)
    {
        if (!Inside(area, position, _shot)) return false;
        foreach (Door door in Areas[area].Doors)
            if (door.Blocks(position))
                return false;
        return true;
    }

    private bool Inside(int index, Vector2 point, byte[][] masks)
    {
        Area area = Areas[index];
        return point.X >= 0 && point.X < area.Width && point.Y >= 0 && point.Y < area.Height &&
            masks[index][(int)point.Y * area.Width + (int)point.X] != 0;
    }

    public void Draw(int index, Vector2 camera, bool roomCleared)
    {
        Area area = Areas[index];
        _backgrounds[index].Draw(
            new Rect(-camera.X * Scale, -camera.Y * Scale, area.Width * Scale, area.Height * Scale),
            new Rect(0, 0, area.Width, area.Height), 1, BitmapInterpolationMode.NearestNeighbor);

        foreach (Door door in area.Doors)
        {
            if (door.Locked) continue;
            string sprite = door.Vertical ? "sprDoorV" : "sprDoorH";
            DrawSprite(sprite, 0, door.Hinge, camera, door.Angle);
        }

        if (!roomCleared) return;
        foreach (Exit exit in area.Exits)
        {
            int frame = exit.Dx > 0 ? 0 : exit.Dx < 0 ? 2 : exit.Dy > 0 ? 3 : 1;
            DrawSprite("sprGoArrow", frame, new Vector2(exit.X, exit.Y), camera, 0);
        }
    }

    public void DrawSprite(string name, int frame, Vector2 position, Vector2 camera, float angle)
    {
        if (!_data.Sprites.TryGetValue($"{name}_{frame}", out Sprite? sprite))
            sprite = _data.Sprites[$"{name}_0"];

        Vector2 center = (position - camera) * Scale;
        var target = G2AppBase.Instance!.RenderTarget;
        Matrix3x2 previous = target.Transform;
        target.Transform = Matrix3x2.CreateRotation(angle, center) * previous;
        _atlas.Draw(new Rect(center.X - sprite.OriginX * Scale, center.Y - sprite.OriginY * Scale,
            sprite.Width * Scale, sprite.Height * Scale),
            new Rect(sprite.X, sprite.Y, sprite.Width, sprite.Height), 1, BitmapInterpolationMode.NearestNeighbor);
        target.Transform = previous;
    }

    public void Dispose()
    {
        foreach (G2Texture texture in _backgrounds) texture.Dispose();
        _atlas.Dispose();
    }
}
