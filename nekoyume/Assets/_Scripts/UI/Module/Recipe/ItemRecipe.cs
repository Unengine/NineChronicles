using System;
using System.Collections.Generic;
using System.Linq;
using Nekoyume.UI.Scroller;
using Nekoyume.Model.Item;
using UniRx;
using Nekoyume.State;
using UnityEngine;
using UnityEngine.UI;
using Nekoyume.UI.Tween;
using Nekoyume.Model.Quest;
using Nekoyume.Game.Controller;
using Nekoyume.Game.VFX;
using Nekoyume.Model.Stat;

namespace Nekoyume.UI.Module
{
    public class ItemRecipe : MonoBehaviour
    {
        [SerializeField]
        private RecipeCellView cellViewPrefab = null;

        [SerializeField]
        private RecipeCellView[] cellViews = null;

        [SerializeField]
        private TabButton weaponTabButton = null;

        [SerializeField]
        private TabButton armorTabButton = null;

        [SerializeField]
        private TabButton beltTabButton = null;

        [SerializeField]
        private TabButton necklaceTabButton = null;

        [SerializeField]
        private TabButton ringTabButton = null;

        [SerializeField]
        private TabButton hpTabButton = null;

        [SerializeField]
        private TabButton atkTabButton = null;

        [SerializeField]
        private TabButton criTabButton = null;

        [SerializeField]
        private TabButton hitTabButton = null;

        [SerializeField]
        private TabButton defTabButton = null;

        [SerializeField]
        private Transform cellViewParent = null;

        [SerializeField]
        private ScrollRect scrollRect = null;

        [SerializeField]
        private DOTweenGroupAlpha scrollAlphaTweener = null;

        [SerializeField]
        private AnchoredPositionYTweener scrollPositionTweener = null;

        [SerializeField]
        private GameObject equipmentTabs = null;

        [SerializeField]
        private GameObject consumableTabs = null;

        private bool _initialized = false;
        private int _notificationId;

        private readonly ToggleGroup _equipmentToggleGroup = new ToggleGroup();

        private readonly ToggleGroup _consumableToggleGroup = new ToggleGroup();

        private readonly ReactiveProperty<ItemSubType> _itemFilterType =
            new ReactiveProperty<ItemSubType>(ItemSubType.Weapon);

        private readonly ReactiveProperty<StatType> _statFilterType =
            new ReactiveProperty<StatType>(StatType.HP);

        private readonly List<IDisposable> _disposablesAtLoadRecipeList = new List<IDisposable>();

        private readonly ReactiveProperty<State> _state = new ReactiveProperty<State>(State.Equipment);

        private Dictionary<string, ItemSubType> _equipmentToggleableMap;

        private Dictionary<string, StatType> _consumableToggleableMap;

        public bool HasNotification { get; private set; }

        public enum State
        {
            Equipment,
            Consumable,
        }

        private void Awake()
        {
            Initialize();
        }


        private void OnDisable()
        {
            foreach (var view in cellViews)
            {
                view.shakeTweener.KillTween();
            }
        }

        private void OnDestroy()
        {
            _itemFilterType.Dispose();
            _statFilterType.Dispose();
            _disposablesAtLoadRecipeList.DisposeAllAndClear();
        }

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            var combination = Widget.Find<Combination>();

            _initialized = true;
            _equipmentToggleGroup.OnToggledOn.Subscribe(SubscribeOnEquipmentToggledOn).AddTo(gameObject);
            _equipmentToggleGroup.RegisterToggleable(weaponTabButton);
            _equipmentToggleGroup.RegisterToggleable(armorTabButton);
            _equipmentToggleGroup.RegisterToggleable(beltTabButton);
            _equipmentToggleGroup.RegisterToggleable(necklaceTabButton);
            _equipmentToggleGroup.RegisterToggleable(ringTabButton);
            _equipmentToggleGroup.DisabledFunc = () => !combination.CanHandleInputEvent;

