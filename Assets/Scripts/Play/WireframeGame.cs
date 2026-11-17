using System.Collections.Generic;
using System.Linq;
using Executor.Data;
using Executor.Game;
using Executor.Network;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Executor.Play
{
    /// <summary>
    /// 와이어프레임 앱 흐름: 시작 화면 → 메인 화면 → 스테이지 선택 → 게임.
    /// 빈 씬의 GameObject 하나에 붙이면 카메라·UI를 만든다 (메뉴 Executor > 와이어프레임 플레이 씬 만들기).
    /// <list type="bullet">
    ///   <item>로그인: 서버 계정 (자동 로그인 포함). 오프라인 테스트로도 들어갈 수 있다</item>
    ///   <item>챕터: Resources/Maps/chapters.json, 맵: Resources/Maps/{챕터ID}/stage-N.json (ChapterCatalog)</item>
    ///   <item>턴: 남은 턴(parMoves - 쓴 턴)만 보여 주고, -1이 되면 실패</item>
    ///   <item>진행 기록은 계정·챕터별로 PC에 저장 (StageProgress, 서버에 공식 스테이지 등록 후 서버 기록으로 교체)</item>
    /// </list>
    /// </summary>
    public class WireframeGame : MonoBehaviour
    {
        private const float EndDelay = 0.35f;
        private const string DefaultQuest = "목표: 출구(문 아이콘)에 도착하기";

        private enum Mode { Title, Main, StageSelect, Story, Playing, Dialogue, Result }

        private List<ChapterInfo> _chapters = new List<ChapterInfo>();
        private ChapterInfo _chapter;
        private MenuUI _menu;
        private GameUI _game;
        private BoardView _view;
        private Mode _mode;

        private StageInfo _stage;
        private Board _board;
        private GuideTracker _guides;
        private readonly List<string> _questGuides = new List<string>();
        private DialogueSession _dialogue;
        private bool _choicesShown;
        private float _endAt = -1;
        private int _blockInputFrame = -1;

        private List<StoryStep> _story;
        private int _storyIndex;
        private System.Action _afterStory;
        private bool _savingName;

        private void Start()
        {
            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
                cam.GetComponent<Camera>().orthographic = true;
            }
            _view = new GameObject("Board").AddComponent<BoardView>();
            _menu = new GameObject("MenuUI").AddComponent<MenuUI>();
            _menu.Build();
            _game = new GameObject("GameUI").AddComponent<GameUI>();
            _game.Build();
            WireMenu();
            WireGame();
            ApiClient.SessionExpired += () =>
            {
                if (_mode != Mode.Title) ShowTitle();
                // 계정이 정지돼서 끝났으면 기한과 사유를 보여 준다
                string reason = ApiClient.TakeSessionEndReason();
                if (reason != null) _menu.ShowMessage("계정 정지", reason);
            };
            LoadStages();
            ShowTitle();
            RestoreLogin();
        }

        private async void RestoreLogin()
        {
            if (await PlayerSession.TryRestoreAsync() && _mode == Mode.Title) ShowTitle();
        }

        private void LoadStages()
        {
            _chapters = ChapterCatalog.Load();
            _chapter = _chapters.FirstOrDefault(c => c.IsAvailable) ?? _chapters.FirstOrDefault();
        }

        private void Block() => _blockInputFrame = Time.frameCount;

        // ================= 메뉴 =================
        private void WireMenu()
        {
            // 로그인(또는 오프라인)한 뒤에만 화면 클릭으로 메인에 들어간다
            _menu.OnTitleStart = () =>
            {
                if (_menu.HasPopup || !PlayerSession.CanPlay) return;
                // 닉네임을 아직 정하지 않았으면(가입 후 첫 로그인) 바로 튜토리얼 1스테이지로
                if (PlayerSession.IsOnline && !PlayerSession.HasNickname) StartFirstTutorial(); else ShowMain();
            };
            _menu.OnTitleLogin = OpenLogin;
            _menu.OnTitleLogout = Logout;
            _menu.OnTitleOffline = () => { PlayerSession.StartOffline(); ShowTitle(); };
            _menu.OnSettings = () => _menu.ShowSettings(PlayerSession.DisplayName, PlayerSession.IsOnline, Logout);

            _menu.OnProfile = ShowProfile;
            _menu.OnMail = () => _menu.ShowMessage("우편", "우편함은 준비 중입니다.\n(서버 우편 API 연동 예정)");
            _menu.OnNotice = OpenNotices;
            _menu.OnShop = () => _menu.ShowMessage("상점", "상점은 준비 중입니다.\n(서버 상점 API 연동 예정)");
            _menu.OnStages = ShowStageSelect;
            _menu.OnMainBack = ShowTitle;

            _menu.OnStageBack = ShowMain;
            _menu.OnStageSelected = no =>
            {
                StageInfo s = _chapter?.Stages.FirstOrDefault(x => x.No == no);
                if (s != null && StageProgress.IsStageUnlocked(_chapter, s)) StartStage(s, true);
            };
            _menu.OnProgress = ShowProgress;
            _menu.OnScenario = ShowNavigator;
            _menu.OnChapterSelected = id =>
            {
                ChapterInfo c = _chapters.FirstOrDefault(x => x.Id == id);
                if (c == null || !StageProgress.IsChapterUnlocked(_chapters, c)) return;
                _chapter = c;
                ShowStageSelect();
            };
            _menu.OnChapterStory = id =>
            {
                ChapterInfo c = _chapters.FirstOrDefault(x => x.Id == id);
                if (c == null || c.Story.Count == 0) return;
                PlayStory(c.Story, ShowStageSelect);
            };
        }

        private void ShowTitle()
        {
            _mode = Mode.Title;
            Block();
            HideGame();
            string account = PlayerSession.IsOnline ? $"{PlayerSession.DisplayName} 님으로 로그인" : "오프라인 테스트 중";
            _menu.ShowTitle(PlayerSession.CanPlay, account, PlayerSession.IsOnline ? "로그아웃" : "오프라인 종료");
        }

        private void OpenLogin()
        {
            // 로그인하면 시작 화면으로 돌아가고 화면을 클릭해 메인으로 들어간다.
            // 닉네임이 없으면(가입 후 첫 로그인) 바로 튜토리얼 1스테이지로 가서 닉네임을 입력받는다
            _menu.ShowLogin(PlayerSession.LoginAsync, PlayerSession.SignupAsync, () =>
            {
                if (PlayerSession.IsOnline && !PlayerSession.HasNickname) StartFirstTutorial(); else ShowTitle();
            });
        }

        /// <summary>첫 챕터(튜토리얼)의 1스테이지를 도입 나레이션부터 시작한다</summary>
        private void StartFirstTutorial()
        {
            ChapterInfo first = _chapters.FirstOrDefault(c => c.IsAvailable);
            if (first == null) { ShowTitle(); return; }
            _chapter = first;
            StartStage(first.Stages[0], true);
        }

        private async void Logout()
        {
            await PlayerSession.LogoutAsync();
            ShowTitle();
        }

        private async void ShowMain()
        {
            _mode = Mode.Main;
            Block();
            HideGame();
            ShowMainProfile();
            if (PlayerSession.IsOnline)
            {
                try
                {
                    await PlayerSession.RefreshProfileAsync();
                    if (_mode == Mode.Main) ShowMainProfile();
                }
                catch (ApiException e)
                {
                    Debug.LogWarning("[WireframeGame] 프로필 갱신 실패: " + e);
                }
            }
        }

        /// <summary>공지 목록 (로그인 없이 조회). 서버에 닿지 못하면 안내만 한다</summary>
        private async void OpenNotices()
        {
            try
            {
                var page = await NoticeApi.GetNoticesAsync(size: 20);
                if (_mode != Mode.Main) return;
                _menu.ClosePopups();
                _menu.ShowNotices(page.Content, OpenNotice);
            }
            catch (ApiException e)
            {
                _menu.ShowMessage("공지사항", e.IsNetworkError ? "서버에 연결할 수 없어 공지를 불러오지 못했습니다." : e.Message);
            }
        }

        private async void OpenNotice(long noticeId)
        {
            try
            {
                var notice = await NoticeApi.GetNoticeAsync(noticeId);
                _menu.ClosePopups();
                _menu.ShowNoticeDetail(notice);
            }
            catch (ApiException e)
            {
                _menu.ClosePopups();
                _menu.ShowMessage("공지사항", e.Status == 404 ? "내려간 공지입니다." : e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message);
            }
        }

        private void ShowMainProfile()
        {
            string coin = PlayerSession.IsOnline ? $"코인 {PlayerSession.Profile.Coin}" : "오프라인 (서버 기록 없음)";
            _menu.ShowMain(PlayerSession.DisplayName, coin);
        }

        private void ShowProfile()
        {
            if (!PlayerSession.IsOnline)
            {
                _menu.ShowList("프로필", new[] { "오프라인 테스트 중입니다.", $"닉네임: {NameOrDash(PlayerSession.Nickname)}" });
                return;
            }
            MyProfileResponse p = PlayerSession.Profile;
            _menu.ShowList("프로필", new[]
            {
                $"닉네임: {NameOrDash(p.Nickname)}",
                $"코인: {p.Coin}",
                $"이메일: {p.Email}",
                $"자기소개: {NameOrDash(p.Comment)}",
            });
        }

        private static string NameOrDash(string s) => string.IsNullOrEmpty(s) ? "-" : s;

        private void ShowStageSelect()
        {
            _mode = Mode.StageSelect;
            Block();
            HideGame();
            if (_chapter == null) { _menu.ShowStageSelect("", "챕터 없음", new List<MenuUI.StageNode>()); return; }
            _menu.ShowStageSelect(_chapter.Id, $"{_chapter.Title} · {_chapter.Subtitle}", _chapter.Stages.Select(s => new MenuUI.StageNode
            {
                No = s.No,
                Title = s.Title.Replace("시뮬레이션 ", ""),
                Unlocked = StageProgress.IsStageUnlocked(_chapter, s),
                Cleared = StageProgress.IsCleared(_chapter.Id, s.No),
                Detail = StageProgress.IsCleared(_chapter.Id, s.No) ? $"최고 {StageProgress.BestMoves(_chapter.Id, s.No)}턴 사용" : null,
            }).ToList());
        }

        /// <summary>시나리오 버튼: 오른쪽 챕터 내비게이터</summary>
        private void ShowNavigator()
        {
            _menu.ShowNavigator(_chapters.Select(c =>
            {
                bool unlocked = StageProgress.IsChapterUnlocked(_chapters, c);
                string status = !c.IsAvailable ? "준비 중"
                              : !unlocked ? "잠김 · 앞 챕터를 모두 클리어하면 열립니다"
                              : $"클리어 {StageProgress.ClearedCount(c)} / {c.Stages.Count}";
                return new MenuUI.ChapterEntry
                {
                    Id = c.Id,
                    Title = c.Title,
                    Subtitle = c.Subtitle,
                    Status = status,
                    Selectable = c.IsAvailable && unlocked,
                    HasStory = unlocked && c.Story.Count > 0,
                    Current = c == _chapter,
                };
            }).ToList());
        }

        private void ShowProgress()
        {
            if (_chapter == null || !_chapter.IsAvailable) { _menu.ShowMessage("진행 상황", "준비 중인 챕터입니다."); return; }
            int cleared = StageProgress.ClearedCount(_chapter);
            var lines = new List<string> { $"{_chapter.Title} · 클리어 {cleared} / {_chapter.Stages.Count}", "" };
            foreach (StageInfo s in _chapter.Stages)
            {
                string state = StageProgress.IsCleared(_chapter.Id, s.No)
                    ? $"클리어 · 최소 {StageProgress.BestMoves(_chapter.Id, s.No)}턴 사용 (제한 {s.Par})"
                    : StageProgress.IsStageUnlocked(_chapter, s) ? "도전 가능" : "잠김";
                lines.Add($"{s.No}. {s.Title} — {state}");
            }
            _menu.ShowList("진행 상황", lines);
        }

        private void HideGame()
        {
            _board = null;
            _view.gameObject.SetActive(false);
            _game.HideAll();
        }

        // ================= 게임 =================
        private void WireGame()
        {
            _game.OnSettings = ShowPauseMenu;
            _game.OnCheckNickname = CheckNickname;
            _game.OnChat = () => _game.ShowMessage("채팅", "채팅은 준비 중입니다.\n(친구 메시지 API 연동 예정)");
            _game.OnCollect = () => _game.ShowMessage("수집", "이 스테이지에는 수집 요소가 없습니다.\n(숨겨진 요소는 본 스테이지에서 추가)");
        }

        private void ShowPauseMenu()
        {
            if (_board == null || _game.HasPopup) return;
            var buttons = new List<(string, System.Action)> { ("계속하기", null) };
            if (_board.CheckpointCount > 0) buttons.Add(("체크포인트로 이동 (Q)", () => ShowWarpMenu()));
            buttons.Add(("다시 하기 (R)", () => StartStage(_stage, false)));
            buttons.Add(("스테이지 선택", ShowStageSelect));
            buttons.Add(("메인으로", ShowMain));
            _game.ShowMenu("일시 정지", buttons);
        }

        private void StartStage(StageInfo stage, bool withIntro)
        {
            Block();
            _stage = stage;
            _board = new Board(stage.Map, stage.Par);
            _guides = new GuideTracker(_board, PlayerSession.Nickname);
            _questGuides.Clear();
            _dialogue = null;
            _endAt = -1;
            _menu.HideAll();
            _game.HideAll();
            _view.gameObject.SetActive(true);
            _view.Build(_board);
            _game.ShowHud($"{stage.No}. {stage.Title}", DefaultQuest, 0, 0);
            UpdateHud();

            if (withIntro && stage.Map.Story?.Intro?.Count > 0) PlayStory(stage.Map.Story.Intro, BeginPlay);
            else BeginPlay();
        }

        private void BeginPlay()
        {
            _mode = Mode.Playing;
            Block();
            _guides = new GuideTracker(_board, PlayerSession.Nickname);
            ShowGuides(_guides.OnStart());
            if (_board.CheckpointCount > 0)
            {
                _game.Toast($"체크포인트 {_board.CheckpointCount}개: 남은 턴 안에 도착하면 안정, 넘기면 불안정. Q로 도착한 체크포인트에 워프할 수 있습니다.");
            }
        }

        private void ShowGuides(IReadOnlyList<string> texts)
        {
            foreach (string text in texts)
            {
                _game.Toast(text);
                _questGuides.Add(text);
            }
            if (texts.Count == 0) return;
            // 퀘스트 패널: 목표 + 최근 안내 2개
            _game.SetQuest(DefaultQuest + "\n\n" + string.Join("\n\n", _questGuides.Skip(System.Math.Max(0, _questGuides.Count - 2))));
        }

        private void UpdateHud() => _game.SetTurns(_board.RemainingTurns, _board.Keys.Values.Sum());

        // ---------------- 나레이션 ----------------
        private void PlayStory(List<StoryStep> steps, System.Action after)
        {
            _story = steps;
            _storyIndex = -1;
            _afterStory = after;
            _mode = Mode.Story;
            Block();
            NextStoryStep();
        }

        private void NextStoryStep()
        {
            _storyIndex++;
            while (_storyIndex < _story.Count && StoryTypes.IsNicknameInput(_story[_storyIndex].Type)
                   && PlayerSession.HasNickname)
            {
                _storyIndex++;
            }
            if (_storyIndex >= _story.Count)
            {
                _game.HideNarration();
                _afterStory?.Invoke();
                return;
            }
            StoryStep step = _story[_storyIndex];
            _game.ShowNarration(TextFormat.Apply(step.Text, PlayerSession.Nickname),
                StoryTypes.IsNicknameInput(step.Type), PlayerSession.Nickname);
        }

        private bool AskingName => _mode == Mode.Story && _storyIndex < _story.Count
                                   && StoryTypes.IsNicknameInput(_story[_storyIndex].Type);

        private async void SubmitName()
        {
            string input = _game.NameInput;
            if (_savingName) return;
            if (input.Length < 2 || input.Length > 20) { _game.SetNicknameStatus("닉네임은 2~20자여야 합니다.", false); return; }
            _savingName = true;
            _game.SetNicknameStatus("저장 중...", true);
            try
            {
                await PlayerSession.SetNicknameAsync(input);
                NextStoryStep();
            }
            catch (ApiException e)
            {
                _game.SetNicknameStatus(e.Code == "DUPLICATE_NICKNAME" ? "이미 사용 중인 닉네임입니다." : "저장하지 못했습니다: " + e.Message, false);
            }
            finally
            {
                _savingName = false;
            }
        }

        /// <summary>닉네임 입력 옆 중복 확인 버튼</summary>
        private async void CheckNickname()
        {
            string input = _game.NameInput;
            if (input.Length < 2 || input.Length > 20) { _game.SetNicknameStatus("닉네임은 2~20자여야 합니다.", false); return; }
            if (!PlayerSession.IsOnline) { _game.SetNicknameStatus("오프라인 테스트에서는 확인 없이 저장됩니다.", true); return; }
            try
            {
                bool available = await PlayerSession.IsNicknameAvailableAsync(input);
                _game.SetNicknameStatus(available ? $"'{input}' 사용할 수 있습니다." : $"'{input}'은(는) 이미 사용 중입니다.", available);
            }
            catch (ApiException e)
            {
                _game.SetNicknameStatus("확인하지 못했습니다: " + e.Message, false);
            }
        }

        // ---------------- 입력 ----------------
        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null || Time.frameCount == _blockInputFrame) return;
            bool enter = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame;
            bool next = k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || enter
                        || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            switch (_mode)
            {
                case Mode.Title:
                    if (!_menu.HasPopup && (enter || k.spaceKey.wasPressedThisFrame)) _menu.OnTitleStart();
                    break;

                case Mode.Main:
                    if (!_menu.HasPopup && k.escapeKey.wasPressedThisFrame) ShowTitle();
                    break;

                case Mode.StageSelect:
                    if (_menu.HasPopup) break;
                    if (_menu.IsNavigatorOpen)
                    {
                        if (k.escapeKey.wasPressedThisFrame) _menu.CloseNavigator();
                        break;
                    }
                    if (k.escapeKey.wasPressedThisFrame) { ShowMain(); return; }
                    // 확대: + / - (숫자 패드 포함), 0 맞춤
                    if (k.equalsKey.wasPressedThisFrame || k.numpadPlusKey.wasPressedThisFrame) _menu.ZoomStageMap(1.25f);
                    if (k.minusKey.wasPressedThisFrame || k.numpadMinusKey.wasPressedThisFrame) _menu.ZoomStageMap(0.8f);
                    if (k.digit0Key.wasPressedThisFrame || k.numpad0Key.wasPressedThisFrame) _menu.ZoomStageMap(1f);
                    for (int i = 0; _chapter != null && i < _chapter.Stages.Count && i < 9; i++)
                    {
                        if (k[Key.Digit1 + i].wasPressedThisFrame || k[Key.Numpad1 + i].wasPressedThisFrame)
                        {
                            _menu.OnStageSelected(_chapter.Stages[i].No);
                            return;
                        }
                    }
                    break;

                case Mode.Story:
                    if (AskingName) { if (enter) SubmitName(); }
                    else if (next) NextStoryStep();
                    break;

                case Mode.Playing:
                    if (_game.HasPopup)
                    {
                        if (k.escapeKey.wasPressedThisFrame) _game.ClosePopups();
                        return;
                    }
                    if (_endAt >= 0)
                    {
                        if (Time.time >= _endAt) FinishStage();
                        return;
                    }
                    if (k.escapeKey.wasPressedThisFrame) { ShowPauseMenu(); return; }
                    if (k.rKey.wasPressedThisFrame) { StartStage(_stage, false); return; }
                    if (k.eKey.wasPressedThisFrame) { TryTalk(); return; }
                    if (k.qKey.wasPressedThisFrame) { ShowWarpMenu(); return; }
                    if (Pressed(k, out Direction dir)) Move(dir);
                    break;

                case Mode.Dialogue:
                    if (_dialogue.HasChoices && !_game.IsTyping)
                    {
                        if (!_choicesShown)
                        {
                            _choicesShown = true;
                            _game.ShowChoices(_dialogue.Choices.Select(c => c.Text).ToList(), Choose);
                        }
                        for (int i = 0; i < _dialogue.Choices.Count && i < 9; i++)
                        {
                            if (k[Key.Digit1 + i].wasPressedThisFrame || k[Key.Numpad1 + i].wasPressedThisFrame)
                            {
                                Choose(i);
                                return;
                            }
                        }
                    }
                    else if (next)
                    {
                        if (_game.IsTyping) _game.CompleteLine();
                        else { _dialogue.Advance(); ShowDialogueNode(); }
                    }
                    break;

                case Mode.Result:
                    if (k.rKey.wasPressedThisFrame) StartStage(_stage, false);
                    else if (k.escapeKey.wasPressedThisFrame) ShowStageSelect();
                    else if (enter && _board != null && _board.IsCleared) NextStage();
                    break;
            }
        }

        private static bool Pressed(Keyboard k, out Direction dir)
        {
            dir = default;
            if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) { dir = Direction.Up; return true; }
            if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) { dir = Direction.Left; return true; }
            if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) { dir = Direction.Down; return true; }
            if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) { dir = Direction.Right; return true; }
            return false;
        }

        // ---------------- 플레이 ----------------
        private void Move(Direction dir)
        {
            MoveResult result = _board.Move(dir);
            if (result.UsedTurn) _view.Sync(); else _view.Bump(dir);
            ShowGuides(_guides.AfterMove(result));
            if (result.Outcome == MoveOutcome.PickedUpKey) _game.Toast("열쇠를 얻었습니다.");
            if (result.Outcome == MoveOutcome.ReachedCheckpoint)
            {
                _game.Toast(_board.LastArrivalStable
                    ? $"체크포인트 {result.Target.Order} 도착 (안정). 다음 구간 {Turns(_board.SegmentLimit)}"
                    : $"체크포인트 {result.Target.Order} 도착 (불안정: 턴 초과). Q로 앞 체크포인트에 워프해 다시 시도할 수 있습니다.");
            }
            UpdateHud();
            if (_board.IsOver) _endAt = Time.time + EndDelay;
        }

        private static string Turns(int? limit) => limit == null ? "제한 없음" : $"{limit}턴";

        /// <summary>도착한 체크포인트 목록 (Q). 고르면 그때 상태로 워프한다</summary>
        private void ShowWarpMenu()
        {
            if (_board == null || _board.IsOver || _game.HasPopup) return;
            if (_board.CheckpointCount == 0)
            {
                _game.Toast("이 스테이지에는 체크포인트가 없습니다.");
                return;
            }
            var buttons = new List<(string, System.Action)>();
            foreach (int order in _board.WarpPoints)
            {
                string name = order == Board.StartOrder ? "시작 지점" : $"체크포인트 {order}";
                bool? stable = order == Board.StartOrder ? null : _board.StabilityOf(order);
                string tag = stable == null ? "" : stable.Value ? " · 안정" : " · 불안정";
                string here = order == _board.CurrentOrder ? " (지금 구간)" : "";
                int target = order;
                buttons.Add(($"{name}{tag}{here}", () => Warp(target)));
            }
            buttons.Add(("닫기", null));
            _game.ShowMenu($"워프 · 불안정 {_board.UnstableCount}개 (별 2: {_board.Data.Stars?.MaxUnstable ?? 0}개 이하)", buttons);
        }

        private void Warp(int order)
        {
            if (!_board.WarpTo(order)) return;
            _view.SnapToBoard();
            UpdateHud();
            _game.Toast(order == Board.StartOrder ? "시작 지점으로 돌아왔습니다." : $"체크포인트 {order}(으)로 돌아왔습니다. {Turns(_board.SegmentLimit)} 안에 다음 체크포인트로.");
        }

        private void TryTalk()
        {
            BoardObject npc = _board.TalkableNpc;
            if (npc == null) return;
            _dialogue = new DialogueSession(_board, npc, PlayerSession.Nickname);
            _mode = Mode.Dialogue;
            ShowDialogueNode();
        }

        private void Choose(int index)
        {
            if (_mode != Mode.Dialogue || _dialogue == null || _dialogue.IsFinished || _game.IsTyping) return;
            _dialogue.Choose(index);
            ShowDialogueNode();
        }

        private void ShowDialogueNode()
        {
            Block();
            if (_dialogue.IsFinished)
            {
                _game.HideDialogue();
                if (_dialogue.GivenKeys.Count > 0) _game.Toast("열쇠를 받았습니다.");
                _dialogue = null;
                _mode = Mode.Playing;
                UpdateHud();
                return;
            }
            _choicesShown = false;
            Sprite portrait = ArtCatalog.Portrait(_dialogue.Current.Portrait, _dialogue.Speaker, out string characterId);
            _game.ShowLine(_dialogue.Speaker, _dialogue.Text, portrait, characterId == "player", _dialogue.Speaker);
            UpdateHud();
        }

        // ---------------- 끝 ----------------
        private void FinishStage()
        {
            _endAt = -1;

            // TODO: 서버에 튜토리얼이 등록되면 StageApi.SubmitResultAsync(stageId, _board.ToResultRequest())
            // 기록 = 구간 최고 기록의 합 (워프로 다시 통과한 구간은 좋은 쪽만). 체크포인트가 없으면 쓴 턴과 같다
            int record = _board.RecordMoves;
            int best = StageProgress.BestMoves(_chapter.Id, _stage.No);
            bool newBest = StageProgress.RecordClear(_chapter.Id, _stage.No, record);

            void ShowResult()
            {
                _mode = Mode.Result;
                Block();
                bool hasNext = _chapter.Stages.Any(s => s.No > _stage.No);
                var buttons = new List<(string, System.Action)>
                {
                    ("다시 (R)", () => StartStage(_stage, false)),
                    ("스테이지 선택 (Esc)", ShowStageSelect),
                };
                if (hasNext) buttons.Add(("다음 (Enter)", NextStage));
                string detail = _board.CheckpointCount == 0
                    ? $"남은 턴 {_board.RemainingTurns} · {record}턴 사용"
                    : $"기록 {record}턴 (구간 최고의 합) · 누른 턴 {_board.Turns} · 워프 {_board.Rewinds}회";
                bool tutorial = _stage.Tutorial;
                string star2 = tutorial ? "" : _board.MeetsStableStar
                    ? $"\n★2 달성 (불안정 {_board.UnstableCount}개)"
                    : $"\n★2 미달성 (불안정 {_board.UnstableCount}개 > 허용 {_board.Data.Stars?.MaxUnstable ?? 0}개)";
                _game.ShowResult($"클리어!\n\n{_stage.Title}\n{detail}{star2}" +
                                 (newBest ? "\n최고 기록" : $"\n최고 {best}턴"), true, buttons);
            }

            if (_stage.Map.Story?.Outro?.Count > 0) PlayStory(_stage.Map.Story.Outro, ShowResult);
            else ShowResult();
        }

        private void NextStage()
        {
            StageInfo next = _chapter.Stages.FirstOrDefault(s => s.No > _stage.No);
            if (next != null && StageProgress.IsStageUnlocked(_chapter, next)) StartStage(next, true);
            else ShowStageSelect();
        }
    }
}
