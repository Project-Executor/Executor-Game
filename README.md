# Executor(집행자) Game

Unity 6 PC 2D 퍼즐 게임 **Executor(집행자)** 의 클라이언트.
정해진 턴 안에 출구까지 가는 격자 퍼즐이고, 서버([Executor-Server](https://github.com/Project-Executor/Executor-Server))에 로그인하면 기록·별·코인·도감·친구가 저장된다.
스테이지·맵은 관리자 웹([Executor-Admin](https://github.com/Project-Executor/Executor-Admin))에서 만들고, 게임은 서버에서 받아 플레이한다.

| 문서 | 내용 |
| --- | --- |
| [docs/art-guide.md](docs/art-guide.md) | **그림 크기·경로** (도트, 일러스트, 도감 그림). 그림을 넣는 사람은 이것만 보면 된다 |
| [Executor-Server docs/map-data.md](https://github.com/Project-Executor/Executor-Server/blob/main/docs/map-data.md) | 맵 JSON 구조와 게임 규칙 (이동, 턴, 체크포인트, 별, 요소별 동작) |
| [Executor-Server docs/api-spec.md](https://github.com/Project-Executor/Executor-Server/blob/main/docs/api-spec.md) | 서버 API |

## 실행

1. Unity Hub에서 이 폴더를 연다 (에디터 버전은 `ProjectSettings/ProjectVersion.txt`).
2. `Assets/Scenes/Wireframe.unity`를 열고 ▶ 재생. 씬이 없으면 메뉴 **Executor → 와이어프레임 플레이 씬 만들기**.
3. 크게 보려면 Game 탭의 **Play Focused ▾ → Play Maximized** (재생을 멈춘 상태에서), 해상도는 **Full HD (1920x1080)**.

시작 화면에서 고를 수 있다.

| 방식 | 맵 | 기록 |
| --- | --- | --- |
| **로그인** | 서버의 공식 스테이지 (레이어 → Operation → 스테이지) | 서버에 저장 (별, 코인, 도감, 순위) |
| **게스트로 플레이** | 서버에 닿으면 공식 스테이지(`/api/guest/**`, 로그인 없음), 닿지 않으면 PC의 `Assets/Resources/Maps/` | 이 PC에만 저장. 누를 때마다 새 게스트(진행·닉네임 초기화, 전시용). 결과 제출·순위·코인·상점·메일·친구는 로그인 필요 |

서버 주소는 `Assets/Scripts/Network/ApiConfig.cs`의 `BaseUrl` (기본은 배포 서버, 로컬 서버는 `LocalBaseUrl`).

## 조작

| 키 | 동작 |
| --- | --- |
| W A S D / 방향키 | 이동 (상태가 바뀌는 이동만 1턴) |
| E | 인물과 대화, 감염된 컴퓨터 해킹 (주변 3x3 안의 가장 가까운 대상) |
| Q | 도착한 체크포인트로 워프 (위치·상자·방화벽은 되돌아가고, 열쇠·연 문·데이터 파편·도감은 유지) |
| M | 맵 전체 보기 ↔ 구간 보기 |
| R | 다시 하기 |
| Esc | 일시 정지 메뉴 (버그 제보 포함) |
| R (결과·순위 창) | 다시 도전 |

## 주요 화면

| 화면 | 내용 |
| --- | --- |
| 시작 | 로그인·회원가입, 게스트로 플레이 |
| 메인 (바탕화면) | 작업 표시줄(프로필 사진·코인), 델타(처음 만난 뒤 '마스터'라고 부르며 시간·진행에 맞춰 인사), 바탕화면 창들 |
| 스테이지 선택 | 왼쪽 레이어·Operation 트리, 가운데 회로 기판(노드 위치는 관리자 웹에서 정한 자리, 없으면 지그재그), 오른쪽 스테이지 정보·별 조건·맵 미리 보기 |
| 플레이 | 대화창, 나레이션, 감염된 컴퓨터 타자 해킹, 워프 창, 미니맵, 결과, 순위(시상대 1~3위·메달, 4위부터 목록) |
| profile.exe | 프로필(닉네임·장착 꾸미기), 사진 바꾸기(파일 고르기 → 정사각형 자르기 → 업로드) |
| README.txt (공지) | 검색, 분류 탭(이벤트·업데이트·안내), 안 읽음·NEW, 줄 번호 본문 |
| 그 밖 | 상점(구매 확인), 메일, 도감, 친구·메시지(messenger.exe), 설정, 신고 |

## 폴더

```
Assets/
├── Scripts/
│   ├── Game/        규칙 (Unity와 상관없는 순수 C#) — Board(이동·턴·체크포인트·별), DialogueSession, GuideTracker, TypingChallenge
│   ├── Data/        서버·맵 JSON 형식 (MapData, StageDtos, ItemDtos, SocialDtos ...)
│   ├── Network/     서버 호출 (ApiClient: 토큰 첨부·재발급, 기능별 *Api)
│   ├── Play/        화면
│   │   ├── WireframeGame.cs   게임 흐름 (메뉴 ↔ 스테이지 ↔ 결과, 입력 처리)
│   │   ├── MenuUI.cs          시작·스테이지 선택·시나리오·상점·메일·도감 등 메뉴 화면 (시작·로그인 MenuUI.Title.cs, 메인 MenuUI.Main.cs, 스테이지 선택 MenuUI.Stage.cs, 바탕화면 창·확인 창 MenuUI.Windows.cs, 프로필 MenuUI.Profile.cs, 공지 MenuUI.Notice.cs)
│   │   ├── GameUI.cs          플레이 화면 공용 (알림·팝업). HUD GameUI.Hud.cs, 결과 GameUI.Result.cs, 순위 GameUI.Ranking.cs, 대화 GameUI.Dialogue.cs, 해킹 GameUI.Hack.cs, 워프 GameUI.Warp.cs, 나레이션 GameUI.Story.cs
│   │   ├── PlayerSession.cs   로그인·게스트 상태, 계정(게스트 번호)별 진행 저장 위치
│   │   ├── DeltaGreeting.cs   메인 화면 델타 인사 문구
│   │   ├── AvatarStore.cs     프로필 사진 캐시 (avatarVersion이 바뀌면 다시 받음), FilePicker.cs 파일 고르기 창
│   │   ├── BoardView.cs       판 그리기와 카메라
│   │   ├── Minimap.cs         미니맵 그림
│   │   ├── SocialPanel.cs     친구·메시지 창 (messenger.exe)
│   │   ├── ReportPanel.cs     신고·버그 제보 팝업
│   │   ├── ArtCatalog.cs      그림 찾기 (없으면 임시 색 상자) — docs/art-guide.md
│   │   ├── ServerSync.cs      서버 스테이지 목록·맵 받기 (게스트는 /api/guest), 못 보낸 결과 다시 보내기
│   │   ├── Theme.cs           디자인 시안의 색·부품 (창, 테두리, 버튼). 시안 px × 1.5
│   │   └── UiKit.cs           UI 부품 (버튼, 입력칸, 팝업, 배치)
│   ├── Dev/         서버 API 확인용 스크립트 (테스트 계정을 만들고 지움)
│   └── Editor/      에디터 메뉴
├── Resources/       코드가 이름으로 불러오는 파일 (그림, 글꼴, 오프라인 맵)
│   ├── Maps/        chapters.json + {챕터}/stage-N.json (서버에 닿지 않을 때 게스트용)
│   ├── Pixel/       도트 (16px × 4배: 타일·오브젝트 64x64, 플레이어 걷기 프레임)
│   │   └── map/{세트}/  맵 그림 세트 (map_data.theme): hologram(튜토리얼), helloworld(1챕터), undercity, corporation(3챕터)
│   ├── Portraits/   대화 스탠딩 일러스트 ({인물ID}/{표정}.png)
│   ├── Illustrations/  시작 화면·메인 화면 그림
│   ├── Guidebook/   도감 그림
│   ├── UI/          메뉴 화면 그림 (desktop/ 아이콘, delta/ 델타 꾸미기, profile/ 기본 사진, ranking/ 메달)
│   └── Fonts/       Galmuri (도트 글꼴, SIL OFL)
└── Scenes/Wireframe.unity
Art/                 원본 그림 (게임에 안 들어감). 예: player/*.gif → Tools/gif_to_frames.py로 프레임 PNG
Tools/RulesCheck/    규칙 점검 (Unity 없이 실행)
Tools/SpriteCut/     시트 자르기 (install_sheet.py: 맵 시트 한 장 → 세트 폴더, sheets/에 원본 시트) — docs/art-guide.md
Tools/gif_to_frames.py  GIF를 프레임 PNG로 나누기
```

## 규칙 점검

`Game/`의 규칙을 Unity 없이 확인한다 (튜토리얼 맵 풀이, 이동·체크포인트·워프(열쇠 유지 포함)·별·컴퓨터·타자 해킹 등 142개).

```bash
bash Tools/RulesCheck/run.sh                    # 기본: ../Executor-Server/docs/maps/tutorial
```

Unity 에디터가 설치되어 있어야 한다 (에디터에 들어 있는 C# 컴파일러를 쓴다). 결과물은 `Tools/RulesCheck/bin/` (git에 올리지 않음).

## 작업 흐름

- 기능 하나를 마치면 `develop`에 바로 커밋한다. 배포(릴리스) 때만 `develop` → `main` PR을 연다.
- 커밋 제목은 `feat:`, `fix:`, `chore:`, `docs:` 접두어와 짧은 명사형.
- 새 스크립트를 만들면 Unity가 만든 `.meta` 파일도 함께 커밋한다.
