using System.Numerics;
using Vortice.Mathematics;

class GameMain : G2AppBase
{
    private const float MoveSpeed = 240;
    private const float PlayerBulletSpeed = 1800;
    private const float EnemyBulletSpeed = 1500;
    private static readonly bool AudioEnabled = false;

    private enum GameState { Playing, Dead, Cleared }

    private sealed class Enemy(Vector2 position, int kind)
    {
        public Vector2 Position = position;
        public int Kind = kind;
        public bool Alive = true;
        public float Angle;
        public float ShootTimer = 1.6f + kind * 0.25f;
    }

    private sealed class Bullet(Vector2 position, Vector2 velocity, bool fromPlayer)
    {
        public Vector2 Position = position;
        public Vector2 Velocity = velocity;
        public bool FromPlayer = fromPlayer;
    }

    private sealed class Impact(Vector2 position, bool wall)
    {
        public Vector2 Position = position;
        public bool Wall = wall;
        public float Time = 0.24f;
    }

    private readonly List<Enemy> _enemies = new();
    private readonly List<Bullet> _bullets = new();
    private readonly List<Impact> _impacts = new();
    private StageMap? _stageMap;
    private G2Texture? _player;
    private G2Texture? _playerWalk1;
    private G2Texture? _playerWalk2;
    private G2Texture? _enemiesLight;
    private G2Texture? _enemiesHeavy;
    private G2Texture? _playerBullet;
    private G2Texture? _enemyBullet;
    private G2Texture? _effects;
    private G2AudioMp3? _music;
    private G2AudioSound? _shotSound;
    private G2AudioSound? _enemyHitSound;
    private G2AudioSound? _playerHitSound;
    private G2AudioSound? _clearSound;
    private G2Font? _hudFont;
    private G2Font? _messageFont;
    private float _playerX;
    private float _playerY;
    private float _facingAngle;
    private float _targetAngle;
    private float _walkTime;
    private float _fireTimer;
    private bool _walking;
    private bool _mouseAimActive;
    private GameState _state;

    public override System.Drawing.Size ScreenSize => GameGlobal.ScreenSize;
    public override string GameName => GameGlobal.GameName;

    protected override void Initialize()
    {
        ClearColor = new Color4(0.03f, 0.05f, 0.11f, 1.0f);
        _stageMap = new StageMap();
        _player = new G2Texture("resource/image/player.png");
        _playerWalk1 = new G2Texture("resource/image/player_walk_1.png");
        _playerWalk2 = new G2Texture("resource/image/player_walk_2.png");
        _enemiesLight = new G2Texture("resource/image/enemies_light.png");
        _enemiesHeavy = new G2Texture("resource/image/enemies_heavy.png");
        _playerBullet = new G2Texture("resource/image/bullet_player.png");
        _enemyBullet = new G2Texture("resource/image/bullet_enemy.png");
        _effects = new G2Texture("resource/image/effects.png");
        if (AudioEnabled)
        {
            _music = new G2AudioMp3("resource/sound/bgm.mp3");
            _shotSound = new G2AudioSound("resource/sound/shot.wav");
            _enemyHitSound = new G2AudioSound("resource/sound/enemy_hit.wav");
            _playerHitSound = new G2AudioSound("resource/sound/player_hit.wav");
            _clearSound = new G2AudioSound("resource/sound/clear.wav");
        }
        _hudFont = new G2Font("맑은 고딕", 20);
        _messageFont = new G2Font("맑은 고딕", 48,
            textAlignment: Vortice.DirectWrite.TextAlignment.Center);
        ResetGame();
        _music?.Play(true);
    }

    private void ResetGame()
    {
        _playerX = 1280;
        _playerY = 1660;
        _facingAngle = 0;
        _targetAngle = 0;
        _walkTime = 0;
        _fireTimer = 0;
        _walking = false;
        _mouseAimActive = false;
        _state = GameState.Playing;
        _bullets.Clear();
        _impacts.Clear();
        _enemies.Clear();
        _enemies.Add(new Enemy(new Vector2(1100, 1040), 0));
        _enemies.Add(new Enemy(new Vector2(1510, 1190), 1));
        _enemies.Add(new Enemy(new Vector2(560, 930), 2));
        _enemies.Add(new Enemy(new Vector2(830, 1150), 0));
        _enemies.Add(new Enemy(new Vector2(1900, 930), 3));
        _enemies.Add(new Enemy(new Vector2(2130, 1130), 1));
        _enemies.Add(new Enemy(new Vector2(1110, 390), 2));
        _enemies.Add(new Enemy(new Vector2(1510, 580), 3));
    }

