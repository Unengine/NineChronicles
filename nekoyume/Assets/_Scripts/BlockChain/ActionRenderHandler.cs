using System;
using System.Collections.Generic;
using Nekoyume.Action;
using Nekoyume.Game.Item;
using Nekoyume.Model;
using Nekoyume.State;
using UniRx;

namespace Nekoyume.BlockChain
{
    public class ActionRenderHandler
    {
        private static class Singleton
        {
            internal static readonly ActionRenderHandler Value = new ActionRenderHandler();
        }

        public static readonly ActionRenderHandler Instance = Singleton.Value;
        
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        private ActionRenderHandler()
        {
        }

        public void Start()
        {
            RewardGold();
            CreateAvatar();
            DeleteAvatar();
            HackAndSlash();
            Combination();
            Sell();
            SellCancellation();
            Buy();
            Ranking();
        }

        public void Stop()
        {
            _disposables.DisposeAllAndClear();
        }
        
        private void RewardGold()
        {
            ActionBase.EveryRender<RewardGold>()
                .Where(eval => eval.InputContext.Signer == States.Instance.agentState.Value.address)
                .ObserveOnMainThread()
                .Subscribe(eval =>
                {
                    ReactiveAgentState.Gold.Value = States.Instance.agentState.Value.gold += eval.Action.gold;
                }).AddTo(_disposables);
        }

        private void CreateAvatar()
        {
            ActionBase.EveryRender<CreateAvatar>()
                .Where(eval => eval.InputContext.Signer == States.Instance.agentState.Value.address
                               && eval.Action.Succeed)
                .ObserveOnMainThread()
                .Subscribe(eval =>
                {
                    var index = eval.Action.index;
                    var avatarAddress = AvatarManager.GetOrCreateAvatarAddress(index);
                    States.Instance.agentState.Value.avatarAddresses.Add(index, avatarAddress);
                    States.Instance.avatarStates.Add(index,
                        (AvatarState) AgentController.Agent.GetState(avatarAddress));
                }).AddTo(_disposables);
        }
        
        private void DeleteAvatar()
        {
            ActionBase.EveryRender<DeleteAvatar>()
                .Where(eval => eval.InputContext.Signer == States.Instance.agentState.Value.address
                               && eval.Action.Succeed)
                .ObserveOnMainThread()
                .Subscribe(eval =>
                {
                    var index = eval.Action.index;
                    States.Instance.agentState.Value.avatarAddresses.Remove(index);
                    States.Instance.avatarStates.Remove(index);
                    AvatarManager.DeleteAvatarPrivateKey(index);
                }).AddTo(_disposables);
        }
        
        private void HackAndSlash()
        {
            ActionBase.EveryRender<HackAndSlash>()
                .Where(eval => eval.InputContext.Signer == States.Instance.currentAvatarState.Value.address
                               && eval.Action.Succeed)
                .ObserveOnMainThread()
                .Subscribe(eval =>
                {
                    var state = (AvatarState) AgentController.Agent.GetState(States.Instance.currentAvatarState.Value.address);
                    foreach (var item in States.Instance.avatarStates)
                    {
                        if (item.Value.address != state.address)
                        {
                            continue;
                        }
                        
                        States.Instance.avatarStates[item.Key] = state;
                        break;
                    }
                    
                    ReactiveCurrentAvatarState.AvatarState.Value = States.Instance.currentAvatarState.Value = state;
                }).AddTo(_disposables);
        }

        private void Combination()
        {
            ActionBase.EveryRender<Combination>()
                .Where(eval => eval.InputContext.Signer == States.Instance.currentAvatarState.Value.address
                               && (eval.Action.Succeed || eval.Action.errorCode == GameAction.ErrorCode.CombinationNoResultItem))
                .ObserveOnMainThread()
                .Subscribe(eval =>
                {
                    foreach (var material in eval.Action.Materials)
                    {
                        States.Instance.currentAvatarState.Value.inventory.RemoveFungibleItem(material.id, material.count);
                    }

                    if (eval.Action.errorCode == GameAction.ErrorCode.CombinationNoResultItem)
                    {
                        return;
                    }
                    
                    foreach (var itemUsable in eval.Action.Results)
                    {
                        States.Instance.currentAvatarState.Value.inventory.AddUnfungibleItem(itemUsable);
                    }
                }).AddTo(_disposables);
        }

        private void Sell()
        {
            ActionBase.EveryRender<Sell>()
                .Where(eval => eval.Action.Succeed)
                .ObserveOnMainThread()
                .Subscribe(eval =>
                {
                    var result = eval.Action.result;
                    if (eval.InputContext.Signer == States.Instance.currentAvatarState.Value.address)
                    {
                        States.Instance.currentAvatarState.Value.inventory.RemoveUnfungibleItem(result.shopItem.itemUsable);
                    }
                    
                    ShopState.Register(ReactiveShopState.Items, States.Instance.currentAvatarState.Value.address,
                        result.shopItem);
                }).AddTo(_disposables);
        }

        private void SellCancellation()
        {
            ActionBase.EveryRender<SellCancellation>()
                .Where(eval => eval.Action.Succeed)
                .ObserveOnMainThread()
                .Subscribe(eval =>
                {
                    var result = eval.Action.result;
                    if (eval.InputContext.Signer == States.Instance.currentAvatarState.Value.address)
                    {
                        States.Instance.currentAvatarState.Value.inventory.AddUnfungibleItem(result.shopItem.itemUsable);
                    }
                    
                    ShopState.Unregister(ReactiveShopState.Items, result.owner, result.shopItem.productId);
                }).AddTo(_disposables);
        }

        private void Buy()
        {
            ActionBase.EveryRender<Buy>()
                .Where(eval => eval.Action.Succeed)
                .ObserveOnMainThread()
                .Subscribe(eval =>
                {
                    var result = eval.Action.result;
                    if (eval.InputContext.Signer == States.Instance.currentAvatarState.Value.address)
                    {
                        States.Instance.currentAvatarState.Value.inventory.AddUnfungibleItem(result.shopItem.itemUsable);
                    }
                    
                    ShopState.Unregister(ReactiveShopState.Items, result.owner, result.shopItem.productId);
                }).AddTo(_disposables);
        }

        private void Ranking()
        {
            ActionBase.EveryRender(RankingState.Address)
                .ObserveOnMainThread()
                .Subscribe(eval =>
                {
                    var asGameAction = eval.Action as GameAction;
                    if (asGameAction is null || asGameAction.Succeed) 
                    {
                        var state = (RankingState) eval.OutputStates.GetState(RankingState.Address);
                        ReactiveRankingState.RankingState.Value = States.Instance.rankingState.Value = state;
                    }
                })
                .AddTo(_disposables);
        }
    }
}
