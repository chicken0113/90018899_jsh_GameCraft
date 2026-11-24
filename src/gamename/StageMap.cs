using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using Vortice.Direct2D1;
using Vortice.Mathematics;

sealed class StageMap : IDisposable
{
    private const string AssetDirectory = "resource/hot_heavy";
    public const float Scale = 2;
    public const int StageCount = 3;

    private sealed record TileBlock(int X, int Y, bool Vertical);

    private static readonly TileBlock[][] TileLayouts =
    [
        [new(128, 128, false), new(512, 128, false), new(384, 384, false),
         new(384, 128, true), new(128, 384, true), new(640, 512, true)],
        [new(256, 128, false), new(512, 256, false), new(128, 384, false),
         new(384, 512, false), new(128, 128, true), new(512, 128, true),
         new(384, 384, true)]
    ];

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
    private readonly G2Texture?[] _backgrounds;
    private readonly G2Texture? _floor;
    private readonly G2Texture? _horizontalWall;
    private readonly G2Texture? _verticalWall;
    private readonly int _tileLayoutIndex;
    private readonly byte[][] _walk;
    private readonly byte[][] _shot;

    public Point Start => _data.Start;
    public Area[] Areas => _data.Rooms;

    public StageMap(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= StageCount)
            throw new ArgumentOutOfRangeException(nameof(stageIndex));
        _tileLayoutIndex = stageIndex;

        string json = File.ReadAllText(Path.Combine(AssetDirectory, "stage.json"));
        StageData source = JsonSerializer.Deserialize<StageData>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) ?? throw new InvalidDataException("Hot & Heavy stage data is missing.");

        _data = stageIndex == StageCount - 1 ? source : BuildTileStage(stageIndex, source.Sprites);

        _walk = Areas.Select(area => Decode(area.Walk, area.Width * area.Height)).ToArray();
        _shot = Areas.Select(area => Decode(area.Shot, area.Width * area.Height)).ToArray();
        _atlas = new G2Texture(Path.Combine(AssetDirectory, "sprites.png"));
        List<G2Texture?> backgrounds = new();
        try
        {
            foreach (Area area in Areas)
                backgrounds.Add(area.Texture == "tile" ? null :
                    new G2Texture(Path.Combine(AssetDirectory, area.Texture)));
            _backgrounds = backgrounds.ToArray();
            if (stageIndex < StageCount - 1)
            {
                string tiles = Path.Combine("resource", "image", "tiles");
                _floor = new G2Texture(Path.Combine(tiles,
                    stageIndex == 0 ? "floor_service_1.png" : "floor_club_1.png"));
                _horizontalWall = new G2Texture(Path.Combine(tiles,
                    stageIndex == 0 ? "wall_cyan_horizontal.png" : "wall_amber_horizontal.png"));
                _verticalWall = new G2Texture(Path.Combine(tiles,
                    stageIndex == 0 ? "wall_cyan_vertical.png" : "wall_amber_vertical.png"));
            }
        }
        catch
        {
            foreach (G2Texture? texture in backgrounds) texture?.Dispose();
            _floor?.Dispose();
            _horizontalWall?.Dispose();
            _verticalWall?.Dispose();
            _atlas.Dispose();
            throw;
        }
    }

    private static StageData BuildTileStage(int index, Dictionary<string, Sprite> sprites)
    {
        const int width = 800;
        const int height = 704;
        byte[] mask = new byte[width * height];
        for (int y = 16; y < height - 20; y++)
            Array.Fill(mask, (byte)1, y * width + 16, width - 32);

        foreach (TileBlock block in TileLayouts[index])
        {
            int left = block.X + (block.Vertical ? 20 : 0);
            int top = block.Y + (block.Vertical ? 0 : 52);
            int right = block.X + (block.Vertical ? 74 : 128);
            int bottom = block.Y + (block.Vertical ? 128 : 120);
            for (int y = top; y < bottom; y++)
                Array.Fill(mask, (byte)0, y * width + left, right - left);
        }

        EnemySpawn[] enemies = index == 0
            ? [Spawn(290, 190, "gun"), Spawn(520, 100, "gun"), Spawn(650, 220, "melee"),
               Spawn(555, 360, "gun"), Spawn(240, 350, "melee"), Spawn(330, 530, "gun"),
               Spawn(570, 590, "melee"), Spawn(620, 560, "gun")]
            : [Spawn(315, 80, "gun"), Spawn(590, 90, "gun"), Spawn(685, 190, "melee"),
               Spawn(305, 295, "melee"), Spawn(470, 300, "gun"), Spawn(680, 380, "gun"),
               Spawn(295, 570, "gun"), Spawn(540, 580, "melee"), Spawn(700, 570, "gun"),
               Spawn(75, 250, "melee"), Spawn(580, 585, "dog"), Spawn(345, 360, "gun")];

        Area room = new(width, height, "tile", Pack(mask), Pack(mask), [], [], enemies);
        return new StageData(new Point(176, 608), [room], sprites);
    }

    private static EnemySpawn Spawn(float x, float y, string kind) =>
        new(x, y, 0, kind, false, false, null);

    private static string Pack(byte[] mask)
    {
        using MemoryStream bytes = new();
        using (ZLibStream stream = new(bytes, CompressionLevel.Fastest, true))
            stream.Write(mask);
        return Convert.ToBase64String(bytes.ToArray());
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
        if (area.Texture == "tile") DrawTileRoom(camera, area);
        else _backgrounds[index]!.Draw(
            new Rect(-camera.X * Scale, -camera.Y * Scale, area.Width * Scale, area.Height * Scale),
            new Rect(0, 0, area.Width, area.Height), 1, BitmapInterpolationMode.NearestNeighbor);

        foreach (Door door in area.Doors)
        {
            if (door.Locked) continue;
            string sprite = door.Vertical ? "sprDoorV" : "sprDoorH";
            DrawSprite(sprite, 0, door.Hinge, camera, door.Angle);
        }

        if (!roomCleared) return;
        if (area.Texture == "tile")
            DrawSprite("sprGoArrow", 3, new Vector2(Start.X, 650), camera, 0);
        foreach (Exit exit in area.Exits)
        {
            int frame = exit.Dx > 0 ? 0 : exit.Dx < 0 ? 2 : exit.Dy > 0 ? 3 : 1;
            DrawSprite("sprGoArrow", frame, new Vector2(exit.X, exit.Y), camera, 0);
        }
    }

    private void DrawTileRoom(Vector2 camera, Area area)
    {
        for (int y = 0; y < area.Height; y += 128)
            for (int x = 0; x < area.Width; x += 128)
                DrawTile(_floor!, x, y, camera);

        foreach (TileBlock block in TileLayouts[_tileLayoutIndex])
            DrawTile(block.Vertical ? _verticalWall! : _horizontalWall!, block.X, block.Y, camera);
    }

    private static void DrawTile(G2Texture texture, int x, int y, Vector2 camera)
    {
        texture.Draw(new Rect((x - camera.X) * Scale, (y - camera.Y) * Scale,
            128 * Scale, 128 * Scale), new Rect(0, 0, 128, 128), 1,
            BitmapInterpolationMode.NearestNeighbor);
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
        foreach (G2Texture? texture in _backgrounds) texture?.Dispose();
        _floor?.Dispose();
        _horizontalWall?.Dispose();
        _verticalWall?.Dispose();
        _atlas.Dispose();
    }
}