    protected override void Update()
    {
        if (Input.IsKeyDown(Keys.R))
        {
            ResetGame();
            return;
        }
        float dt = (float)Math.Min(DeltaTime, 0.05);
        for (int i = _impacts.Count - 1; i >= 0; i--)
        {
            _impacts[i].Time -= dt;
            if (_impacts[i].Time <= 0) _impacts.RemoveAt(i);
        }
        if (_state != GameState.Playing) return;

        _fireTimer = MathF.Max(0, _fireTimer - dt);
        if (MathF.Abs(Input.MouseDelta.X) + MathF.Abs(Input.MouseDelta.Y) > 0.1f)
            _mouseAimActive = true;

        float dx = 0;
        float dy = 0;
        if (Held(Keys.A)) dx--;
        if (Held(Keys.D)) dx++;
        if (Held(Keys.W)) dy--;
        if (Held(Keys.S)) dy++;

        bool firing = Input.IsButtonDown(MouseButtons.Left) || Input.IsButtonPress(MouseButtons.Left);
        if (dx != 0 || dy != 0)
        {
            if (dx != 0 && dy != 0)
            {
                dx *= 0.7071068f;
                dy *= 0.7071068f;
            }
            float oldX = _playerX;
            float oldY = _playerY;
            float nextX = _playerX + dx * MoveSpeed * dt;
            float nextY = _playerY + dy * MoveSpeed * dt;
            if (CanWalk(nextX, _playerY)) _playerX = nextX;
            if (CanWalk(_playerX, nextY)) _playerY = nextY;
            _walking = _playerX != oldX || _playerY != oldY;
            _targetAngle = MathF.Atan2(dy, dx);
        }
        else
        {
            _walking = false;
        }

        if (_walking) _walkTime += dt;
        else _walkTime = 0;

        Vector2 playerPosition = new(_playerX, _playerY);
        Vector2 mouseWorld = new(Input.MousePosition.X + CameraX(), Input.MousePosition.Y + CameraY());
        Vector2 aim = mouseWorld - playerPosition;
        if (aim.LengthSquared() > 100 && (firing || (!_walking && _mouseAimActive)))
            _targetAngle = MathF.Atan2(aim.Y, aim.X);
        TurnTowardsTarget(dt);

        if (firing && _fireTimer <= 0 && aim.LengthSquared() > 100)
        {
            Vector2 direction = Vector2.Normalize(aim);
            _bullets.Add(new Bullet(playerPosition + direction * 44, direction * PlayerBulletSpeed, true));
            _shotSound?.Play();
            _fireTimer = 0.18f;
        }

        UpdateEnemies(dt);
        UpdateBullets(dt);
        if (_state == GameState.Playing && _enemies.All(enemy => !enemy.Alive))
        {
            _state = GameState.Cleared;
            _clearSound?.Play();
        }
    }

    private void UpdateEnemies(float dt)
    {
        Vector2 playerPosition = new(_playerX, _playerY);
        foreach (Enemy enemy in _enemies)
        {
            if (!enemy.Alive) continue;
            enemy.ShootTimer -= dt;
            Vector2 toPlayer = playerPosition - enemy.Position;
            float distance = toPlayer.Length();
            if (distance < 1 || distance > 500 || !HasLineOfSight(enemy.Position, playerPosition))
                continue;

            Vector2 direction = toPlayer / distance;
            enemy.Angle = MathF.Atan2(direction.Y, direction.X);
            if (distance > 175 && distance < 350)
            {
                Vector2 next = enemy.Position + direction * (75 * dt);
                if (CanWalk(next.X, enemy.Position.Y)) enemy.Position.X = next.X;
                if (CanWalk(enemy.Position.X, next.Y)) enemy.Position.Y = next.Y;
            }
            if (distance < 430 && enemy.ShootTimer <= 0)
            {
                _bullets.Add(new Bullet(enemy.Position + direction * 36, direction * EnemyBulletSpeed, false));
                enemy.ShootTimer = 1.1f + enemy.Kind * 0.1f;
            }
        }
    }

