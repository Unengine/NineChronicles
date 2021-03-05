﻿using System;
using System.Collections.Generic;
using System.Linq;
using Nekoyume.Game.Character;
using Nekoyume.Game.Controller;
using Nekoyume.L10n;
using Nekoyume.Model.Item;
using Nekoyume.State;
using Nekoyume.UI.Model;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using ShopItem = Nekoyume.UI.Model.ShopItem;

namespace Nekoyume.UI.Module
{
    public class ShopSellItems : MonoBehaviour
    {
        public const int shopItemsCountOfOnePage = 20;

        public List<ShopItemView> items;

        [SerializeField]
        private TMP_Dropdown itemSubTypeFilter = null;

        [SerializeField]
        private TMP_Dropdown sortFilter = null;

        [SerializeField]
        private Button previousPageButton = null;

        [SerializeField]
        private InteractableSwitchableSelectable previousPageButtonInteractableSwitch = null;

        [SerializeField]
        private Button nextPageButton = null;

        [SerializeField]
        private InteractableSwitchableSelectable nextPageButtonInteractableSwitch = null;

        [SerializeField]
        private TouchHandler refreshButtonTouchHandler = null;

        [SerializeField]
        private RefreshButton refreshButton = null;

        private int _filteredPageIndex;
        private readonly List<IDisposable> _disposablesAtOnEnable = new List<IDisposable>();

        public Model.ShopItems SharedModel { get; private set; }

        #region Mono

        private void Awake()
        {
            SharedModel = new Model.ShopItems();
            SharedModel.State
                .Subscribe(_ => UpdateView())
                .AddTo(gameObject);
            SharedModel.AgentProducts
                .Subscribe(_ => UpdateView())
                .AddTo(gameObject);
            SharedModel.ItemSubTypeProducts
                .Subscribe(_ => UpdateView())
                .AddTo(gameObject);

            itemSubTypeFilter.AddOptions(new[]
                {
                    ItemSubTypeFilter.All,
                    ItemSubTypeFilter.Equipment,
                    ItemSubTypeFilter.Food,
                    ItemSubTypeFilter.Costume,
                    ItemSubTypeFilter.Weapon,
                    ItemSubTypeFilter.Armor,
                    ItemSubTypeFilter.Belt,
                    ItemSubTypeFilter.Necklace,
                    ItemSubTypeFilter.Ring,
                    ItemSubTypeFilter.Food_HP,
                    ItemSubTypeFilter.Food_ATK,
                    ItemSubTypeFilter.Food_DEF,
                    ItemSubTypeFilter.Food_CRI,
                    ItemSubTypeFilter.Food_HIT,
                    ItemSubTypeFilter.FullCostume,
                    ItemSubTypeFilter.HairCostume,
                    ItemSubTypeFilter.EarCostume,
                    ItemSubTypeFilter.EyeCostume,
                    ItemSubTypeFilter.TailCostume,
                    ItemSubTypeFilter.Title,
                }
                .Select(type => type == ItemSubTypeFilter.All
                    ? L10nManager.Localize("ALL")
                    : ((ItemSubType) Enum.Parse(typeof(ItemSubType), type.ToString()))
                    .GetLocalizedString())
                .ToList());
            itemSubTypeFilter.onValueChanged.AsObservable()
                .Select(index =>
                {
                    try
                    {
                        return (ItemSubTypeFilter) index;
                    }
                    catch
                    {
                        return ItemSubTypeFilter.All;
                    }
                })
                .Subscribe(filter =>
                {
                    SharedModel.itemSubTypeFilter = filter;
                    OnItemSubTypeFilterChanged(SharedModel.itemSubTypeFilter);
                })
                .AddTo(gameObject);

            sortFilter.AddOptions(new[]
                {
                    SortFilter.Class,
                    SortFilter.CP,
                    SortFilter.Price,
                }
                .Select(type => L10nManager.Localize($"UI_{type.ToString().ToUpper()}"))
                .ToList());
            sortFilter.onValueChanged.AsObservable()
                .Select(index =>
                {
                    try
                    {
                        return (SortFilter) index;
                    }
                    catch
                    {
                        return SortFilter.Class;
                    }
                })
                .Subscribe(filter =>
                {
                    SharedModel.sortFilter = filter;
                    OnSortFilterChanged(SharedModel.sortFilter);
                })
                .AddTo(gameObject);

            previousPageButton.OnClickAsObservable()
                .Subscribe(OnPreviousPageButtonClick)
                .AddTo(gameObject);
            nextPageButton.OnClickAsObservable()
                .Subscribe(OnNextPageButtonClick)
                .AddTo(gameObject);

            refreshButtonTouchHandler.OnClick.Subscribe(_ =>
            {
                AudioController.PlayClick();
                // NOTE: 아래 코드를 실행해도 아무런 변화가 없습니다.
                // 새로고침을 새로 정의한 후에 수정합니다.
                // SharedModel.ResetItemSubTypeProducts();
            }).AddTo(gameObject);
        }

