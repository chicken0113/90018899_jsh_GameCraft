# 리소스 정리

## 현재 게임에서 사용

| 파일 | 용도 |
| --- | --- |
| `hot_heavy/area_0.png`~`area_3.png` | 1층과 2층 세 구역의 배경 |
| `hot_heavy/stage.json` | 벽 충돌, 문, 계단, 소품과 적 배치 |
| `hot_heavy/sprites.png` | 플레이어·적·문·총알·효과 스프라이트 |
| `sound/bgm.mp3` | 반복 재생하는 배경음 |
| `sound/shot.wav` | 발사음 |
| `sound/enemy_hit.wav`, `sound/player_hit.wav` | 피격음 |
| `sound/clear.wav` | 클리어 효과음 |

배경과 스프라이트는 GameCraft의 분홍·청록 리소스를 바탕으로 만들었다. 계단·수조·차량·개 등은 같은 색감으로 추가했다. 방 구조와 적 시작 위치는 Dennaton Games의 *Hotline Miami* 10장 ‘Hot & Heavy’ 및 [보존된 원본 방 데이터](https://github.com/Pi0h1/HotlineMiami.gmx/tree/d7dc51008f3cfe7e9dcf4def6b7b9e809a0a9362)를 참고했다. 사용한 방 데이터는 `rmHouse4Middle`, `rmHouse4Left`, `rmHouse4Right`, `rmHouse4Upstairs`다.

## 보관 중인 이전 리소스

`image/tiles/`, `image/player*.png`, `image/enemies_*.png`, `image/effects.png`, `image/props.png`, `image/room.png`, `image/stage01.png`, `preview.png`, `multiroom_preview.png`, `atlas.json`은 이전 스테이지와 시안에 사용한 자료다. 현재 실행 파일에는 포함되지 않지만 원본을 유지했다. 이전 캐릭터·적·소품 이미지는 이미지 생성 도구로 제작했다.

현재 게임에서 사용하지 않는 나머지 `sound/*.wav`도 추가 연출을 위해 보관한다. 배경음과 효과음은 게임 안에서 낮은 볼륨으로 재생된다.

## 소리 출처

- [Tarush Singhal, Cyberpunk Beauty](https://opengameart.org/content/cyberpunk-beauty) — `bgm.mp3`, CC0
- [bart, Hollywood-style pistol silencer sound effect](https://opengameart.org/content/hollywood-style-pistol-silencer-sound-effect) — `shot.wav`, CC0
- [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds) — 피격음과 일부 보관 효과음, CC0
- [Kenney Music Jingles](https://kenney.nl/assets/music-jingles) — `clear.wav`, CC0
- [Kenney RPG Audio](https://kenney.nl/assets/rpg-audio) — 보관 중인 문·재장전 효과음, CC0

Kenney OGG 파일은 원본 샘플레이트를 유지한 16비트 WAV로 변환했다.