            _consumableToggleGroup.OnToggledOn.Subscribe(SubscribeOnConsumableToggledOn).AddTo(gameObject);
            _consumableToggleGroup.RegisterToggleable(hpTabButton);
            _consumableToggleGroup.RegisterToggleable(atkTabButton);
            _consumableToggleGroup.RegisterToggleable(criTabButton);
            _consumableToggleGroup.RegisterToggleable(hitTabButton);
            _consumableToggleGroup.RegisterToggleable(defTabButton);
            _consumableToggleGroup.DisabledFunc = () => !combination.CanHandleInputEvent;

            _equipmentToggleableMap = new Dictionary<string, ItemSubType>()
            {
                {weaponTabButton.Name, ItemSubType.Weapon},
                {armorTabButton.Name, ItemSubType.Armor},
                {beltTabButton.Name, ItemSubType.Belt},
                {necklaceTabButton.Name, ItemSubType.Necklace},
                {ringTabButton.Name, ItemSubType.Ring},
            };

            _consumableToggleableMap = new Dictionary<string, StatType>()
            {
                {hpTabButton.Name, StatType.HP},
                {atkTabButton.Name, StatType.ATK},
                {criTabButton.Name, StatType.CRI},
                {hitTabButton.Name, StatType.HIT},
                {defTabButton.Name, StatType.DEF},
            };

            LoadRecipes();
            _itemFilterType.Subscribe(SubScribeFilterType).AddTo(gameObject);
            _statFilterType.Subscribe(SubScribeFilterType).AddTo(gameObject);
            _state.Subscribe(SubscribeState).AddTo(gameObject);
            ReactiveAvatarState.QuestList.Subscribe(SubscribeHasNotification)
                .AddTo(gameObject);
        }

        public void ShowEquipmentCellViews(int? recipeId = null)
        {
            if (recipeId.HasValue &&
                TryGetCellView(recipeId.Value, out var cellView) &&
                Game.Game.instance.TableSheets.EquipmentItemSheet.TryGetValue(
                    cellView.EquipmentRowData.ResultEquipmentId,
                    out var row))
            {
                SetToggledOnItemType(row.ItemSubType);
                var content = scrollRect.content;
                var localPositionX = content.localPosition.x;
                content.localPosition = new Vector2(
                    localPositionX,
                    -cellView.transform.localPosition.y);
            }
            else
            {
                SetToggledOnItemType(_itemFilterType.Value);
            }

            scrollAlphaTweener.Play();
            scrollPositionTweener.PlayTween();

            foreach (var view in cellViews)
            {
                view.SetInteractable(true);
                view.Show();
            }
        }

        public void ShowConsumableCellViews()
        {
            SetToggledOnFilterType(_statFilterType.Value);
            scrollAlphaTweener.Play();
            scrollPositionTweener.PlayTween();

            foreach (var view in cellViews)
            {
                view.SetInteractable(true);
            }
        }

        public void HideCellViews()
        {
            scrollAlphaTweener.PlayReverse();
            scrollPositionTweener.PlayReverse();
            foreach (var view in cellViews)
            {
                view.SetInteractable(false);
            }
        }

        private void LoadRecipes()
        {
            _disposablesAtLoadRecipeList.DisposeAllAndClear();

            var recipeSheet = Game.Game.instance.TableSheets.EquipmentItemRecipeSheet;
            var ids = recipeSheet.Values.Select(r => r.ResultEquipmentId).ToList();
            var equipments = Game.Game.instance.TableSheets.EquipmentItemSheet.Values.Where(r => ids.Contains(r.Id));
            var maxCount = equipments.GroupBy(r => r.ItemSubType).Select(x => x.Count()).Max();
            cellViews = new RecipeCellView[maxCount];

            var idx = 0;
            foreach (var recipeRow in recipeSheet)
            {
                if (idx < maxCount)
                {
                    var cellView = Instantiate(cellViewPrefab, cellViewParent);
                    cellView.Set(recipeRow);
                    cellView.OnClick
                        .Subscribe(SubscribeOnClickCellView)
                        .AddTo(_disposablesAtLoadRecipeList);
                    cellViews[idx++] = cellView;
                }
                else
                {
                    break;
                }
            }
        }

