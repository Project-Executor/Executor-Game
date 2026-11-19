using System.Collections.Generic;
using System.Threading.Tasks;
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

        private enum Mode { Title, Main, StageSelect, Story, Playing, Dialogue, Hacking, Result }

        // 타자 해킹 (감염된 컴퓨터)
        private TypingChallenge _hacking;
        private BoardObject _hackTarget;
        private int _hackStartFrame;

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
        /// <summary>이번 판 결과를 서버에 보냈는지 (클리어 제출 또는 나갈 때의 실패 제출, 한 번만)</summary>
        private bool _runSubmitted;
        private bool _loadingStage;

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
            if (!await PlayerSession.TryRestoreAsync()) return;
            await ReloadChaptersAsync();
            if (_mode == Mode.Title) ShowTitle();
        }

        /// <summary>
        /// 로그인 상태에 맞춰 챕터를 다시 만든다. 로그인했으면 서버(작전·스테이지·내 진행도)에서, 아니면 Resources 맵에서.
        /// 서버에 닿지 못하면 Resources 맵을 쓴다 (이때 결과는 서버에 보내지 않는다). 보내지 못한 결과가 있으면 먼저 보낸다
        /// </summary>
        private async Task ReloadChaptersAsync()
        {
            string current = _chapter?.Id;
            if (PlayerSession.IsOnline)
            {
                try
                {
                    await PendingResults.FlushAsync();
                    var chapters = await ServerCatalog.LoadAsync();
                    _chapters = chapters;
                    _chapter = _chapters.FirstOrDefault(c => c.Id == current)
                               ?? _chapters.FirstOrDefault(c => c.IsAvailable) ?? _chapters.FirstOrDefault();
                    return;
                }
                catch (ApiException e)
                {
                    Debug.LogWarning("[WireframeGame] 서버 스테이지를 불러오지 못해 PC의 맵을 씁니다: " + e);
                    _menu.ShowMessage("스테이지", e.IsNetworkError
                        ? "서버에 연결할 수 없어 PC에 있는 맵으로 플레이합니다.\n(이때 기록은 서버에 저장되지 않습니다)"
                        : "서버 스테이지를 불러오지 못했습니다.\n" + e.Message);
                }
            }
            StageProgress.ClearRemote();
            _chapters = ChapterCatalog.Load();
            _chapter = _chapters.FirstOrDefault(c => c.Id == current)
                       ?? _chapters.FirstOrDefault(c => c.IsAvailable) ?? _chapters.FirstOrDefault();
        }

        private async void ReloadChapters() => await ReloadChaptersAsync();

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
            _menu.OnTitleOffline = () => { PlayerSession.StartOffline(); ReloadChapters(); ShowTitle(); };
            _menu.OnSettings = () => _menu.ShowSettings(PlayerSession.DisplayName, PlayerSession.IsOnline, Logout);

            _menu.OnProfile = ShowProfile;
            _menu.OnMail = OpenMailbox;
            _menu.OnNotice = OpenNotices;
            _menu.OnGuidebook = () => OpenGuidebook(null, 0);
            _menu.OnShop = () => OpenShop(_shopFilter);
            // 메인의 스테이지 버튼: 가장 멀리 진행한 Operation의 스테이지 화면으로
            _menu.OnStages = () =>
            {
                _chapter = FrontierChapter() ?? _chapter;
                ShowStageSelect();
            };
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
            _menu.ShowLogin(PlayerSession.LoginAsync, PlayerSession.SignupAsync, async () =>
            {
                await ReloadChaptersAsync();
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
            await ReloadChaptersAsync();
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
                    RefreshMailCount();
                }
                catch (ApiException e)
                {
                    Debug.LogWarning("[WireframeGame] 프로필 갱신 실패: " + e);
                }
            }
        }

        // ---------------- 우편함 ----------------
        private List<MailResponse> _mails = new List<MailResponse>();

        /// <summary>메인 화면 우편 버튼의 받지 않은 우편 수를 갱신한다</summary>
        private async void RefreshMailCount()
        {
            if (!PlayerSession.IsOnline) { _menu.SetMailCount(0); return; }
            try
            {
                _mails = (await MailApi.GetMailsAsync()).Content;
                _menu.SetMailCount(_mails.Count(m => !m.Claimed));
            }
            catch (ApiException e)
            {
                Debug.LogWarning("[WireframeGame] 우편 수를 불러오지 못했습니다: " + e.Message);
            }
        }

        private async void OpenMailbox()
        {
            if (!PlayerSession.IsOnline)
            {
                _menu.ShowMessage("우편", "로그인해야 우편함을 쓸 수 있습니다.");
                return;
            }
            try
            {
                _mails = (await MailApi.GetMailsAsync()).Content;
                if (_mode != Mode.Main) return;
                _menu.SetMailCount(_mails.Count(m => !m.Claimed));
                _menu.ClosePopups();
                _menu.ShowMailbox(_mails, OpenMail, ClaimAllMail);
            }
            catch (ApiException e)
            {
                _menu.ShowMessage("우편", e.IsNetworkError ? "서버에 연결할 수 없어 우편을 불러오지 못했습니다." : e.Message);
            }
        }

        private void OpenMail(MailResponse mail)
        {
            _menu.ClosePopups();
            _menu.ShowMail(mail, () => ClaimMail(mail));
        }

        private async void ClaimMail(MailResponse mail)
        {
            try
            {
                MailClaimResponse res = await MailApi.ClaimAsync(mail.MailId);
                ApplyCoin(res.TotalCoin);
                var parts = new List<string>();
                if (res.RewardCoin > 0) parts.Add($"코인 {res.RewardCoin}");
                if (res.RewardItem != null) parts.Add($"{mail.RewardItem?.Name ?? "아이템"} ×{res.RewardItem.Quantity}");
                if (res.ConvertedCoin > 0) parts.Add($"이미 가진 아이템이라 코인 {res.ConvertedCoin}으로 바꿈");
                _menu.ClosePopups();
                _menu.ShowMessage("우편 받기", (parts.Count > 0 ? "받았습니다.\n" + string.Join("\n", parts) : "받았습니다.")
                                             + $"\n\n보유 코인 {res.TotalCoin}");
            }
            catch (ApiException e)
            {
                _menu.ClosePopups();
                _menu.ShowMessage("우편 받기", e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message);
            }
            RefreshMailCount();
        }

        private async void ClaimAllMail()
        {
            try
            {
                MailClaimAllResponse res = await MailApi.ClaimAllAsync();
                ApplyCoin(res.TotalCoin);
                var lines = new List<string> { $"{res.ClaimedCount}통을 받았습니다." };
                if (res.RewardCoin > 0) lines.Add($"코인 {res.RewardCoin}");
                foreach (ClaimedItem item in res.RewardItems)
                {
                    string name = _mails.FirstOrDefault(m => m.RewardItem?.ItemId == item.ItemId)?.RewardItem?.Name ?? "아이템";
                    lines.Add($"{name} ×{item.Quantity}");
                }
                if (res.ConvertedCoin > 0) lines.Add($"이미 가진 아이템은 코인 {res.ConvertedCoin}으로 바꿈");
                lines.Add("");
                lines.Add($"보유 코인 {res.TotalCoin}");
                _menu.ClosePopups();
                _menu.ShowMessage("모두 받기", string.Join("\n", lines));
            }
            catch (ApiException e)
            {
                _menu.ClosePopups();
                _menu.ShowMessage("모두 받기", e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message);
            }
            RefreshMailCount();
        }

        // ---------------- 상점 ----------------
        /// <summary>상점 종류 탭 (null = 전체)</summary>
        private string _shopFilter;

        private async void OpenShop(string filter)
        {
            if (!PlayerSession.IsOnline)
            {
                _menu.ShowMessage("상점", "로그인해야 상점을 쓸 수 있습니다.");
                return;
            }
            _shopFilter = filter;
            try
            {
                List<ShopItem> items = await ItemApi.GetShopItemsAsync(filter);
                if (_mode != Mode.Main) return;
                _menu.ClosePopups();
                _menu.ShowShop(items, PlayerSession.Profile.Coin, filter, OpenShop, ConfirmPurchase);
            }
            catch (ApiException e)
            {
                _menu.ShowMessage("상점", e.IsNetworkError ? "서버에 연결할 수 없어 상점을 불러오지 못했습니다." : e.Message);
            }
        }

        private void ConfirmPurchase(ShopItem item)
        {
            int price = item.Price;
            int coin = PlayerSession.Profile?.Coin ?? 0;
            string text = $"{item.Name}을(를) {price} 코인에 살까요?\n\n보유 코인 {coin} → {coin - price}";
            if (price > coin)
            {
                _menu.ShowMessage("구매", $"코인이 모자랍니다.\n\n필요 {price} · 보유 {coin}");
                return;
            }
            _menu.ShowConfirm("구매", text, () => Purchase(item));
        }

        private async void Purchase(ShopItem item)
        {
            try
            {
                PurchaseResponse res = await ItemApi.PurchaseAsync(item.ItemId);
                ApplyCoin(res.RemainingCoin);
                _menu.ClosePopups();
                OpenShop(_shopFilter);
                _menu.ShowMessage("구매 완료", $"{item.Name}을(를) 샀습니다.\n프로필에서 장착할 수 있습니다.\n\n남은 코인 {res.RemainingCoin}");
            }
            catch (ApiException e)
            {
                _menu.ClosePopups();
                _menu.ShowMessage("구매", e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message);
            }
        }

        /// <summary>보유 코인을 바꾸고 메인 화면에 반영한다</summary>
        private void ApplyCoin(int totalCoin)
        {
            if (PlayerSession.Profile != null) PlayerSession.Profile.Coin = totalCoin;
            if (_mode == Mode.Main) ShowMainProfile();
        }

        /// <summary>공지 목록 (로그인 없이 조회). 서버에 닿지 못하면 안내만 한다</summary>
        // ---------------- 도감 ----------------
        private async void OpenGuidebook(string category, int page)
        {
            if (!PlayerSession.IsOnline)
            {
                _menu.ShowMessage("도감", "도감은 로그인하면 볼 수 있습니다.\n(해금 기록이 서버에 저장됩니다)");
                return;
            }
            try
            {
                GuidebookListResponse list = await GuidebookApi.GetListAsync(category);
                if (_mode != Mode.Main) return;
                _menu.ClosePopups();
                _menu.ShowGuidebook(list, category, page, c => OpenGuidebook(c, 0), p => OpenGuidebook(category, p), OpenGuidebookItem);
            }
            catch (ApiException e)
            {
                _menu.ShowMessage("도감", e.IsNetworkError ? "서버에 연결할 수 없어 도감을 불러오지 못했습니다." : e.Message);
            }
        }

        private async void OpenGuidebookItem(long guidebookId)
        {
            try
            {
                GuidebookDetail d = await GuidebookApi.GetDetailAsync(guidebookId);
                if (_mode != Mode.Main) return;
                _menu.ShowGuidebookDetail(d, StageLabel(d.StageId), ArtCatalog.Guidebook(d.Image));
            }
            catch (ApiException e)
            {
                _menu.ShowMessage("도감", e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message);
            }
        }

        /// <summary>서버 스테이지 ID -> "Operation 이름 3. 스테이지 제목" (모르면 null)</summary>
        private string StageLabel(long? stageId)
        {
            if (stageId == null) return null;
            foreach (ChapterInfo c in _chapters)
            {
                StageInfo s = c.Stages.FirstOrDefault(x => x.StageId == stageId);
                if (s != null) return $"{c.Title} {s.No}. {s.Title}";
            }
            return null;
        }

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

        /// <summary>프로필: 정보·기록, 닉네임·자기소개 수정, 스킨·테마 장착</summary>
        private async void ShowProfile()
        {
            if (!PlayerSession.IsOnline)
            {
                _menu.ShowList("프로필", new[] { "오프라인 테스트 중입니다.", $"닉네임: {NameOrDash(PlayerSession.Nickname)}" });
                return;
            }
            try
            {
                MyProfileResponse p = PlayerSession.Profile;
                var itemsTask = ItemApi.GetMyItemsAsync();
                var statsTask = ProfileApi.GetProfileAsync(p.UserId);
                List<MyItem> items = await itemsTask;
                ProfileDetailResponse stats = null;
                try { stats = await statsTask; } catch (ApiException) { /* 기록 줄만 빠진다 */ }
                if (_mode != Mode.Main) return;
                _menu.ClosePopups();
                _menu.ShowProfileEditor(PlayerSession.Profile, stats, items, new MenuUI.ProfileActions
                {
                    CheckNickname = CheckNicknameForProfile,
                    SaveNickname = SaveNicknameForProfile,
                    SaveComment = SaveCommentForProfile,
                    Equip = Equip,
                });
            }
            catch (ApiException e)
            {
                _menu.ShowMessage("프로필", e.IsNetworkError ? "서버에 연결할 수 없어 프로필을 불러오지 못했습니다." : e.Message);
            }
        }

        private static string NicknameRule(string name) =>
            name.Length < 2 || name.Length > 20 ? "닉네임은 2~20자여야 합니다." : null;

        private async Task<string> CheckNicknameForProfile(string name)
        {
            if (NicknameRule(name) is string rule) return rule;
            if (name == PlayerSession.Profile.Nickname) return "✓ 지금 쓰고 있는 닉네임입니다.";
            try { return await PlayerSession.IsNicknameAvailableAsync(name) ? "✓ 쓸 수 있는 닉네임입니다." : "이미 쓰는 닉네임입니다."; }
            catch (ApiException e) { return e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message; }
        }

        private async Task<string> SaveNicknameForProfile(string name)
        {
            if (NicknameRule(name) is string rule) return rule;
            if (name == PlayerSession.Profile.Nickname) return "✓ 바뀐 것이 없습니다.";
            try
            {
                await PlayerSession.SetNicknameAsync(name);
                ShowMainProfile();
                return $"✓ 닉네임을 '{name}'(으)로 바꿨습니다.";
            }
            catch (ApiException e) { return e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message; }
        }

        private async Task<string> SaveCommentForProfile(string comment)
        {
            try
            {
                PlayerSession.SetProfile(await ProfileApi.UpdateCommentAsync(comment.Trim()));
                return string.IsNullOrWhiteSpace(comment) ? "✓ 자기소개를 지웠습니다." : "✓ 자기소개를 저장했습니다.";
            }
            catch (ApiException e) { return e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message; }
        }

        private async void Equip(long? skinId, long? themeId)
        {
            try
            {
                EquipmentResponse res = await ProfileApi.UpdateEquipmentAsync(skinId, themeId);
                PlayerSession.Profile.EquippedSkinId = res.EquippedSkinId;
                PlayerSession.Profile.EquippedThemeId = res.EquippedThemeId;
                ShowProfile();
            }
            catch (ApiException e)
            {
                _menu.ShowMessage("장착", e.IsNetworkError ? "서버에 연결할 수 없습니다." : e.Message);
            }
        }

        private static string NameOrDash(string s) => string.IsNullOrEmpty(s) ? "-" : s;

        private void ShowStageSelect()
        {
            _mode = Mode.StageSelect;
            Block();
            HideGame();
            if (_chapter == null) { _menu.ShowStageSelect("", "Operation 없음", new List<MenuUI.StageNode>()); return; }
            string subtitle = string.IsNullOrEmpty(_chapter.Subtitle) ? "" : $" · {_chapter.Subtitle}";
            _menu.ShowStageSelect(_chapter.Id, $"{_chapter.LayerLabel} › {_chapter.Title}{subtitle}", _chapter.Stages.Select(s => new MenuUI.StageNode
            {
                No = s.No,
                Title = s.Title.Replace("시뮬레이션 ", ""),
                Unlocked = StageProgress.IsStageUnlocked(_chapter, s),
                Cleared = StageProgress.IsCleared(_chapter.Id, s.No),
                Detail = StageProgress.IsCleared(_chapter.Id, s.No) ? StageDetail(_chapter, s) : null,
            }).ToList());
        }

        /// <summary>스테이지 선택의 기록 줄: "최고 22턴 · 별 2/3" (서버 기록이면 별까지)</summary>
        private static string StageDetail(ChapterInfo chapter, StageInfo stage)
        {
            string text = $"최고 {StageProgress.BestMoves(chapter.Id, stage.No)}턴";
            var stars = StageProgress.RemoteOf(chapter.Id, stage.No)?.Stars;
            return stars == null ? text : $"{text} · 별 {stars.Count}/{stars.Max}";
        }

        /// <summary>시나리오 버튼: 오른쪽 챕터 내비게이터</summary>
        private void ShowNavigator()
        {
            ChapterInfo frontier = FrontierChapter();
            _menu.ShowNavigator(_chapters.Select(c =>
            {
                bool unlocked = StageProgress.IsChapterUnlocked(_chapters, c);
                string status = !c.IsAvailable ? "준비 중"
                              : !unlocked ? "잠김 · 앞 Operation을 모두 클리어하면 열립니다"
                              : $"클리어 {StageProgress.ClearedCount(c)} / {c.Stages.Count}";
                return new MenuUI.ChapterEntry
                {
                    Id = c.Id,
                    Title = c.Title,
                    Subtitle = c.Subtitle,
                    LayerId = c.LayerId,
                    LayerLabel = c.LayerLabel,
                    LayerDescription = c.LayerDescription,
                    Status = status,
                    Selectable = c.IsAvailable && unlocked,
                    HasStory = unlocked && c.Story.Count > 0,
                    Current = c == _chapter,
                    Frontier = c == frontier,
                };
            }).ToList());
        }

        /// <summary>가장 멀리 진행한 Operation = 열린 Operation 중 (진행 순서로) 마지막. 없으면 null</summary>
        private ChapterInfo FrontierChapter() =>
            _chapters?.LastOrDefault(c => c.IsAvailable && StageProgress.IsChapterUnlocked(_chapters, c));

        private void ShowProgress()
        {
            if (_chapter == null || !_chapter.IsAvailable) { _menu.ShowMessage("진행 상황", "준비 중인 Operation입니다."); return; }
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
            SubmitAbandon();
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
            _game.OnCollect = () =>
            {
                if (_board == null || _game.HasPopup) return;
                int total = _board.HiddenTotal, found = _board.HiddenCollected;
                _game.ShowMessage("수집", total == 0
                    ? "이 스테이지에는 숨겨진 요소가 없습니다."
                    : $"숨겨진 요소 {found}/{total}\n\n" + (found == total
                        ? "모두 찾았습니다. 클리어하면 별 3을 받습니다."
                        : "가까이(3x3) 가면 드러납니다. 모두 모으면 별 3입니다.\n워프해도 모은 것은 유지됩니다."));
            };
            _game.OnMinimap = () =>
            {
                if (_board == null || _game.HasPopup) return;
                _game.ShowMapPopup($"{_stage.No}. {_stage.Title} · 전체 맵", Minimap.Render(_board), Minimap.Legend);
            };
            _game.OnQuest = () =>
            {
                if (_board == null || _game.HasPopup) return;
                _game.ShowDetail("퀘스트", QuestDetail());
            };
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
            SubmitAbandon();
            if (stage.Map == null)
            {
                LoadAndStart(stage, withIntro);
                return;
            }
            _stage = stage;
            _board = new Board(stage.Map, stage.Par);
            _runSubmitted = false;
            _guides = new GuideTracker(_board, PlayerSession.Nickname);
            _questGuides.Clear();
            _dialogue = null;
            _endAt = -1;
            _menu.HideAll();
            _game.HideAll();
            _view.gameObject.SetActive(true);
            _view.Build(_board);
            WordBank.Prefetch(_board.Objects.Where(o => o.Type == MapObjectTypes.Computer && o.WordListId != null)
                .Select(o => o.WordListId.Value).Distinct());
            _game.ShowHud($"{stage.No}. {stage.Title}", DefaultQuest, 0, 0);
            UpdateHud();

            if (withIntro && stage.Map.Story?.Intro?.Count > 0) PlayStory(stage.Map.Story.Intro, BeginPlay);
            else BeginPlay();
        }

        /// <summary>서버 스테이지의 맵을 받아서 시작한다 (잠겨 있던 스테이지는 맵을 미리 받지 못한다)</summary>
        private async void LoadAndStart(StageInfo stage, bool withIntro)
        {
            if (_loadingStage) return;
            _loadingStage = true;
            try
            {
                await ServerCatalog.LoadMapAsync(stage);
                StartStage(stage, withIntro);
            }
            catch (ApiException e)
            {
                _menu.ShowMessage("스테이지", e.IsNetworkError ? "서버에 연결할 수 없어 스테이지를 불러오지 못했습니다." : e.Message);
            }
            finally
            {
                _loadingStage = false;
            }
        }

        /// <summary>
        /// 클리어하지 않고 나가면(다시 하기, 스테이지 선택, 메인 등) 실패로 보낸다: 도전 횟수와 만난 이상 개체(도감)가 반영된다.
        /// 아무것도 하지 않았으면 보내지 않는다. 서버 스테이지이고 로그인했을 때만
        /// </summary>
        private void SubmitAbandon()
        {
            if (_board == null || _runSubmitted || _board.IsCleared || _stage?.StageId == null || !PlayerSession.IsOnline) return;
            if (_board.Turns == 0 && _board.EncounteredGuidebookIds.Count == 0) return;
            _runSubmitted = true;
            SubmitInBackground(_stage.StageId.Value, _board.ToResultRequest());
        }

        private static async void SubmitInBackground(long stageId, StageResultRequest request)
        {
            try
            {
                await StageApi.SubmitResultAsync(stageId, request);
            }
            catch (ApiException e)
            {
                if (e.IsNetworkError) PendingResults.Add(stageId, request);
                else Debug.LogWarning($"[WireframeGame] 결과 제출 실패 stage {stageId}: {e.Message}");
            }
        }

        private void BeginPlay()
        {
            _mode = Mode.Playing;
            Block();
            _guides = new GuideTracker(_board, PlayerSession.Nickname);
            ShowGuides(_guides.OnStart());
            ToastEncounters();
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
            _game.SetQuest(QuestDetail());
        }

        private Texture2D _minimapTexture;

        private void UpdateHud()
        {
            _game.SetTurns(_board.RemainingTurns, _board.Keys.Values.Sum());
            _game.SetCollect(_board.HiddenCollected, _board.HiddenTotal);
            _minimapTexture = Minimap.Render(_board, _minimapTexture);
            _game.SetMinimap(_minimapTexture);
            _game.SetQuestStars(QuestStars());
        }

        /// <summary>퀘스트 칸의 별 조건 3줄 (튜토리얼은 ★1만)</summary>
        private List<(string, GameUI.QuestState)> QuestStars()
        {
            var rows = new List<(string, GameUI.QuestState)>
            {
                ("★1 출구에 도착", _board.IsCleared ? GameUI.QuestState.Done : GameUI.QuestState.Pending)
            };
            if (_stage.Tutorial) return rows;
            int max = _board.Data.Stars?.MaxUnstable ?? 0;
            int unstable = _board.UnstableCount + (_board.IsOverLimit ? 1 : 0); // 이대로 도착하면 하나 더
            string limit = max == 0 ? "모든 구간을 턴 안에" : $"불안정 {max}개 이하";
            rows.Add(($"★2 {limit} (불안정 {_board.UnstableCount})",
                _board.IsCleared && _board.MeetsStableStar ? GameUI.QuestState.Done
                : unstable > max ? GameUI.QuestState.Warn : GameUI.QuestState.Pending));
            rows.Add((_board.HiddenTotal == 0 ? "★3 숨겨진 요소 없음" : $"★3 숨겨진 요소 모두 찾기 ({_board.HiddenCollected}/{_board.HiddenTotal})",
                _board.IsCleared && _board.AllHiddenFound ? GameUI.QuestState.Done : GameUI.QuestState.Pending));
            return rows;
        }

        /// <summary>퀘스트 자세히: 목표, 별 조건, 체크포인트 상태, 지금까지 나온 안내</summary>
        private string QuestDetail()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(DefaultQuest).AppendLine();
            sb.AppendLine("별 조건");
            sb.AppendLine("  ★1 출구에 도착하면 클리어");
            if (_stage.Tutorial)
            {
                sb.AppendLine("  튜토리얼은 ★1만 있습니다.");
            }
            else
            {
                int max = _board.Data.Stars?.MaxUnstable ?? 0;
                sb.AppendLine($"  ★2 불안정 체크포인트(출구 포함)가 {max}개 이하. 남은 턴 안에 도착하면 안정, 넘기면 불안정");
                sb.AppendLine(_board.HiddenTotal == 0
                    ? "  ★3 이 스테이지에는 숨겨진 요소가 없습니다."
                    : $"  ★3 숨겨진 요소 {_board.HiddenTotal}개를 모두 찾기 (지금 {_board.HiddenCollected}개). 가까이 가면 드러나고, 워프해도 유지");
                if (_board.EncounteredGuidebookIds.Count > 0)
                {
                    sb.AppendLine($"  만난 이상 개체 {_board.EncounteredGuidebookIds.Count}종 (클리어하지 않아도 도감에 기록)");
                }
            }
            if (_board.CheckpointCount > 0)
            {
                sb.AppendLine().AppendLine($"체크포인트 (Q로 도착한 곳에 워프, 지금 구간 {(_board.CurrentOrder == Board.StartOrder ? "시작" : _board.CurrentOrder.ToString())})");
                for (int order = 1; order <= _board.CheckpointCount; order++)
                {
                    BoardObject cp = _board.CheckpointOf(order);
                    if (cp == null) continue;
                    bool? s = _board.StabilityOf(order);
                    string state = s == null ? "아직" : s.Value ? "안정" : "불안정";
                    sb.AppendLine($"  {order}. {state} · 다음 구간 {Turns(cp.SegmentTurns ?? _board.TurnLimit)}");
                }
                bool? exit = _board.StabilityOf(Board.ExitOrder);
                sb.AppendLine($"  출구. {(exit == null ? "아직" : exit.Value ? "안정" : "불안정")}");
            }
            if (_questGuides.Count > 0)
            {
                sb.AppendLine().AppendLine("안내");
                foreach (string g in _questGuides) sb.AppendLine("  · " + g);
            }
            return sb.ToString();
        }

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
                _game.HideDialogue();
                _afterStory?.Invoke();
                return;
            }
            StoryStep step = _story[_storyIndex];
            if (step.Type == StoryTypes.Line)
            {
                _game.HideNarration();
                ShowSpeech(step.Speaker, step.Portrait, TextFormat.Apply(step.Text, PlayerSession.Nickname));
                return;
            }
            _game.HideDialogue();
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
                    else if (next)
                    {
                        if (_game.IsTyping) _game.CompleteLine();
                        else NextStoryStep();
                    }
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
                    if (k.eKey.wasPressedThisFrame) { TryInteract(); return; }
                    if (k.qKey.wasPressedThisFrame) { ShowWarpMenu(); return; }
                    if (k.mKey.wasPressedThisFrame)
                    {
                        _view.ToggleOverview();
                        _game.Toast(_view.Overview ? "맵 전체 보기 (M으로 돌아가기)" : "구간 보기");
                        return;
                    }
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

                case Mode.Hacking:
                    UpdateHacking(k);
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
            if (result.Outcome == MoveOutcome.FoundFakeWall) _game.Toast("가짜 벽이었습니다. 지나갈 수 있습니다.");
            if (result.Outcome == MoveOutcome.Collected)
            {
                string name = string.IsNullOrEmpty(result.Target.Name) ? "숨겨진 요소" : result.Target.Name;
                _game.Toast(_board.AllHiddenFound
                    ? $"{name} 발견! 숨겨진 요소를 모두 찾았습니다. ({_board.HiddenCollected}/{_board.HiddenTotal})"
                    : $"{name} 발견! ({_board.HiddenCollected}/{_board.HiddenTotal})");
            }
            ToastEncounters();
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

        /// <summary>방금 처음 만난 이상 개체 알림 (도감 기록)</summary>
        private void ToastEncounters()
        {
            foreach (BoardObject o in _board.LastEncounters)
            {
                _game.Toast($"이상 개체 발견: {(string.IsNullOrEmpty(o.Name) ? "알 수 없는 개체" : o.Name)} (도감에 기록)");
            }
        }

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
            ToastEncounters();
            _game.Toast(order == Board.StartOrder ? "시작 지점으로 돌아왔습니다." : $"체크포인트 {order}(으)로 돌아왔습니다. {Turns(_board.SegmentLimit)} 안에 다음 체크포인트로.");
        }

        /// <summary>E: 3x3 안의 가장 가까운 인물과 대화하거나 컴퓨터를 해킹한다</summary>
        private void TryInteract()
        {
            BoardObject target = _board.Interactable;
            if (target == null) return;
            if (target.Type == MapObjectTypes.Computer) StartHack(target);
            else TryTalk(target);
        }

        // ---------------- 타자 해킹 ----------------
        private void StartHack(BoardObject computer)
        {
            if (computer.IsRestored) { _game.Toast("이미 복구된 컴퓨터입니다."); return; }
            if (!_board.BeginHack(computer)) return;
            _hackTarget = computer;
            _hacking = new TypingChallenge(WordBank.Get(computer.WordListId), computer.WordCount, computer.TimeLimit);
            _hackStartFrame = Time.frameCount;
            _mode = Mode.Hacking;
            Block();
            _game.ShowHack(string.IsNullOrEmpty(computer.Name) ? "감염된 컴퓨터" : computer.Name);
            _game.UpdateHack(HackWordsText(), _hacking.TimeLeft, _hacking.TimeLimit, "", true);
            UpdateHud();
        }

        private void UpdateHacking(Keyboard k)
        {
            // 해킹을 연 E 키가 입력칸에 들어가지 않게 처음 두 프레임은 비운다
            if (Time.frameCount <= _hackStartFrame + 1 && _game.HackInput.Length > 0) _game.ClearHackInput();
            _hacking.Tick(Time.deltaTime);
            string status = null;
            bool good = true;
            if (k.escapeKey.wasPressedThisFrame) { EndHack(false, "해킹을 포기했습니다. 다시 E로 도전할 수 있습니다 (1턴)."); return; }
            if (_hacking.IsTimedOut) { EndHack(false, "시간 초과! 해킹에 실패했습니다. 다시 E로 도전할 수 있습니다 (1턴)."); return; }
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)
            {
                string input = _game.HackInput;
                if (_hacking.Submit(input))
                {
                    if (_hacking.IsSucceeded) { EndHack(true, null); return; }
                    status = "정상 처리";
                }
                else if (input.Trim().Length > 0)
                {
                    status = $"'{input.Trim()}' 은(는) 틀렸습니다";
                    good = false;
                }
                _game.ClearHackInput();
            }
            _game.UpdateHack(HackWordsText(), _hacking.TimeLeft, _hacking.TimeLimit, status, good);
        }

        /// <summary>단어 줄: 친 단어는 초록, 지금 단어는 노랑 굵게, 남은 단어는 흐리게</summary>
        private string HackWordsText()
        {
            var parts = new List<string>();
            for (int i = 0; i < _hacking.Words.Count; i++)
            {
                string w = _hacking.Words[i];
                parts.Add(i < _hacking.Index ? $"<color=#3ccf7a>{w}</color>"
                        : i == _hacking.Index ? $"<color=#ffd23f><b>[{w}]</b></color>"
                        : $"<color=#8c9bad>{w}</color>");
            }
            return string.Join("   ", parts) + $"\n<size=22><color=#8c9bad>{_hacking.Index} / {_hacking.Words.Count}</color></size>";
        }

        private void EndHack(bool success, string failMessage)
        {
            _game.HideHack();
            _mode = Mode.Playing;
            Block();
            if (success)
            {
                var applied = _board.CompleteHack(_hackTarget);
                _view.Sync();
                int removed = applied.Count(e => e.Type == "remove");
                int keys = applied.Count(e => e.Type == "giveKey");
                string effect = removed > 0 && keys > 0 ? " 장치가 작동하고 열쇠를 받았습니다."
                              : removed > 0 ? " 장치가 작동했습니다." : keys > 0 ? " 열쇠를 받았습니다." : "";
                _game.Toast("컴퓨터를 복구했습니다." + effect);
            }
            else _game.Toast(failMessage);
            _hacking = null;
            _hackTarget = null;
            UpdateHud();
        }

        private void TryTalk(BoardObject npc)
        {
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
            ShowSpeech(_dialogue.Current.Speaker ?? _dialogue.Npc.Name, _dialogue.Current.Portrait, _dialogue.Text);
            UpdateHud();
        }

        /// <summary>
        /// 대화창에 대사 한 줄. 주인공은 왼쪽, 다른 인물은 오른쪽에 일러스트를 둔다.
        /// 주인공: portrait가 player이거나, 이름표가 {nickname}·"집행자"이거나, 내 닉네임과 같을 때
        /// </summary>
        /// <param name="rawSpeaker">{nickname} 치환 전 이름표</param>
        private void ShowSpeech(string rawSpeaker, string portraitKey, string text)
        {
            string speaker = TextFormat.Apply(rawSpeaker, PlayerSession.Nickname);
            bool protagonist = rawSpeaker != null && (rawSpeaker.Contains("{nickname}") || rawSpeaker == "집행자"
                               || (!string.IsNullOrEmpty(PlayerSession.Nickname) && rawSpeaker == PlayerSession.Nickname));
            // 주인공 대사는 portrait가 없어도 주인공 일러스트를 쓴다
            if (string.IsNullOrEmpty(portraitKey) && protagonist) portraitKey = "player";
            Sprite portrait = ArtCatalog.Portrait(portraitKey, speaker, out string characterId);
            protagonist |= characterId == "player";
            _game.ShowLine(speaker, text, portrait, !protagonist, speaker);
        }

        // ---------------- 끝 ----------------
        private void FinishStage()
        {
            _endAt = -1;

            // 기록 = 구간 최고 기록의 합 (워프로 다시 통과한 구간은 좋은 쪽만). 체크포인트가 없으면 쓴 턴과 같다
            int record = _board.RecordMoves;
            int best = StageProgress.BestMoves(_chapter.Id, _stage.No);
            bool newBest = StageProgress.RecordClear(_chapter.Id, _stage.No, record);

            // 서버 스테이지면 결과를 보낸다 (아웃트로 나레이션을 보는 동안 보내고, 결과 화면에서 응답을 쓴다)
            ChapterInfo chapter = _chapter;
            StageInfo stage = _stage;
            StageResultRequest request = _board.ToResultRequest();
            Task<StageResultResponse> submit = null;
            if (PlayerSession.IsOnline && stage.StageId != null)
            {
                _runSubmitted = true;
                submit = StageApi.SubmitResultAsync(stage.StageId.Value, request);
            }

            async void ShowResult()
            {
                string serverLine = "";
                if (submit != null)
                {
                    try
                    {
                        StageResultResponse res = await submit;
                        ApplyServerResult(chapter, stage, res);
                        newBest = res.IsNewBest;
                        best = res.BestMoves ?? best;
                        serverLine = ServerResultText(res);
                    }
                    catch (ApiException e)
                    {
                        if (e.IsNetworkError) PendingResults.Add(stage.StageId.Value, request);
                        serverLine = e.IsNetworkError
                            ? "\n서버에 저장하지 못했습니다. 다음에 접속할 때 다시 보냅니다."
                            : "\n서버 저장 실패: " + e.Message;
                    }
                }
                if (_board == null || _stage != stage) return; // 기다리는 동안 다른 화면으로 갔으면 그만
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
                string star3 = tutorial ? "" : _board.HiddenTotal == 0 ? ""
                    : _board.AllHiddenFound ? $"\n★3 달성 (숨겨진 요소 {_board.HiddenTotal}개)"
                    : $"\n★3 미달성 (숨겨진 요소 {_board.HiddenCollected}/{_board.HiddenTotal})";
                string guide = _board.EncounteredGuidebookIds.Count > 0 ? $"\n도감 기록 {_board.EncounteredGuidebookIds.Count}종" : "";
                _game.ShowResult($"클리어!\n\n{_stage.Title}\n{detail}{star2}{star3}{guide}" +
                                 (newBest ? "\n최고 기록" : $"\n최고 {best}턴") + serverLine, true, buttons);
            }

            if (_stage.Map.Story?.Outro?.Count > 0) PlayStory(_stage.Map.Story.Outro, ShowResult);
            else ShowResult();
        }

        /// <summary>서버 응답을 화면 기록에 반영한다: 코인, 최고 기록·별, 다음 스테이지 해금. 새로 열린 맵은 뒤에서 다시 불러온다</summary>
        private void ApplyServerResult(ChapterInfo chapter, StageInfo stage, StageResultResponse res)
        {
            if (PlayerSession.Profile != null) PlayerSession.Profile.Coin = res.TotalCoin;
            StageProgress.RemoteRecord record = StageProgress.RemoteOf(chapter.Id, stage.No);
            if (record != null)
            {
                record.Cleared = res.Cleared || record.Cleared;
                record.Best = res.BestMoves ?? record.Best;
                record.Stars = res.Stars ?? record.Stars;
            }
            if (res.NextStageId != null)
            {
                foreach (ChapterInfo c in _chapters)
                foreach (StageInfo s in c.Stages)
                {
                    if (s.StageId != res.NextStageId) continue;
                    StageProgress.RemoteRecord next = StageProgress.RemoteOf(c.Id, s.No);
                    if (next != null) next.Unlocked = true;
                }
                ReloadChapters();
            }
        }

        /// <summary>결과 화면의 서버 줄: 누적 별, 첫 클리어 보상, 해금된 도감</summary>
        private static string ServerResultText(StageResultResponse res)
        {
            var sb = new System.Text.StringBuilder();
            if (res.Stars != null) sb.Append($"\n누적 별 {res.Stars.Count}/{res.Stars.Max}");
            if (res.FirstClear && res.RewardCoin > 0) sb.Append($"\n첫 클리어 보상 +{res.RewardCoin} 코인 (보유 {res.TotalCoin})");
            if (res.UnlockedGuidebooks?.Count > 0)
            {
                sb.Append("\n도감 해금: " + string.Join(", ", res.UnlockedGuidebooks.Select(g => g.Title)));
            }
            return sb.ToString();
        }

        private void NextStage()
        {
            StageInfo next = _chapter.Stages.FirstOrDefault(s => s.No > _stage.No);
            if (next != null && StageProgress.IsStageUnlocked(_chapter, next)) StartStage(next, true);
            else ShowStageSelect();
        }
    }
}
