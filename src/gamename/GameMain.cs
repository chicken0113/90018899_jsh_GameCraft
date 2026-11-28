using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;

class GameMain : G2AppBase
{
    private const float MoveSpeed = 135;
    private const float PlayerBulletSpeed = 900;
    private const float EnemyBulletSpeed = 750;
    private const float EnemyDetectionRange = 155;
    private const float GunWindup = 0.9f;
    private const float MeleeWindup = 0.55f;
    private static readonly bool AudioEnabled = true;
    private static readonly string[] AreaNames = ["1층", "2층 북서", "2층 동쪽", "2층 남동"];
    private static readonly string[] Guns = ["sprEWalkM16", "sprEWalkShotgun", "sprEWalkDoubleBarrel"];
    private static readonly string[] MeleeWeapons = ["sprEWalkClub", "sprEWalkBat", "sprEWalkPipe", "sprEWalkKnife"];

    private enum GameState { Title, Playing, Dead, StageComplete, GameComplete }

    private sealed class Enemy(StageMap.EnemySpawn spawn, string sprite, int index)
    {
        public readonly StageMap.EnemySpawn Spawn = spawn;
        public readonly string Sprite = sprite;
        public Vector2 Position = new(spawn.X, spawn.Y);
        public float Angle = -spawn.Angle * MathF.PI / 180;
        public int PatrolTarget = 1;
        public int PatrolStep = 1;
        public readonly List<Vector2> ChaseTrail = new();
        public float StunTime;
        public float AttackTimer = 1.25f + (index % 4) * 0.2f;
        public float Windup;
        public float WalkTime;
        public bool Alive = true;
    }

    private sealed class Bullet(Vector2 position, Vector2 velocity, bool fromPlayer)
    {
        public Vector2 Position = position;
        public readonly Vector2 Velocity = velocity;
        public readonly bool FromPlayer = fromPlayer;
    }

    private sealed class Impact(Vector2 position, bool wall)
    {
        public readonly Vector2 Position = position;
        public readonly bool Wall = wall;
        public float Time = 0.24f;
    }

    private readonly List<Bullet> _bullets = new();
    private readonly List<Impact> _impacts = new();
    private StageMap _stageMap = null!;
    private Enemy[][] _enemies = [];
    private G2AudioMp3? _music;
    private G2AudioSound? _shotSound;
    private G2AudioSound? _enemyHitSound;
    private G2AudioSound? _playerHitSound;
    private G2AudioSound? _clearSound;
    private G2Font? _hudFont;
    private G2Font? _messageFont;
    private G2Font? _titleFont;
    private G2Font? _buttonFont;
    private ID2D1SolidColorBrush? _titlePanel;
    private ID2D1SolidColorBrush? _startButton;
    private Vector2 _player;
    private float _facingAngle;
    private float _walkTime;
    private float _fireTimer;
    private float _transitionTimer;
    private int _area;
    private int _stageIndex;
    private bool _hasFired;
    private bool _clearAnnounced;
    private float _clearNoticeTime;
    private GameState _state;

    private Enemy[] CurrentEnemies => _enemies[_area];
    private bool AreaCleared => CurrentEnemies.All(enemy => !enemy.Alive);
    private bool AllCleared => _enemies.All(area => area.All(enemy => !enemy.Alive));

    public override System.Drawing.Size ScreenSize => GameGlobal.ScreenSize;
    public override string GameName => GameGlobal.GameName;

    protected override void Initialize()
    {
        ClearColor = new Color4(0.03f, 0.05f, 0.11f, 1);
        if (AudioEnabled && G2AudioContext.Instance?.Audio != null)
        {
            G2AudioContext.Instance.MasteringVoice?.SetVolume(0.35f);
            _music = new G2AudioMp3("resource/sound/bgm.mp3");
            _shotSound = new G2AudioSound("resource/sound/shot.wav");
            _enemyHitSound = new G2AudioSound("resource/sound/enemy_hit.wav");
            _playerHitSound = new G2AudioSound("resource/sound/player_hit.wav");
            _clearSound = new G2AudioSound("resource/sound/clear.wav");
        }

        _hudFont = new G2Font("맑은 고딕", 20);
        _messageFont = new G2Font("맑은 고딕", 48,
            textAlignment: Vortice.DirectWrite.TextAlignment.Center);
        _titleFont = new G2Font("맑은 고딕", 68,
            textAlignment: Vortice.DirectWrite.TextAlignment.Center);
        _buttonFont = new G2Font("맑은 고딕", 28,
            textAlignment: Vortice.DirectWrite.TextAlignment.Center);
        _titlePanel = RenderTarget.CreateSolidColorBrush(new Color4(0.015f, 0.025f, 0.07f, 0.9f));
        _startButton = RenderTarget.CreateSolidColorBrush(new Color4(0.08f, 0.35f, 0.44f, 0.96f));
        StartStage(0);
        _state = GameState.Title;
        _music?.Play(true);
    }

