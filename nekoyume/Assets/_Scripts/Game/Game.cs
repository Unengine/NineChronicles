using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.CognitoIdentity;
using Bencodex.Types;
using Libplanet;
using Libplanet.Crypto;
using mixpanel;
using Nekoyume.Action;
using Nekoyume.BlockChain;
using Nekoyume.Game.Controller;
using Nekoyume.Game.VFX;
using Nekoyume.Helper;
using Nekoyume.L10n;
using Nekoyume.Model.State;
using Nekoyume.Pattern;
using Nekoyume.State;
using Nekoyume.UI;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Menu = Nekoyume.UI.Menu;

namespace Nekoyume.Game
{
    [RequireComponent(typeof(Agent), typeof(RPCAgent))]
    public class Game : MonoSingleton<Game>
    {
        [SerializeField]
        private Stage stage = null;

        [SerializeField]
        private bool useSystemLanguage = true;

        [SerializeField]
        private LanguageType languageType = default;

        [SerializeField]
        private Prologue prologue = null;

        public States States { get; private set; }

        public LocalLayer LocalLayer { get; private set; }

        public IAgent Agent { get; private set; }

        public Stage Stage => stage;

        // FIXME Action.PatchTableSheet.Execute()에 의해서만 갱신됩니다.
        // 액션 실행 여부와 상관 없이 최신 상태를 반영하게끔 수정해야합니다.
        public TableSheets TableSheets { get; private set; }

        public ActionManager ActionManager { get; private set; }

        public bool IsInitialized { get; private set; }

        public Prologue Prologue => prologue;

        public const string AddressableAssetsContainerPath = nameof(AddressableAssetsContainer);

        private CommandLineOptions _options;

        private AmazonCloudWatchLogsClient _logsClient;

        private string _msg;

        private static readonly string CommandLineOptionsJsonPath =
            Path.Combine(Application.streamingAssetsPath, "clo.json");

        #region Mono & Initialization

        protected override void Awake()
        {
            Application.targetFrameRate = 60;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            base.Awake();
            _options = CommandLineOptions.Load(
                CommandLineOptionsJsonPath
            );

#if !UNITY_EDITOR
            // FIXME 이후 사용자가 원치 않으면 정보를 보내지 않게끔 해야 합니다.
            Mixpanel.SetToken("80a1e14b57d050536185c7459d45195a");

            if (!(_options.PrivateKey is null))
            {
                var privateKey = new PrivateKey(ByteUtil.ParseHex(_options.PrivateKey));
                Address address = privateKey.ToAddress();
                Mixpanel.Identify(address.ToString());
            }

            Mixpanel.Init();
            Mixpanel.Track("Unity/Started");
#endif

            if (_options.RpcClient)
            {
                Agent = GetComponent<RPCAgent>();
                SubscribeRPCAgent();
            }
            else
            {
                Agent = GetComponent<Agent>();
            }

            States = new States();
            LocalLayer = new LocalLayer();
            MainCanvas.instance.InitializeIntro();

#if !UNITY_EDITOR
            var c = new CognitoAWSCredentials("ap-northeast-2:6fea0e84-a609-4774-a407-c63de9dbea7b",
                RegionEndpoint.APNortheast2);
            _logsClient = new AmazonCloudWatchLogsClient(c, RegionEndpoint.APNortheast2);
            Application.logMessageReceivedThreaded += UploadLog;
#endif
        }

        private IEnumerator Start()
        {
#if UNITY_EDITOR
            if (useSystemLanguage)
            {
                yield return L10nManager.Initialize().ToYieldInstruction();
            }
            else
            {
                yield return L10nManager.Initialize(languageType).ToYieldInstruction();
            }
#else
            yield return L10nManager.Initialize(LanguageTypeMapper.ISO396(_options.Language)).ToYieldInstruction();
#endif

            // UI 초기화 1차.
            MainCanvas.instance.InitializeFirst();
            yield return Addressables.InitializeAsync();
            yield return StartCoroutine(CoInitializeTableSheets());
            AudioController.instance.Initialize();
            yield return null;
            // Agent 초기화.
            // Agent를 초기화하기 전에 반드시 Table과 TableSheets를 초기화 함.
            // Agent가 Table과 TableSheets에 약한 의존성을 갖고 있음.(Deserialize 단계 때문)
            var agentInitialized = false;
            var agentInitializeSucceed = false;
            yield return StartCoroutine(
                CoLogin(
                    succeed =>
                    {
                        Debug.Log($"Agent initialized. {succeed}");
                        agentInitialized = true;
                        agentInitializeSucceed = succeed;
                    }
                )
            );
            yield return new WaitUntil(() => agentInitialized);
            // NOTE: Create ActionManager after Agent initialized.
            ActionManager = new ActionManager(Agent);
            yield return StartCoroutine(CoSyncTableSheets());
            // UI 초기화 2차.
            yield return StartCoroutine(MainCanvas.instance.InitializeSecond());
            Stage.Initialize();

            Observable.EveryUpdate()
                .Where(_ => Input.GetMouseButtonUp(0))
                .Select(_ => Input.mousePosition)
                .Subscribe(PlayMouseOnClickVFX)
                .AddTo(gameObject);


            Widget.Find<VersionInfo>().SetVersion(Agent.AppProtocolVersion);

            ShowNext(agentInitializeSucceed);
        }