        public void UpdateRecipes(ItemSubType type)
        {
            var avatarState = States.Instance.CurrentAvatarState;
            if (avatarState is null)
            {
                return;
            }

            var tableSheets = Game.Game.instance.TableSheets;
            var equipmentIds = tableSheets.EquipmentItemSheet.Values
                .Where(r => r.ItemSubType == type)
                .Select(r => r.Id).ToList();
            var rows = tableSheets.EquipmentItemRecipeSheet.Values
                .Where(r => equipmentIds.Contains(r.ResultEquipmentId)).ToList();
            var combination = Widget.Find<Combination>();

            combination.LoadRecipeVFXSkipMap();

            for (var index = 0; index < cellViews.Length; index++)
            {
                var cellView = cellViews[index];
                if (index < rows.Count)
                {
                    cellView.Set(rows[index]);
                    var hasNotification = _notificationId == cellView.EquipmentRowData.Id;
                    var isUnlocked = avatarState.worldInformation
                        .IsStageCleared(cellView.EquipmentRowData.UnlockStage);
                    var isFirstOpen =
                        !combination.RecipeVFXSkipMap
                            .ContainsKey(cellView.EquipmentRowData.Id) && isUnlocked;

                    cellView.Set(avatarState, hasNotification, isFirstOpen);
                    cellView.Show();
                }
                else
                {
                    cellView.Hide();
                }
            }
        }

        private void UpdateRecipes(StatType type)
        {
            var avatarState = States.Instance.CurrentAvatarState;
            if (avatarState is null)
            {
                return;
            }

            var tableSheets = Game.Game.instance.TableSheets;
            var consumableIds = tableSheets.ConsumableItemSheet.Values
                .Where(r => r.Stats.FirstOrDefault()?.StatType == type)
                .Select(r => r.Id).ToList();
            var rows = tableSheets.ConsumableItemRecipeSheet.Values
                .Where(r => consumableIds.Contains(r.ResultConsumableItemId)).ToList();
            var combination = Widget.Find<Combination>();

            combination.LoadRecipeVFXSkipMap();

            for (var index = 0; index < cellViews.Length; index++)
            {
                var cellView = cellViews[index];
                if (index < rows.Count)
                {
                    cellView.Set(rows[index]);
                    cellView.Set(avatarState);
                    cellView.Show();
                }
                else
                {
                    cellView.Hide();
                }
            }
        }

        public bool TryGetCellView(int recipeId, out RecipeCellView cellView)
        {
            cellView = cellViews.FirstOrDefault(item => item.EquipmentRowData.Id == recipeId);
            return !(cellView is null);
        }

        private TabButton GetButton(ItemSubType itemSubType)
        {
            TabButton btn = null;

            switch (itemSubType)
            {
                case ItemSubType.Weapon:
                    btn = weaponTabButton;
                    break;
                case ItemSubType.Armor:
                    btn = armorTabButton;
                    break;
                case ItemSubType.Belt:
                    btn = beltTabButton;
                    break;
                case ItemSubType.Necklace:
                    btn = necklaceTabButton;
                    break;
                case ItemSubType.Ring:
                    btn = ringTabButton;
                    break;
            }

            return btn;
        }

        private TabButton GetButton(StatType statType)
        {
            TabButton btn = null;

            switch (statType)
            {
                case StatType.HP:
                    btn = hpTabButton;
                    break;
                case StatType.ATK:
                    btn = atkTabButton;
                    break;
                case StatType.DEF:
                    btn = defTabButton;
                    break;
                case StatType.CRI:
                    btn = criTabButton;
                    break;
                case StatType.HIT:
                    btn = hitTabButton;
                    break;
            }

            return btn;
        }

        public void SetToggledOnItemType(ItemSubType itemSubType)
        {
            IToggleable toggleable = GetButton(itemSubType);

            _equipmentToggleGroup.SetToggledOn(toggleable);
            SubscribeOnEquipmentToggledOn(toggleable);
        }

        private void SetToggledOnFilterType(StatType statType)
        {
            IToggleable toggleable = GetButton(statType);

            _equipmentToggleGroup.SetToggledOn(toggleable);
            SubscribeOnEquipmentToggledOn(toggleable);
        }