    private void StartStage(int index)
    {
        StageMap next = new(index);
        _stageMap?.Dispose();
        _stageMap = next;
        _stageIndex = index;
        ResetStage();
    }

    private void ResetStage()
    {
        _area = 0;
        _player = new Vector2(_stageMap.Start.X, _stageMap.Start.Y);
        _facingAngle = -MathF.PI / 2;
        _walkTime = _fireTimer = _transitionTimer = 0;
        _state = GameState.Playing;
        _hasFired = false;
        _clearAnnounced = false;
        _clearNoticeTime = 0;
        _bullets.Clear();
        _impacts.Clear();
        _stageMap.Reset();
        _enemies = _stageMap.Areas.Select(area => area.Enemies.Select(CreateEnemy).ToArray()).ToArray();
    }

    private static Enemy CreateEnemy(StageMap.EnemySpawn spawn, int index)
    {
        string sprite = spawn.Kind switch
        {
            "dog" => "sprDogWalk",
            "heavy" => "sprEFatWalk",
            "melee" => MeleeWeapons[index % MeleeWeapons.Length],
            _ => Guns[index % Guns.Length]
        };
        return new Enemy(spawn, sprite, index);
    }

    protected override void Update()
    {
        if (_state == GameState.Title)
        {
            System.Drawing.PointF mouse = Input.MousePosition;
            bool startClicked = Input.IsButtonDown(MouseButtons.Left) &&
                mouse.X >= 360 && mouse.X <= 600 && mouse.Y >= 365 && mouse.Y <= 430;
            if (Input.IsKeyDown(Keys.Enter) || Input.IsKeyDown(Keys.Space) || startClicked)
            {
                ResetStage();
                _fireTimer = 0.25f;
            }
            return;
        }

        if (_state == GameState.GameComplete)
        {
            System.Drawing.PointF mouse = Input.MousePosition;
            bool returnClicked = Input.IsButtonDown(MouseButtons.Left) &&
                mouse.X >= 340 && mouse.X <= 620 && mouse.Y >= 400 && mouse.Y <= 470;
            if (Input.IsKeyDown(Keys.Enter) || Input.IsKeyDown(Keys.R) || returnClicked)
            {
                StartStage(0);
                _state = GameState.Title;
            }
            return;
        }

        if (Input.IsKeyDown(Keys.R))
        {
            ResetStage();
            return;
        }

        if (_state == GameState.StageComplete && Input.IsKeyDown(Keys.Enter))
        {
            StartStage(_stageIndex + 1);
            return;
        }

        float dt = (float)Math.Min(DeltaTime, 0.05);
        _clearNoticeTime = MathF.Max(0, _clearNoticeTime - dt);
        for (int i = _impacts.Count - 1; i >= 0; i--)
        {
            _impacts[i].Time -= dt;
            if (_impacts[i].Time <= 0) _impacts.RemoveAt(i);
        }

        if (_state != GameState.Playing) return;

        _fireTimer -= dt;
        _transitionTimer -= dt;
        UpdateDoors(dt);
        Vector2 movement = new(
            (Held(Keys.D) ? 1 : 0) - (Held(Keys.A) ? 1 : 0),
            (Held(Keys.S) ? 1 : 0) - (Held(Keys.W) ? 1 : 0));
        Vector2 previousPosition = _player;
        if (movement != Vector2.Zero)
            _player = Move(_player, Vector2.Normalize(movement) * MoveSpeed * dt, player: true);
        _walkTime = previousPosition == _player ? 0 : _walkTime + dt;

        if (_transitionTimer <= 0 && AreaCleared && TryChangeArea(previousPosition))
            return;

        if (_area == 0 && AllCleared && Vector2.Distance(_player,
            new Vector2(_stageMap.Finish.X, _stageMap.Finish.Y)) < 40)
        {
            _state = _stageIndex == StageMap.StageCount - 1 ? GameState.GameComplete : GameState.StageComplete;
            return;
        }

        Vector2 aim = new Vector2(Input.MousePosition.X, Input.MousePosition.Y) / StageMap.Scale + Camera() - _player;
        if (aim.LengthSquared() > 1)
        {
            _facingAngle = MathF.Atan2(aim.Y, aim.X);
            if (_fireTimer <= 0 && (Input.IsButtonDown(MouseButtons.Left) || Input.IsButtonPress(MouseButtons.Left)))
            {
                _hasFired = true;
                _bullets.Add(new Bullet(_player, Vector2.Normalize(aim) * PlayerBulletSpeed, true));
                _shotSound?.Play();
                _fireTimer = 0.18f;
            }
        }

        UpdateEnemies(dt);
        if (_state == GameState.Playing) UpdateBullets(dt);
    }

