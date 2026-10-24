# 게임 리소스 목록

기획서의 한 방짜리 탑다운 액션 게임에 사용할 파일이다. 이미지는 PNG, 효과음은 WAV, 배경음은 MP3로 정리했다.

| 파일 | 사용 목적 | 원본 | 출처 |
| --- | --- | --- | --- |
| `image/character/player_idle.png` | 플레이어 대기 | `hitman1_stand.png` | K1 |
| `image/character/player_attack.png` | 플레이어 공격 | `hitman1_silencer.png` | K1 |
| `image/character/enemy_idle.png` | 적 대기 | `manBrown_stand.png` | K1 |
| `image/character/enemy_attack.png` | 적 공격 | `manBrown_gun.png` | K1 |
| `image/world/floor.png` | 방 바닥 | `tile_09.png` | K1 |
| `image/world/wall.png` | 방 벽 | `tile_90.png` | K1 |
| `image/world/wall_edge.png` | 벽의 직선 테두리 | `tile_120.png` | K1 |
| `image/world/wall_corner.png` | 벽 모서리 | `tile_110.png` | K1 |
| `image/world/crate.png` | 엄폐물 상자 | `tile_130.png` | K1 |
| `image/world/table.png` | 방 안 장애물 | `tile_370.png` | K1 |
| `image/world/sofa.png` | 방 안 가구 | `tile_450.png` | K1 |
| `image/effect/projectile.png` | 탄환 표시 | `tile_360.png` | K1 |
| `image/effect/hit.png` | 피격 표시 | `tile_320.png` | K1 |
| `image/ui/weapon_icon.png` | 무기 표시 | `weapon_silencer.png` | K1 |
| `sound/shot.wav` | 발사 효과음 | `silencer.wav` | O1 |
| `sound/enemy_hit.wav` | 적 피격 효과음 | `impactPunch_heavy_000.ogg` | K2 |
| `sound/player_hit.wav` | 플레이어 사망 효과음 | `impactSoft_heavy_000.ogg` | K2 |
| `sound/wall_hit.wav` | 벽 피격 효과음 | `impactMetal_medium_000.ogg` | K2 |
| `sound/footstep.wav` | 이동 발소리 | `footstep_wood_000.ogg` | K2 |
| `sound/clear.wav` | 클리어 효과음 | `jingles_HIT00.ogg` | K3 |
| `sound/bgm.mp3` | 플레이 중 배경음 | `cyberpunk.mp3` | O2 |

## 출처와 사용 조건

| 표시 | 제작자 및 출처 | 사용 조건 |
| --- | --- | --- |
| K1 | Kenney, [Top-down Shooter](https://kenney.nl/assets/top-down-shooter) | CC0, 출처 표기 의무 없음 |
| K2 | Kenney, [Impact Sounds](https://kenney.nl/assets/impact-sounds) | CC0, 출처 표기 의무 없음 |
| K3 | Kenney, [Music Jingles](https://kenney.nl/assets/music-jingles) | CC0, 출처 표기 의무 없음 |
| O1 | bart, [Hollywood-style pistol silencer sound effect](https://opengameart.org/content/hollywood-style-pistol-silencer-sound-effect) | CC0, 출처 표기 의무 없음 |
| O2 | Tarush Singhal, [Cyberpunk Beauty](https://opengameart.org/content/cyberpunk-beauty) | CC0, 출처 표기 의무 없음 |

Kenney의 OGG 효과음은 게임에서 쓰기 편하도록 WAV(44.1 kHz, 16비트 PCM)로 변환했다. 그 외 파일은 원본 그대로 사용한다.