        private void SubscribeRPCAgent()
        {
            if (!(Agent is RPCAgent rpcAgent))
            {
                return;
            }

            rpcAgent.OnRetryStarted
                .ObserveOnMainThread()
                .Subscribe(OnRPCAgentRetryStarted)
                .AddTo(gameObject);

            // NOTE: RPCAgent가 허브에 조인을 재시도하는 것과 프리로드를 끝마쳤을 때를 구독합니다.
            rpcAgent.OnRetryEnded
                .Zip(rpcAgent.OnPreloadEnded, (agent, agent1) => agent)
                .First()
                .Repeat()
                .ObserveOnMainThread()
                .Subscribe(OnRPCAgentRetryAndPreloadEnded)
                .AddTo(gameObject);

            rpcAgent.OnDisconnected
                .ObserveOnMainThread()
                .Subscribe(QuitWithAgentConnectionError)
                .AddTo(gameObject);
        }

        private static void OnRPCAgentRetryStarted(RPCAgent rpcAgent)
        {
            Widget.Find<BlockSyncLoadingScreen>().Show();
        }

        private static void OnRPCAgentRetryAndPreloadEnded(RPCAgent rpcAgent)
        {
            if (Widget.Find<Intro>().IsActive() ||
                Widget.Find<PreloadingScreen>().IsActive() ||
                Widget.Find<Synopsis>().IsActive())
            {
                // NOTE: 타이틀 화면에서 리트라이와 프리로드가 완료된 상황입니다.
                // FIXME: 이 경우에는 메인 로비가 아니라 기존 초기화 로직이 흐르도록 처리해야 합니다.
                return;
            }

            var needToBackToMain = false;
            var showLoadingScreen = false;
            var widget = (Widget) Widget.Find<BlockSyncLoadingScreen>();
            if (widget.IsActive())
            {
                widget.Close();
            }

            if (Widget.Find<LoadingScreen>().IsActive())
            {
                Widget.Find<LoadingScreen>().Close();
                widget = Widget.Find<QuestPreparation>();
                if (widget.IsActive())
                {
                    widget.Close(true);
                    needToBackToMain = true;
                }

                widget = Widget.Find<Menu>();
                if (widget.IsActive())
                {
                    widget.Close(true);
                    needToBackToMain = true;
                }
            }
            else if (Widget.Find<StageLoadingScreen>().IsActive())
            {
                Widget.Find<StageLoadingScreen>().Close();

                if (Widget.Find<BattleResult>().IsActive())
                {
                    Widget.Find<BattleResult>().Close(true);
                }

                needToBackToMain = true;
                showLoadingScreen = true;
            }
            else if (Widget.Find<ArenaBattleLoadingScreen>().IsActive())
            {
                Widget.Find<ArenaBattleLoadingScreen>().Close();
                needToBackToMain = true;
            }
            else if (Widget.Find<MimisbrunnrPreparation>().IsActive())
            {
                Widget.Find<MimisbrunnrPreparation>().Close(true);
                needToBackToMain = true;
            }

            if (!needToBackToMain)
            {
                return;
            }

            ActionRenderHandler.BackToMain(
                showLoadingScreen,
                new UnableToRenderWhenSyncingBlocksException());
        }

        private void QuitWithAgentConnectionError(RPCAgent rpcAgent)
        {
            var screen = Widget.Find<BlockSyncLoadingScreen>();
            if (screen.IsActive())
            {
                screen.Close();
            }

            // FIXME 콜백 인자를 구조화 하면 타입 쿼리 없앨 수 있을 것 같네요.
            if (Agent is Agent _)
            {
                var errorMsg = string.Format(L10nManager.Localize("UI_ERROR_FORMAT"),
                    L10nManager.Localize("BLOCK_DOWNLOAD_FAIL"));

                Widget.Find<SystemPopup>().Show(
                    L10nManager.Localize("UI_ERROR"),
                    errorMsg,
                    L10nManager.Localize("UI_QUIT"),
                    false
                );

                return;
            }

            if (rpcAgent is null)
            {
                // FIXME: 최신 버전이 뭔지는 Agent.EncounrtedHighestVersion 속성에 들어있으니, 그걸 UI에서 표시해줘야 할 듯?
                // AppProtocolVersion? newVersion = _agent is Agent agent ? agent.EncounteredHighestVersion : null;
                Widget.Find<UpdatePopup>().Show();
                return;
            }

            if (rpcAgent.Connected)
            {
                // 무슨 상황이지?
                Debug.Log($"{nameof(QuitWithAgentConnectionError)}() called. But {nameof(RPCAgent)}.Connected is {rpcAgent.Connected}.");
                return;
            }

            Widget.Find<SystemPopup>().Show(
                "UI_ERROR",
                "UI_ERROR_RPC_CONNECTION",
                "UI_QUIT"
            );
        }