    private bool TryChangeArea(Vector2 previousPosition)
    {
        foreach (StageMap.Exit exit in _stageMap.Areas[_area].Exits)
        {
            if (!exit.Crossed(previousPosition, _player)) continue;

            Vector2 destination = _player + new Vector2(exit.OffsetX, exit.OffsetY);
            if (!_stageMap.CanWalk(exit.Target, destination))
                destination = new Vector2(exit.X + exit.OffsetX, exit.Y + exit.OffsetY);

            _area = exit.Target;
            _player = destination;
            _transitionTimer = 0.45f;
            _bullets.Clear();
            _impacts.Clear();
            return true;
        }

        return false;
    }

    private Vector2 Move(Vector2 position, Vector2 delta, bool player = false, List<Vector2>? trail = null)
    {
        int steps = Math.Max(1, (int)MathF.Ceiling(delta.Length() / 1.5f));
        Vector2 step = delta / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector2 horizontal = new(position.X + step.X, position.Y);
            if (step.X != 0 && _stageMap.TryStep(_area, position, horizontal, player))
            {
                RememberStep(trail, position, horizontal);
                position = horizontal;
            }

            Vector2 vertical = new(position.X, position.Y + step.Y);
            if (step.Y != 0 && _stageMap.TryStep(_area, position, vertical, player))
            {
                RememberStep(trail, position, vertical);
                position = vertical;
            }
        }