    private void UpdateBullets(float dt)
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            Bullet bullet = _bullets[i];
            Vector2 end = bullet.Position + bullet.Velocity * dt;
            int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(bullet.Position, end) / 8));
            bool remove = false;
            for (int step = 1; step <= steps; step++)
            {
                Vector2 point = Vector2.Lerp(bullet.Position, end, (float)step / steps);
                if (!CanWalk(point.X, point.Y))
                {
                    _impacts.Add(new Impact(point, true));
                    remove = true;
                    break;
                }
                if (bullet.FromPlayer)
                {
                    foreach (Enemy enemy in _enemies)
                    {
                        if (!enemy.Alive || Vector2.DistanceSquared(point, enemy.Position) > 28 * 28) continue;
                        enemy.Alive = false;
                        _impacts.Add(new Impact(enemy.Position, false));
                        _enemyHitSound?.Play();
                        remove = true;
                        break;
                    }
                }
                else if (Vector2.DistanceSquared(point, new Vector2(_playerX, _playerY)) <= 26 * 26)
                {
                    _state = GameState.Dead;
                    _impacts.Add(new Impact(new Vector2(_playerX, _playerY), false));
                    _playerHitSound?.Play();
                    remove = true;
                }
                if (remove) break;
            }
            if (_state == GameState.Dead)
            {
                _bullets.Clear();
                return;
            }
            if (remove) _bullets.RemoveAt(i);
            else bullet.Position = end;
        }
    }

    private bool HasLineOfSight(Vector2 start, Vector2 end)
    {
        int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(start, end) / 16));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(start, end, (float)i / steps);
            if (!CanWalk(point.X, point.Y)) return false;
        }
        return true;
    }

    private void TurnTowardsTarget(float dt)
    {
        float difference = MathF.Atan2(
            MathF.Sin(_targetAngle - _facingAngle),
            MathF.Cos(_targetAngle - _facingAngle));
        _facingAngle += Math.Clamp(difference, -14 * dt, 14 * dt);
    }

    protected override void Render()
    {
        float cameraX = CameraX();
        float cameraY = CameraY();
        _stageMap?.Draw(cameraX, cameraY, ScreenSize.Width, ScreenSize.Height);

        foreach (Enemy enemy in _enemies)
        {
            if (!enemy.Alive) continue;
            float x = enemy.Position.X - cameraX;
            float y = enemy.Position.Y - cameraY;
            G2Texture? texture = enemy.Kind < 2 ? _enemiesLight : _enemiesHeavy;
            float sourceY = enemy.Kind % 2 == 0 ? 150 : 918;
            DrawRotated(texture, new Rect(x - 61, y - 46, 122, 92),
                new Rect(250, sourceY, 700, 500), enemy.Angle, x, y);
        }

        foreach (Bullet bullet in _bullets)
        {
            float x = bullet.Position.X - cameraX;
            float y = bullet.Position.Y - cameraY;
            G2Texture? texture = bullet.FromPlayer ? _playerBullet : _enemyBullet;
            texture?.Draw(new Rect(x - 10, y - 10, 20, 20), new Rect(0, 0, 32, 32));
        }

        foreach (Impact impact in _impacts)
        {
            float x = impact.Position.X - cameraX;
            float y = impact.Position.Y - cameraY;
            Rect source = impact.Wall ? new Rect(768, 0, 768, 512) : new Rect(0, 512, 768, 512);
            _effects?.Draw(new Rect(x - 55, y - 40, 110, 80), source, impact.Time / 0.24f);
        }

        int frame = _walking ? (int)(_walkTime / 0.12f) % 4 : 0;
        G2Texture? playerSprite = frame switch
        {
            1 => _playerWalk1,
            3 => _playerWalk2,
            _ => _player
        };
        float playerScreenX = _playerX - cameraX;
        float playerScreenY = _playerY - cameraY;
        DrawRotated(playerSprite,
            new Rect(playerScreenX - 52, playerScreenY - 48, 104, 96),
            new Rect(300, 300, 800, 750), _facingAngle, playerScreenX, playerScreenY);

        int remaining = _enemies.Count(enemy => enemy.Alive);
        _hudFont?.DrawText($"남은 적: {remaining}    WASD 이동  마우스 발사  R 재시작",
            new Rect(20, 12, 800, 40), new Color4(1, 1, 1, 1));
        if (_state == GameState.Dead)
            _messageFont?.DrawText("사망  |  R로 재시작",
                new Rect(0, 270, ScreenSize.Width, 100), new Color4(1, 0.35f, 0.43f, 1));
        else if (_state == GameState.Cleared)
            _messageFont?.DrawText("클리어!  |  R로 다시 시작",
                new Rect(0, 270, ScreenSize.Width, 100), new Color4(0.35f, 1, 0.82f, 1));
    }

    private void DrawRotated(G2Texture? texture, Rect destination, Rect source,
        float angle, float centerX, float centerY)
    {
        Matrix3x2 original = RenderTarget.Transform;
        RenderTarget.Transform = Matrix3x2.CreateRotation(angle, new Vector2(centerX, centerY)) * original;
        texture?.Draw(destination, source);
        RenderTarget.Transform = original;
    }

    private float CameraX() => Math.Clamp(_playerX - ScreenSize.Width / 2f, 0, StageMap.Width - ScreenSize.Width);
    private float CameraY() => Math.Clamp(_playerY - ScreenSize.Height / 2f, 0, StageMap.Height - ScreenSize.Height);
    private bool Held(Keys key) => Input.IsKeyDown(key) || Input.IsKeyPress(key);

    private bool CanWalk(float x, float y) => _stageMap?.CanWalk(x, y) ?? false;

    public override void Dispose()
    {
        _messageFont?.Dispose();
        _hudFont?.Dispose();
        _clearSound?.Dispose();
        _playerHitSound?.Dispose();
        _enemyHitSound?.Dispose();
        _shotSound?.Dispose();
        _music?.Dispose();
        _enemyBullet?.Dispose();
        _effects?.Dispose();
        _playerBullet?.Dispose();
        _enemiesHeavy?.Dispose();
        _enemiesLight?.Dispose();
        _playerWalk2?.Dispose();
        _playerWalk1?.Dispose();
        _player?.Dispose();
        _stageMap?.Dispose();
        base.Dispose();
    }
}
