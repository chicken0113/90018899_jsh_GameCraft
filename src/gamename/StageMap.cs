using Vortice.Mathematics;

class StageMap : IDisposable
{
    public const int TileSize = 128;
    public const int Columns = 20;
    public const int Rows = 16;
    public const int Width = Columns * TileSize;
    public const int Height = Rows * TileSize;

    private readonly string?[,] _tiles = new string?[Columns, Rows];
    private readonly Dictionary<string, G2Texture> _textures = new();
    private readonly (int X, int Y, bool Vertical)[] _doors =
    [
        (9, 11, false),
        (9, 6, false),
        (7, 8, true),
        (12, 8, true)
    ];

    public StageMap()
    {
        AddRoom(7, 11, 12, 14, "hall", "amber");
        AddRoom(7, 6, 12, 11, "club", "magenta");
        AddRoom(7, 1, 12, 6, "service", "cyan");
        AddRoom(2, 6, 7, 10, "lounge", "magenta");
        AddRoom(12, 6, 17, 10, "hall", "cyan");

        foreach (var door in _doors)
        {
            _tiles[door.X, door.Y] = "floor_hall_1";
            if (door.Vertical) _tiles[door.X, door.Y + 1] = "floor_hall_2";
            else _tiles[door.X + 1, door.Y] = "floor_hall_2";
        }

        AddCover(10, 8);
        AddCover(9, 4);
        AddCover(5, 8);
        AddCover(15, 9);

        foreach (string? name in _tiles)
        {
            if (name != null && !_textures.ContainsKey(name))
                _textures.Add(name, new G2Texture($"resource/image/tiles/{name}.png"));
        }
        _textures.Add("door_open_horizontal", new G2Texture("resource/image/tiles/door_open_horizontal.png"));
        _textures.Add("door_open_vertical", new G2Texture("resource/image/tiles/door_open_vertical.png"));
    }

    private void AddRoom(int left, int top, int right, int bottom, string floor, string wall)
    {
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (x == left || x == right)
                    _tiles[x, y] = $"wall_{wall}_vertical";
                else if (y == top || y == bottom)
                    _tiles[x, y] = $"wall_{wall}_horizontal";
                else
                    _tiles[x, y] = $"floor_{floor}_{(x + y) % 2 + 1}";
            }
        }
    }

    private void AddCover(int x, int y)
    {
        _tiles[x, y] = "wall_amber_horizontal";
    }

    public bool CanWalk(float x, float y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
        string? tile = _tiles[(int)x / TileSize, (int)y / TileSize];
        return tile != null && tile.StartsWith("floor_", StringComparison.Ordinal);
    }

    public void Draw(float cameraX, float cameraY, int screenWidth, int screenHeight)
    {
        int left = Math.Max(0, (int)cameraX / TileSize);
        int top = Math.Max(0, (int)cameraY / TileSize);
        int right = Math.Min(Columns - 1, (int)(cameraX + screenWidth) / TileSize);
        int bottom = Math.Min(Rows - 1, (int)(cameraY + screenHeight) / TileSize);

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                string? name = _tiles[x, y];
                if (name == null) continue;
                _textures[name].Draw(
                    new Rect(x * TileSize - cameraX, y * TileSize - cameraY, TileSize, TileSize),
                    new Rect(0, 0, TileSize, TileSize));
            }
        }

        foreach (var door in _doors)
        {
            float x = door.X * TileSize - cameraX;
            float y = door.Y * TileSize - cameraY;
            if (x < -256 || y < -256 || x > screenWidth || y > screenHeight) continue;
            string name = door.Vertical ? "door_open_vertical" : "door_open_horizontal";
            _textures[name].Draw(
                new Rect(x, y, door.Vertical ? 128 : 256, door.Vertical ? 256 : 128),
                new Rect(0, 0, door.Vertical ? 128 : 256, door.Vertical ? 256 : 128));
        }
    }

    public void Dispose()
    {
        foreach (G2Texture texture in _textures.Values) texture.Dispose();
    }
}