        return position;
    }

    private static void RememberStep(List<Vector2>? trail, Vector2 from, Vector2 to)
    {
        if (trail is null) return;
        if (trail.Count > 0)
        {
            Vector2 previous = from - trail[^1];
            Vector2 next = to - from;
            if (previous == Vector2.Zero || (previous.X * next.Y == previous.Y * next.X &&
                Vector2.Dot(previous, next) > 0))
                return;
        }

        trail.Add(from);
    }

    private void UpdateDoors(float dt)
    {
        Enemy[] enemies = CurrentEnemies;
        Vector2[] occupied = enemies.Where(enemy => enemy.Alive).Select(enemy => enemy.Position).ToArray();
        foreach (StageMap.Door door in _stageMap.Areas[_area].Doors)
        {
            door.Update(dt, _player, occupied);
            if (!door.PlayerSwinging) continue;
            foreach (Enemy enemy in enemies)
            {
                if (enemy.Alive && door.HitByPlayerSwing(enemy.Position))
                    enemy.StunTime = 1.2f;
            }
        }
    }

    private void Patrol(Enemy enemy, float dt)
    {
        if (enemy.Spawn.Route is not { Length: > 1 } route) return;

        float distanceLeft = (enemy.Spawn.Kind == "dog" ? 85 : 55) * dt;
        while (distanceLeft > 0 && enemy.ChaseTrail.Count > 0)
        {
            Vector2 target = enemy.ChaseTrail[^1];
            float distance = Vector2.Distance(enemy.Position, target);
            if (!FollowPatrolPoint(enemy, target, distanceLeft)) return;
            enemy.ChaseTrail.RemoveAt(enemy.ChaseTrail.Count - 1);
            distanceLeft -= distance;
        }

        if (distanceLeft <= 0) return;
        StageMap.Point point = route[enemy.PatrolTarget];
        if (!FollowPatrolPoint(enemy, new Vector2(point.X, point.Y), distanceLeft)) return;
        if (enemy.PatrolTarget == 0 || enemy.PatrolTarget == route.Length - 1)
            enemy.PatrolStep = -enemy.PatrolStep;
        enemy.PatrolTarget += enemy.PatrolStep;
    }

    private bool FollowPatrolPoint(Enemy enemy, Vector2 target, float budget)
    {
        Vector2 direction = target - enemy.Position;
        float distance = direction.Length();
        if (distance > 0.01f)
        {
            enemy.Angle = MathF.Atan2(direction.Y, direction.X);
            enemy.Position = Move(enemy.Position, direction * (MathF.Min(budget, distance) / distance));
        }

        return Vector2.DistanceSquared(enemy.Position, target) <= 0.01f * 0.01f;
    }

    private void UpdateEnemies(float dt)
    {
        foreach (Enemy enemy in CurrentEnemies)
        {
            if (!enemy.Alive) continue;
            if (enemy.StunTime > 0)
            {
                enemy.StunTime -= dt;
                enemy.WalkTime = 0;
                continue;
            }
            enemy.AttackTimer = MathF.Max(0, enemy.AttackTimer - dt);
            Vector2 toPlayer = _player - enemy.Position;
            float distance = toPlayer.Length();
            bool seesPlayer = distance > 0 && distance < EnemyDetectionRange && HasLineOfSight(enemy.Position, _player);
            bool closeRange = enemy.Spawn.Kind is "melee" or "dog" or "heavy";
            Vector2 previous = enemy.Position;

            if (seesPlayer)
            {
                Vector2 direction = toPlayer / distance;
                enemy.Angle = MathF.Atan2(direction.Y, direction.X);
                bool inAttackRange = !closeRange || distance <= 17;
                if (inAttackRange && enemy.Windup > 0)
                {
                    enemy.Windup -= dt;
                    if (enemy.Windup <= 0)
                    {
                        enemy.Windup = 0;
                        enemy.AttackTimer = closeRange ? 1.2f : enemy.Sprite == Guns[0] ? 1.5f : 1.9f;
                        if (closeRange)
                        {
                            KillPlayer();
                            return;
                        }
                        _bullets.Add(new Bullet(enemy.Position, direction * EnemyBulletSpeed, false));
                    }
                }
                else if (inAttackRange && enemy.AttackTimer <= 0)
                {
                    enemy.Windup = closeRange ? MeleeWindup : GunWindup;
                }
                else if (closeRange && !inAttackRange)
                {
                    enemy.Windup = 0;
                }

                if (enemy.Windup <= 0 && (closeRange || (!enemy.Spawn.Stationary && distance > 90)))
                {
                    float speed = enemy.Spawn.Kind == "dog" ? 175 : closeRange ? 105 : 60;
                    enemy.Position = Move(enemy.Position, direction * speed * dt,
                        trail: enemy.Spawn.Patrol ? enemy.ChaseTrail : null);
                }
            }
            else
            {
                enemy.Windup = 0;
                if (enemy.Spawn.Patrol) Patrol(enemy, dt);
            }

            enemy.WalkTime = enemy.Position == previous ? 0 : enemy.WalkTime + dt;
        }
    }

    private bool HasLineOfSight(Vector2 start, Vector2 end)
    {
        int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(start, end) / 3));
        for (int i = 1; i <= steps; i++)
        {
            if (!_stageMap.CanShoot(_area, Vector2.Lerp(start, end, (float)i / steps)))
                return false;
        }
        return true;
    }

    private void UpdateBullets(float dt)
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            Bullet bullet = _bullets[i];
            Vector2 end = bullet.Position + bullet.Velocity * dt;
            int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(bullet.Position, end) / 2));
            bool remove = false;
            for (int step = 1; step <= steps && !remove; step++)
            {
                Vector2 point = Vector2.Lerp(bullet.Position, end, (float)step / steps);
                if (!_stageMap.CanShoot(_area, point))
                {
                    _impacts.Add(new Impact(point, true));
                    remove = true;
                }
                else if (bullet.FromPlayer)
                {
                    foreach (Enemy enemy in CurrentEnemies)
                    {
                        if (!enemy.Alive || Vector2.DistanceSquared(point, enemy.Position) > 10 * 10) continue;
                        enemy.Alive = false;
                        _impacts.Add(new Impact(enemy.Position, false));
                        _enemyHitSound?.Play();
                        if (!_clearAnnounced && AllCleared)
                        {
                            _clearAnnounced = true;
                            _clearNoticeTime = 2.2f;
                            _clearSound?.Play();
                        }
                        remove = true;
                        break;
                    }
                }
                else if (Vector2.DistanceSquared(point, _player) <= 8 * 8)
                {
                    KillPlayer();
                    return;
                }
            }

            if (remove) _bullets.RemoveAt(i);
            else bullet.Position = end;
        }
    }

    private void KillPlayer()
    {
        _state = GameState.Dead;
        _impacts.Add(new Impact(_player, false));
        _playerHitSound?.Play();
        _bullets.Clear();
    }

    protected override void Render()
    {
        if (_state == GameState.Title)
        {
            RenderTitle();
            return;
        }
        if (_state == GameState.GameComplete)
        {
            RenderGameComplete();
            return;
        }

        Vector2 camera = Camera();
        _stageMap.Draw(_area, camera, AreaCleared);
        if (AllCleared && _area == 0)
            _stageMap.DrawSprite("sprGoArrow", _stageIndex == 1 ? 1 : 3,
                new Vector2(_stageMap.Finish.X, _stageMap.Finish.Y + (_stageIndex == 1 ? 32 : 0)), camera, 0);
        foreach (Enemy enemy in CurrentEnemies)
        {
            if (enemy.Alive)
            {
                _stageMap.DrawSprite(enemy.Sprite, WalkFrame(enemy.WalkTime), enemy.Position, camera,
                    enemy.Angle + (enemy.StunTime > 0 ? MathF.PI / 2 : 0));
                if (enemy.Windup > 0)
                {
                    Vector2 screen = (enemy.Position - camera) * StageMap.Scale;
                    _hudFont?.DrawText("!", new Rect(screen.X - 10, screen.Y - 45, 25, 30),
                        new Color4(1, 0.22f, 0.2f, 1));
                }
            }
        }

        foreach (Bullet bullet in _bullets)
        {
            float angle = MathF.Atan2(bullet.Velocity.Y, bullet.Velocity.X);
            _stageMap.DrawSprite(bullet.FromPlayer ? "sprBullet" : "sprEnemyBullet",
                0, bullet.Position, camera, angle);
        }

        foreach (Impact impact in _impacts)
        {
            int frame = (int)((0.24f - impact.Time) * 12);
            _stageMap.DrawSprite(impact.Wall ? "sprBulletHit" : "sprBloodSplatSmall",
                frame, impact.Position, camera, 0);
        }

        if (_state != GameState.Dead)
            _stageMap.DrawSprite(_hasFired ? "sprPWalkSilencer" : "sprPWalkUnarmed",
                WalkFrame(_walkTime), _player, camera, _facingAngle);

        int remaining = CurrentEnemies.Count(enemy => enemy.Alive);
        string status = AllCleared ?
            (_stageIndex == StageMap.StageCount - 1 ? "입구 차로 돌아가세요" : "처음 입구로 돌아가세요") :
            remaining == 0 ? "계단으로 이동하세요" : $"남은 적 {remaining}";
        string areaName = _stageIndex == StageMap.StageCount - 1 ? AreaNames[_area] :
            _stageIndex == 0 ? (_area == 0 ? "역 입구" : "역 내부") :
            (_area == 0 ? "아파트 입구" : "아파트 2층");
        _hudFont?.DrawText($"{areaName}  |  {status}",
            new Rect(20, 12, 900, 40), new Color4(1, 1, 1, 1));
        _hudFont?.DrawText("WASD 이동 · 마우스 발사 · R 재시작",
            new Rect(20, 44, 900, 35), new Color4(0.78f, 0.88f, 0.95f, 1));

        if (_clearNoticeTime > 0 && _state == GameState.Playing)
        {
            RenderTarget.FillRectangle(new Rect(260, 255, 440, 90), _titlePanel!);
            _messageFont?.DrawText("클리어!",
                new Rect(0, 250, ScreenSize.Width, 100), new Color4(0.35f, 1, 0.82f, 1));
        }

        if (_state == GameState.Dead)
            _messageFont?.DrawText("사망  |  R로 재시작",
                new Rect(0, 270, ScreenSize.Width, 100), new Color4(1, 0.35f, 0.43f, 1));
        else if (_state == GameState.StageComplete)
            _messageFont?.DrawText("다음 스테이지",
                new Rect(0, 240, ScreenSize.Width, 100), new Color4(0.35f, 1, 0.82f, 1));

        if (_state == GameState.StageComplete)
            _hudFont?.DrawText("Enter 다음 스테이지 · R 현재 스테이지 다시 시작",
                new Rect(180, 340, 700, 40), new Color4(1, 1, 1, 1));
    }

    private void RenderTitle()
    {
        Vector2 camera = Camera();
        _stageMap.Draw(_area, camera, false);
        RenderTarget.FillRectangle(new Rect(0, 0, ScreenSize.Width, ScreenSize.Height), _titlePanel!);
        _titleFont?.DrawText("Shooter Lounge", new Rect(80, 145, 800, 110), new Color4(1, 0.32f, 0.56f, 1));
        _hudFont?.DrawText("원샷원킬 탑다운 액션",
            new Rect(260, 265, 600, 45), new Color4(0.73f, 0.89f, 0.95f, 1));
        RenderTarget.FillRectangle(new Rect(360, 365, 240, 65), _startButton!);
        _buttonFont?.DrawText("게임 시작", new Rect(360, 376, 240, 50), new Color4(1, 1, 1, 1));
        _hudFont?.DrawText("Enter 또는 버튼 클릭",
            new Rect(360, 445, 300, 35), new Color4(0.86f, 0.91f, 0.95f, 1));
        _hudFont?.DrawText("WASD 이동  ·  마우스 조준·발사  ·  R 스테이지 재시작",
            new Rect(245, 540, 600, 35), new Color4(0.74f, 0.8f, 0.9f, 1));
    }

    private void RenderGameComplete()
    {
        RenderTarget.FillRectangle(new Rect(0, 0, ScreenSize.Width, ScreenSize.Height), _titlePanel!);
        RenderTarget.FillRectangle(new Rect(140, 155, 680, 4), _startButton!);
        _hudFont?.DrawText("Shooter Lounge", new Rect(390, 95, 300, 40),
            new Color4(1, 0.32f, 0.56f, 1));
        _titleFont?.DrawText("GAME CLEAR", new Rect(80, 185, 800, 115),
            new Color4(0.35f, 1, 0.82f, 1));
        _buttonFont?.DrawText("모든 스테이지 클리어!", new Rect(230, 312, 600, 55),
            new Color4(1, 1, 1, 1));
        RenderTarget.FillRectangle(new Rect(340, 400, 280, 70), _startButton!);
        _buttonFont?.DrawText("타이틀로 돌아가기", new Rect(340, 414, 280, 52),
            new Color4(1, 1, 1, 1));
        _hudFont?.DrawText("Enter 또는 R · 버튼 클릭", new Rect(340, 500, 360, 40),
            new Color4(0.82f, 0.9f, 0.94f, 1));
    }

    private Vector2 Camera()
    {
        StageMap.Area area = _stageMap.Areas[_area];
        Vector2 viewSize = new Vector2(ScreenSize.Width, ScreenSize.Height) / StageMap.Scale;
        return Vector2.Clamp(_player - viewSize / 2, Vector2.Zero,
            new Vector2(area.Width, area.Height) - viewSize);
    }

    private static int WalkFrame(float time) => (int)(time * 12) % 8;

    private bool Held(Keys key) => Input.IsKeyDown(key) || Input.IsKeyPress(key);

    public override void Dispose()
    {
        _startButton?.Dispose();
        _titlePanel?.Dispose();
        _buttonFont?.Dispose();
        _titleFont?.Dispose();
        _messageFont?.Dispose();
        _hudFont?.Dispose();
        _clearSound?.Dispose();
        _playerHitSound?.Dispose();
        _enemyHitSound?.Dispose();
        _shotSound?.Dispose();
        _music?.Dispose();
        _stageMap?.Dispose();
        base.Dispose();
    }
}
