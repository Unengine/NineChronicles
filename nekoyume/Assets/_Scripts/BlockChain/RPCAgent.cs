using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using Grpc.Core;
using Libplanet;
using Libplanet.Action;
using Libplanet.Crypto;
using Libplanet.Tx;
using MagicOnion.Client;
using Nekoyume.Action;
using Nekoyume.Helper;
using Nekoyume.Model.State;
using Nekoyume.Shared.Hubs;
using Nekoyume.Shared.Services;
using Nekoyume.State;
using UniRx;
using UnityEngine;
using static Nekoyume.Action.ActionBase;

namespace Nekoyume.BlockChain
{
    public class RPCAgent : MonoBehaviour, IAgent, IActionEvaluationHubReceiver
    {
        private const float TxProcessInterval = 3.0f;
        
        private readonly Subject<long> _blockIndexSubject = new Subject<long>();
        
        private PrivateKey _privateKey;

        private readonly ConcurrentQueue<PolymorphicAction<ActionBase>> _queuedActions =
            new ConcurrentQueue<PolymorphicAction<ActionBase>>();

        private Channel _channel;
    
        private IActionEvaluationHub _hub;

        private IBlockChainService _service;

        private Codec _codec = new Codec();

        private ActionRenderer _renderer;

        private Subject<ActionEvaluation<ActionBase>> _renderSubject;
        private Subject<ActionEvaluation<ActionBase>> _unrenderSubject;

        public ActionRenderer ActionRenderer { get => _renderer; }

        public Subject<long> BlockIndexSubject { get => _blockIndexSubject; }

        public long BlockIndex { get; private set; }

        public Address Address { get => _privateKey.PublicKey.ToAddress(); }


        public void Initialize(
            CommandLineOptions options,
            PrivateKey privateKey,
            Action<bool> callback)
        {
            _privateKey = privateKey;

            _channel = new Channel(
                options.ClientHost, 
                options.ClientPort, 
                ChannelCredentials.Insecure
            );
            _hub = StreamingHubClient.Connect<IActionEvaluationHub, IActionEvaluationHubReceiver>(_channel, this);
            _service = MagicOnionClient.Create<IBlockChainService>(_channel);

            StartCoroutine(CoTxProcessor());
            StartCoroutine(CoJoin(callback));   
        }

        public IValue GetState(Address address)
        {
            byte[] raw = _service.GetState(address.ToByteArray()).ResponseAsync.Result;
            return _codec.Decode(raw);
        }

        public void EnqueueAction(GameAction action)
        {
            _queuedActions.Enqueue(action);
        }

        #region Mono

        private void Awake()
        {            
            _renderSubject = new Subject<ActionEvaluation<ActionBase>>();
            _unrenderSubject = new Subject<ActionEvaluation<ActionBase>>();
            _renderer = new ActionRenderer(_renderSubject, _unrenderSubject);
        }

        private async void OnDestroy()
        {
            StopAllCoroutines();
            if (!(_hub is null))
            {
                await _hub.DisposeAsync();
            }
            if (!(_channel is null))
            {
                await _channel?.ShutdownAsync();
            }
        }

        #endregion

        private IEnumerator CoJoin(Action<bool> callback)
        {
            Task t = Task.Run(async () => {
                await _hub.JoinAsync();
            });

            yield return new WaitUntil(() => t.IsCompleted);
            
            // 랭킹의 상태를 한 번 동기화 한다.
            States.Instance.SetRankingState(
                GetState(RankingState.Address) is Bencodex.Types.Dictionary rankingDict
                    ? new RankingState(rankingDict)
                    : new RankingState());

            // 상점의 상태를 한 번 동기화 한다.
            States.Instance.SetShopState(
                GetState(ShopState.Address) is Bencodex.Types.Dictionary shopDict
                    ? new ShopState(shopDict)
                    : new ShopState());

            if (ArenaHelper.TryGetThisWeekState(BlockIndex, out var weeklyArenaState))
            {
                States.Instance.SetWeeklyArenaState(weeklyArenaState);
            }
            else
                throw new FailedToInstantiateStateException<WeeklyArenaState>();

            // 에이전트의 상태를 한 번 동기화 한다.
            States.Instance.SetAgentState(
                GetState(Address) is Bencodex.Types.Dictionary agentDict
                    ? new AgentState(agentDict)
                    : new AgentState(Address));

            // 그리고 모든 액션에 대한 랜더와 언랜더를 핸들링하기 시작한다.
            ActionRenderHandler.Instance.Start(ActionRenderer);
            ActionUnrenderHandler.Instance.Start(ActionRenderer);

            callback(true);
        }

        private IEnumerator CoTxProcessor()
        {
            while (true)
            {
                yield return new WaitForSeconds(TxProcessInterval);
                
                var actions = new List<PolymorphicAction<ActionBase>>();
                while (_queuedActions.TryDequeue(out PolymorphicAction<ActionBase> action))
                {
                    actions.Add(action);
                }

                if (actions.Any())
                {
                    Task task = Task.Run(async () => 
                    {
                        await MakeTransaction(actions);
                    });
                    yield return new WaitUntil(() => task.IsCompleted);
                }
            }
        }

        private async Task MakeTransaction(List<PolymorphicAction<ActionBase>> actions)
        {
            long nonce = await GetNonceAsync();
            Transaction<PolymorphicAction<ActionBase>> tx = 
                Transaction<PolymorphicAction<ActionBase>>.Create(
                    nonce,
                    _privateKey,
                    actions
                );
            await _service.PutTransaction(tx.Serialize(true));
        }

        private async Task<long> GetNonceAsync()
        {
            return await _service.GetNextTxNonce(Address.ToByteArray());
        }

        public void OnRender(byte[] evaluation)
        {
            var formatter = new BinaryFormatter();
            using (var stream = new MemoryStream(evaluation))
            {
                var ev = (ActionEvaluation<ActionBase>)formatter.Deserialize(stream);
                _renderSubject.OnNext(ev);
            }
        }

        public void OnTipChanged(long index)
        {
            BlockIndex = index;
            BlockIndexSubject.Publish(index);
        }
    }
}