        private IEnumerator CoInitializeTableSheets()
        {
            yield return null;
            var request =
                Resources.LoadAsync<AddressableAssetsContainer>(AddressableAssetsContainerPath);
            yield return request;
            if (!(request.asset is AddressableAssetsContainer addressableAssetsContainer))
            {
                throw new FailedToLoadResourceException<AddressableAssetsContainer>(
                    AddressableAssetsContainerPath);
            }

            List<TextAsset> csvAssets = addressableAssetsContainer.tableCsvAssets;
            var csv = new Dictionary<string, string>();
            foreach (var asset in csvAssets)
            {
                csv[asset.name] = asset.text;
            }
            TableSheets = new TableSheets(csv);
        }

        public static IDictionary<string, string> GetTableCsvAssets()
        {
            var container =
                Resources.Load<AddressableAssetsContainer>(AddressableAssetsContainerPath);
            return container.tableCsvAssets.ToDictionary(asset => asset.name, asset => asset.text);
        }

        private void ShowNext(bool succeed)
        {
            IsInitialized = true;
            if (succeed)
            {
                var intro = Widget.Find<Intro>();
                intro.Close();
                Widget.Find<PreloadingScreen>().Show();
                StartCoroutine(ClosePreloadingScene(4));
            }
            else
            {
                QuitWithAgentConnectionError(null);
            }
        }

        private IEnumerator ClosePreloadingScene(float time)
        {
            yield return new WaitForSeconds(time);
            Widget.Find<PreloadingScreen>().Close();
        }

        #endregion

        protected override void OnApplicationQuit()
        {
            if (Mixpanel.IsInitialized())
            {
                Mixpanel.Track("Unity/Player Quit");
                Mixpanel.Flush();
            }
            _logsClient?.Dispose();
        }

        public static void Quit()
        {
            var confirm = Widget.Find<Confirm>();

            if (confirm.gameObject.activeSelf &&
                confirm.title.text == L10nManager.Localize("UI_CONFIRM_QUIT_TITLE"))
            {
                confirm.Close();
                return;
            }


            confirm.CloseCallback = result =>
            {
                if (result == ConfirmResult.Yes)
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    return;
                }

                confirm.CloseCallback = null;

                Event.OnNestEnter.Invoke();
                Widget.Find<Login>().Show();
                Widget.Find<Menu>().Close();
            };

            confirm.Show(
                "UI_CONFIRM_QUIT_TITLE",
                "UI_CONFIRM_QUIT_CONTENT",
                "UI_QUIT",
                "UI_CHARACTER_SELECT",
                blurRadius: 2,
                submittable: false);
        }

        private static void PlayMouseOnClickVFX(Vector3 position)
        {
            position = ActionCamera.instance.Cam.ScreenToWorldPoint(position);
            var vfx = VFXController.instance.CreateAndChaseCam<MouseClickVFX>(position);
            vfx.Play();
        }

        private IEnumerator CoLogin(Action<bool> callback)
        {
            if (_options.Maintenance)
            {
                var w = Widget.Create<SystemPopup>();
                w.CloseCallback = () =>
                {
                    Application.OpenURL(GameConfig.DiscordLink);
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.ExitPlaymode();
#else
                    Application.Quit();
#endif
                };
                w.Show(
                    "UI_MAINTENANCE",
                    "UI_MAINTENANCE_CONTENT",
                    "UI_OK"
                );
                yield break;
            }

            if (_options.TestEnd)
            {
                var w = Widget.Find<Confirm>();
                w.CloseCallback = result =>
                {
                    if (result == ConfirmResult.Yes)
                    {
                        Application.OpenURL(GameConfig.DiscordLink);
                    }

#if UNITY_EDITOR
                    UnityEditor.EditorApplication.ExitPlaymode();
#else
                    Application.Quit();
#endif
                };
                w.Show("UI_TEST_END", "UI_TEST_END_CONTENT", "UI_GO_DISCORD", "UI_QUIT");

                yield break;
            }

            var settings = Widget.Find<UI.Settings>();
            settings.UpdateSoundSettings();
            settings.UpdatePrivateKey(_options.PrivateKey);

            var loginPopup = Widget.Find<LoginPopup>();

            if (Application.isBatchMode)
            {
                loginPopup.Show(_options.KeyStorePath, _options.PrivateKey);
            }
            else
            {
                var intro = Widget.Find<Intro>();
                intro.Show(_options.KeyStorePath, _options.PrivateKey);
                yield return new WaitUntil(() => loginPopup.Login);
            }

            Agent.Initialize(
                _options,
                loginPopup.GetPrivateKey(),
                callback
            );
        }