        private void SubScribeFilterType(ItemSubType itemSubType)
        {
            UpdateRecipes(itemSubType);

            scrollRect.normalizedPosition = new Vector2(0.5f, 1.0f);
        }

        public void OnClickCellViewFromTutorial()
        {
            if (cellViews.Length == 0)
            {
                return;
            }

            SubscribeOnClickCellView(cellViews[0]);
        }

        private static void SubscribeOnClickCellView(RecipeCellView cellView)
        {
            var combination = Widget.Find<Combination>();
            if (!combination.CanHandleInputEvent)
            {
                return;
            }
            cellView.scaleTweener.PlayTween();

            if (cellView.tempLocked)
            {
                AudioController.instance.PlaySfx(AudioController.SfxCode.UnlockRecipe);
                var avatarState = Game.Game.instance.States.CurrentAvatarState;

                combination.RecipeVFXSkipMap[cellView.EquipmentRowData.Id]
                    = new int[3] { 0, 0, 0 };
                combination.SaveRecipeVFXSkipMap();

                var centerPos = cellView.GetComponent<RectTransform>()
                    .GetWorldPositionOfCenter();
                VFXController.instance.CreateAndChaseCam<RecipeUnlockVFX>(centerPos);

                cellView.Set(avatarState, null, false);
                return;
            }

            combination.selectedRecipe = cellView;
            combination.State.SetValueAndForceNotify(Combination.StateType.CombinationConfirm);
        }

        private void SubscribeState(State state)
        {
            switch (state)
            {
                case State.Equipment:
                    consumableTabs.gameObject.SetActive(false);
                    equipmentTabs.gameObject.SetActive(true);
                    UpdateRecipes(_itemFilterType.Value);
                    break;
                case State.Consumable:
                    consumableTabs.gameObject.SetActive(true);
                    equipmentTabs.gameObject.SetActive(false);
                    UpdateRecipes(_statFilterType.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private void SubscribeHasNotification(QuestList questList)
        {
            var hasNotification = false;
            weaponTabButton.HasNotification.Value = false;
            armorTabButton.HasNotification.Value = false;
            beltTabButton.HasNotification.Value = false;
            necklaceTabButton.HasNotification.Value = false;
            ringTabButton.HasNotification.Value = false;

            var quest = questList?
                .OfType<CombinationEquipmentQuest>()
                .Where(x => !x.Complete)
                .OrderBy(x => x.StageId)
                .FirstOrDefault();

            if (!(quest is null))
            {
                var tableSheets = Game.Game.instance.TableSheets;
                var row = tableSheets.EquipmentItemRecipeSheet.Values
                    .FirstOrDefault(r => r.Id == quest.RecipeId);
                if (!(row is null))
                {
                    var stageId = row.UnlockStage;
                    if (Game.Game.instance.States.CurrentAvatarState.worldInformation.IsStageCleared(stageId))
                    {
                        var equipRow = tableSheets.EquipmentItemSheet.Values
                            .FirstOrDefault(r => r.Id == row.ResultEquipmentId);

                        if (!(equipRow is null))
                        {
                            hasNotification = true;
                            var button = GetButton(equipRow.ItemSubType);
                            button.HasNotification.Value = true;

                            _notificationId = row.Id;
                        }
                    }
                }
            }

            HasNotification = hasNotification;
        }

        private void SubScribeFilterType(StatType statType)
        {
            UpdateRecipes(statType);

            scrollRect.normalizedPosition = new Vector2(0.5f, 1.0f);
        }

        public void SetState(State state)
        {
            _state.Value = state;
        }

        private void SubscribeOnEquipmentToggledOn(IToggleable toggleable)
        {
            if (_equipmentToggleableMap.TryGetValue(toggleable.Name, out var itemType))
            {
                _itemFilterType.SetValueAndForceNotify(itemType);
            }
        }

        private void SubscribeOnConsumableToggledOn(IToggleable toggleable)
        {
            if (_consumableToggleableMap.TryGetValue(toggleable.Name, out var statType))
            {
                _statFilterType.SetValueAndForceNotify(statType);
            }
        }
    }
}
