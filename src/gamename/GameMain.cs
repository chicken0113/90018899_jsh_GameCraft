using System.Numerics;
using Vortice.Mathematics;

class GameMain : G2AppBase
{
    private const float StageWidth = 1536;
    private const float StageHeight = 1280;
    private const float MoveSpeed = 240;

    private G2Texture? _stage;
    private G2Texture? _player;
    private G2Texture? _playerWalk1;
    private G2Texture? _playerWalk2;
    private G2AudioMp3? _music;
    private float _playerX = 320;
    private float _playerY = 320;
    private float _facingAngle;
    private float _walkTime;
    private bool _walking;

    public override System.Drawing.Size ScreenSize => GameGlobal.ScreenSize;
    public override string GameName => GameGlobal.GameName;

    protected override void Initialize()
    {
        ClearColor = new Color4(0.03f, 0.05f, 0.11f, 1.0f);
        _stage = new G2Texture("resource/image/stage01.png");
        _player = new G2Texture("resource/image/player.png");
        _playerWalk1 = new G2Texture("resource/image/player_walk_1.png");
        _playerWalk2 = new G2Texture("resource/image/player_walk_2.png");
        _music = new G2AudioMp3("resource/sound/bgm.mp3");
        _music.Play(true);
    }

    protected override void Update()
    {
        if (Input.IsKeyDown(Keys.R))
        {
            _playerX = 320;
            _playerY = 320;
            _facingAngle = 0;
            _walkTime = 0;
            _walking = false;
            return;
        }

        float dx = 0;
        float dy = 0;
        if (Held(Keys.A)) dx--;
        if (Held(Keys.D)) dx++;
        if (Held(Keys.W)) dy--;
        if (Held(Keys.S)) dy++;

        if (dx == 0 && dy == 0)
        {
            _walking = false;
            _walkTime = 0;
            return;
        }

        _facingAngle = MathF.Atan2(dy, dx);
        if (dx != 0 && dy != 0)
        {
            dx *= 0.7071068f;
            dy *= 0.7071068f;
        }

        float distance = MoveSpeed * (float)Math.Min(DeltaTime, 0.05);
        float oldX = _playerX;
        float oldY = _playerY;
        float nextX = _playerX + dx * distance;
        float nextY = _playerY + dy * distance;
        if (CanWalk(nextX, _playerY)) _playerX = nextX;
        if (CanWalk(_playerX, nextY)) _playerY = nextY;

        _walking = _playerX != oldX || _playerY != oldY;
        if (_walking) _walkTime += (float)DeltaTime;
        else _walkTime = 0;
    }

    protected override void Render()
    {
        float cameraX = Math.Clamp(_playerX - ScreenSize.Width / 2f, 0, StageWidth - ScreenSize.Width);
        float cameraY = Math.Clamp(_playerY - ScreenSize.Height / 2f, 0, StageHeight - ScreenSize.Height);

        _stage?.Draw(
            new Rect(0, 0, ScreenSize.Width, ScreenSize.Height),
            new Rect(cameraX, cameraY, ScreenSize.Width, ScreenSize.Height));
        int frame = _walking ? (int)(_walkTime / 0.09f) % 4 : 0;
        G2Texture? sprite = frame switch
        {
            1 => _playerWalk1,
            3 => _playerWalk2,
            _ => _player
        };

        float screenX = _playerX - cameraX;
        float screenY = _playerY - cameraY;
        Matrix3x2 originalTransform = RenderTarget.Transform;
        RenderTarget.Transform = Matrix3x2.CreateRotation(_facingAngle, new Vector2(screenX, screenY)) * originalTransform;
        sprite?.Draw(
            new Rect(screenX - 52, screenY - 48, 104, 96),
            new Rect(300, 300, 800, 750));
        RenderTarget.Transform = originalTransform;
    }

    private bool Held(Keys key) => Input.IsKeyDown(key) || Input.IsKeyPress(key);

    private static bool Inside(float x, float y, float left, float top, float right, float bottom)
        => x >= left && x <= right && y >= top && y <= bottom;

    private static bool CanWalk(float x, float y)
    {
        return Inside(x, y, 125, 125, 510, 510)
            || Inside(x, y, 500, 270, 900, 480)
            || Inside(x, y, 885, 125, 1410, 510)
            || Inside(x, y, 1030, 500, 1260, 900)
            || Inside(x, y, 885, 885, 1410, 1155);
    }

    public override void Dispose()
    {
        _music?.Dispose();
        _playerWalk2?.Dispose();
        _playerWalk1?.Dispose();
        _player?.Dispose();
        _stage?.Dispose();
        base.Dispose();
    }
}