        public void ResetStore()
        {
            var confirm = Widget.Find<Confirm>();
            var storagePath = _options.StoragePath ?? BlockChain.Agent.DefaultStoragePath;
            confirm.CloseCallback = result =>
            {
                if (result == ConfirmResult.No)
                {
                    return;
                }

                StoreUtils.ResetStore(storagePath);

#if UNITY_EDITOR
                UnityEditor.EditorApplication.ExitPlaymode();
#else
                Application.Quit();
#endif
            };
            confirm.Show("UI_CONFIRM_RESET_STORE_TITLE", "UI_CONFIRM_RESET_STORE_CONTENT");
        }

        public void ResetKeyStore()
        {
            var confirm = Widget.Find<Confirm>();
            confirm.CloseCallback = result =>
            {
                if (result == ConfirmResult.No)
                {
                    return;
                }

                var keyPath = _options.KeyStorePath;
                if (Directory.Exists(keyPath))
                {
                    Directory.Delete(keyPath, true);
                }

#if UNITY_EDITOR
                UnityEditor.EditorApplication.ExitPlaymode();
#else
                Application.Quit();
#endif
            };

            confirm.Show("UI_CONFIRM_RESET_KEYSTORE_TITLE", "UI_CONFIRM_RESET_KEYSTORE_CONTENT");
        }

        private IEnumerator CoSyncTableSheets()
        {
            yield return null;
            var request =
                Resources.LoadAsync<AddressableAssetsContainer>(AddressableAssetsContainerPath);
            yield return request;
            if (!(request.asset is AddressableAssetsContainer addressableAssetsContainer))
            {
                throw new FailedToLoadResourceException<AddressableAssetsContainer>(
                    AddressableAssetsContainerPath);
            }

            List<TextAsset> csvAssets = addressableAssetsContainer.tableCsvAssets;
            var csv = new Dictionary<string, string>();
            foreach (var asset in csvAssets)
            {
                if (Agent.GetState(Addresses.TableSheet.Derive(asset.name)) is Text tableCsv)
                {
                    var table = tableCsv.ToDotnetString();
                    csv[asset.name] = table;
                }

                yield return null;
            }
            TableSheets = new TableSheets(csv);
        }

        private async void UploadLog(string logString, string stackTrace, LogType type)
        {
            // Avoid NRE
            if (Agent.PrivateKey == default)
            {
                _msg += logString + "\n";
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    _msg += stackTrace + "\n";
                }
            }
            else
            {
                const string groupName = "9c-player-logs";
                var streamName = _options.AwsSinkGuid;
                try
                {
                    var req = new CreateLogGroupRequest(groupName);
                    await _logsClient.CreateLogGroupAsync(req);
                }
                catch (ResourceAlreadyExistsException)
                {
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                try
                {
                    var req = new CreateLogStreamRequest(groupName, streamName);
                    await _logsClient.CreateLogStreamAsync(req);
                }
                catch (ResourceAlreadyExistsException)
                {
                    // ignored
                }

                PutLog(groupName, streamName, GetMessage(logString, stackTrace));
            }
        }

        private async void PutLog(string groupName, string streamName, string msg)
        {
            try
            {
                var req = new DescribeLogStreamsRequest(groupName)
                {
                    LogStreamNamePrefix = streamName
                };
                var resp = await _logsClient.DescribeLogStreamsAsync(req);
                var token = resp.LogStreams.FirstOrDefault(s => s.LogStreamName == streamName)?.UploadSequenceToken;
                var ie = new InputLogEvent
                {
                    Message = msg,
                    Timestamp = DateTime.UtcNow
                };
                var request = new PutLogEventsRequest(groupName, streamName, new List<InputLogEvent> {ie});
                if (!string.IsNullOrEmpty(token))
                {
                    request.SequenceToken = token;
                }
                await _logsClient.PutLogEventsAsync(request);
            }
            catch (Exception e)
            {
                // ignored
            }
        }

        private string GetMessage(string logString, string stackTrace)
        {
            var msg = string.Empty;
            if (!string.IsNullOrEmpty(_msg))
            {
                msg = _msg;
                _msg = string.Empty;
                return msg;
            }

            msg += logString + "\n";
            if (!string.IsNullOrEmpty(stackTrace))
            {
                msg += stackTrace;
            }

            return msg;
        }
    }
}