        private void OnEnable()
        {
            itemSubTypeFilter.SetValueWithoutNotify(0);
            SharedModel.itemSubTypeFilter = 0;
            sortFilter.SetValueWithoutNotify(0);
            SharedModel.sortFilter = 0;

            ReactiveShopState.AgentProducts
                .Subscribe(SharedModel.ResetAgentProducts)
                .AddTo(_disposablesAtOnEnable);

            ReactiveShopState.ItemSubTypeProducts
                .Subscribe(SharedModel.ResetItemSubTypeProducts)
                .AddTo(_disposablesAtOnEnable);
        }

        private void OnDisable()
        {
            _disposablesAtOnEnable.DisposeAllAndClear();
        }

        private void OnDestroy()
        {
            SharedModel.Dispose();
            SharedModel = null;
        }

        #endregion

        private void UpdateView()
        {
            foreach (var item in items)
            {
                item.Clear();
            }

            if (SharedModel is null)
            {
                return;
            }

            _filteredPageIndex = 0;
            UpdateViewWithFilteredPageIndex(SharedModel.AgentProducts.Value);
            refreshButton.gameObject.SetActive(false);
        }

        private void UpdateViewWithFilteredPageIndex(
            IReadOnlyDictionary<int, List<ShopItem>> models)
        {
            var count = models?.Count ?? 0;
            UpdateViewWithItems(count > _filteredPageIndex
                ? models[_filteredPageIndex]
                : new List<ShopItem>());

            if (_filteredPageIndex > 0)
            {
                previousPageButtonInteractableSwitch.SetSwitchOn();
            }
            else
            {
                previousPageButtonInteractableSwitch.SetSwitchOff();
            }

            if (_filteredPageIndex + 1 < count)
            {
                nextPageButtonInteractableSwitch.SetSwitchOn();
            }
            else
            {
                nextPageButtonInteractableSwitch.SetSwitchOff();
            }
        }

        private void UpdateViewWithItems(IEnumerable<ShopItem> viewModels)
        {
            using (var itemViews = items.GetEnumerator())
            using (var itemModels = viewModels.GetEnumerator())
            {
                while (itemViews.MoveNext())
                {
                    if (itemViews.Current is null)
                    {
                        break;
                    }

                    if (!itemModels.MoveNext())
                    {
                        itemViews.Current.Clear();
                        continue;
                    }

                    itemViews.Current.SetData(itemModels.Current);
                }
            }
        }

        private void OnItemSubTypeFilterChanged(ItemSubTypeFilter filter)
        {
            SharedModel.ResetAgentProducts();
            SharedModel.ResetItemSubTypeProducts();
        }

        private void OnSortFilterChanged(SortFilter filter)
        {
            SharedModel.ResetAgentProducts();
            SharedModel.ResetItemSubTypeProducts();
        }

        private void OnPreviousPageButtonClick(Unit unit)
        {
            if (_filteredPageIndex == 0)
            {
                previousPageButtonInteractableSwitch.SetSwitchOff();
                return;
            }

            _filteredPageIndex--;
            nextPageButtonInteractableSwitch.SetSwitchOn();

            if (_filteredPageIndex == 0)
            {
                previousPageButtonInteractableSwitch.SetSwitchOff();
            }

            UpdateViewWithFilteredPageIndex(SharedModel.AgentProducts.Value);
        }

        private void OnNextPageButtonClick(Unit unit)
        {
            var count = SharedModel.AgentProducts.Value.Count;


            if (_filteredPageIndex + 1 >= count)
            {
                nextPageButtonInteractableSwitch.SetSwitchOff();
                return;
            }

            _filteredPageIndex++;
            previousPageButtonInteractableSwitch.SetSwitchOn();

            if (_filteredPageIndex + 1 == count)
            {
                nextPageButtonInteractableSwitch.SetSwitchOff();
            }

            UpdateViewWithFilteredPageIndex(SharedModel.AgentProducts.Value);
        }
    }
}
