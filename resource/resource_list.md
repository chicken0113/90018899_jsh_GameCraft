# 리소스 정리

어두운 실내와 분홍·청록 네온을 기준으로 맞춘 리소스다. 여러 방과 복도를 연결해서 스테이지를 만들 수 있도록 바닥, 벽, 문을 따로 준비했다. 이미지는 PNG이고, 효과음은 WAV, 배경음은 MP3다.

캐릭터·적·소품 이미지는 이미지 생성 도구로 제작했다.

- `image/tiles/`: 바닥 8장, 직선 벽 6장, 모서리 4장, 문 6장. 바닥과 벽은 128×128, 문은 2칸 크기다. 문의 가로·세로 방향과 열린·닫힌·잠긴 상태를 준비했다.
- `image/stage01.png`: 연결된 세 방을 그린 현재 플레이 배경. 1536×1280.
- `image/room.png`: 처음 만든 방 이미지. 분위기 참고용이며 연결형 스테이지 배경으로 쓰지 않는다.
- `image/player.png`: 위에서 본 플레이어. 투명 여백 포함.
- `image/player_walk_1.png`, `image/player_walk_2.png`: 플레이어의 걷기 동작 두 자세.
- `image/enemies_light.png`: 빨강 권총 적, 청록 기관단총 적.
- `image/enemies_heavy.png`: 노랑 산탄총 적, 보라 소총 적.
- `image/bullet_player.png`, `image/bullet_enemy.png`: 플레이어와 적의 총알.
- `image/effects.png`: 총구 불꽃, 벽 충돌, 피격, 연기.
- `image/props.png`: 소파, 상자, 탁자, 식물.
- `preview.png`: 화면 분위기 예시이며 게임 배경에는 사용하지 않는다.
- `multiroom_preview.png`: 타일로 세 방과 연결 통로를 배치한 예시다.

현재 실행에는 방 배경, 플레이어와 적 이미지, 총알과 피격 효과, 배경음악, 발사·피격·클리어 효과음을 사용한다. 소품과 추가 타일은 이후 스테이지를 확장할 때 쓸 리소스다. `stage01.png`는 타일 배치 예시를 게임용으로 복사한 이미지이며, 현재 이동 범위는 이 배경의 방과 통로에 맞춰 잡았다.

`atlas.json`에는 캐릭터와 소품 스프라이트의 잘라 쓸 위치 `[x, y, 너비, 높이]`를 적었다. 타일 이미지는 각각 독립된 파일이다. 플레이어는 걷기 동작이 있고, 적 캐릭터는 아직 한 자세씩만 준비했다.

## 소리 출처

- [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds) — 발소리와 피격음, CC0
- [Kenney Music Jingles](https://kenney.nl/assets/music-jingles) — 클리어음, CC0
- [Kenney RPG Audio](https://kenney.nl/assets/rpg-audio) — 문과 재장전 클릭음, CC0
- [bart, Hollywood-style pistol silencer sound effect](https://opengameart.org/content/hollywood-style-pistol-silencer-sound-effect) — 발사음, CC0
- [Tarush Singhal, Cyberpunk Beauty](https://opengameart.org/content/cyberpunk-beauty) — 배경음악, CC0

Kenney OGG 파일은 원본 샘플레이트를 유지한 16비트 WAV로 변환했다.
