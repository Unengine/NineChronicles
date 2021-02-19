using System;
using Nekoyume.Game.Controller;
using Nekoyume.Game.Item;
using Nekoyume.Game.VFX;
using Nekoyume.Model;
using Nekoyume.State;
using Nekoyume.UI.Module;
using UniRx;
using UnityEngine;
namespace Nekoyume.UI
{
    public class Battle : Widget, IToggleListener
    {
        [SerializeField]
        private StageTitle stageTitle = null;

        [SerializeField]
        private GuidedQuest guidedQuest = null;

        [SerializeField]
        private BossStatus bossStatus = null;

        [SerializeField]
        private ToggleableButton repeatButton = null;

        [SerializeField]
        private HelpButton helpButton = null;

        [SerializeField]
        private BossStatus enemyPlayerStatus = null;

        [SerializeField]
        private StageProgressBar stageProgressBar = null;

        [SerializeField]
        private ComboText comboText = null;

        public BossStatus BossStatus => bossStatus;

        public ToggleableButton RepeatButton => repeatButton;

        public HelpButton HelpButton => helpButton;

        public BossStatus EnemyPlayerStatus => enemyPlayerStatus;

        public StageProgressBar StageProgressBar => stageProgressBar;

        public ComboText ComboText => comboText;

        public const int RequiredStageForExitButton = 3;

        protected override void Awake()
        {
            base.Awake();
            repeatButton.SetToggleListener(this);
            Game.Event.OnGetItem.AddListener(OnGetItem);

            CloseWidget = null;
        }

        public void ShowInArena(bool ignoreShowAnimation = false)
        {
            stageTitle.Close();
            comboText.Close();
            stageProgressBar.Close();
            guidedQuest.Hide(true);
            repeatButton.gameObject.SetActive(false);
            helpButton.gameObject.SetActive(false);
            base.Show(ignoreShowAnimation);
        }

        public void Show(int stageId, bool isRepeat, bool isExitReserved)
        {
            guidedQuest.Hide(true);
            base.Show();
            stageTitle.Show(stageId);
            guidedQuest.Show(States.Instance.CurrentAvatarState, () =>
            {
                guidedQuest.SetWorldQuestToInProgress(stageId);
            });
            stageProgressBar.Show();
            bossStatus.Close();
            enemyPlayerStatus.Close();
            comboText.Close();

            if (isRepeat)
            {
                repeatButton.SetToggledOn();
            }
            else
            {
                repeatButton.SetToggledOff();
            }

            if (States.Instance.CurrentAvatarState.worldInformation.TryGetUnlockedWorldByStageClearedBlockIndex(
                out var world) &&
                world.StageClearedId >= GameConfig.RequireClearedStageLevel.UIBottomMenuInBattle)
            {
                ShowBottomMenu(world);
                WidgetHandler.Instance.BottomMenu.exitButton.SharedModel.IsEnabled.Value = isExitReserved;
            }
            repeatButton.gameObject.SetActive(stageId >= 4 || world.StageClearedId >= 4);
            helpButton.gameObject.SetActive(true);
        }

        public override void Close(bool ignoreCloseAnimation = false)
        {
            guidedQuest.Hide(ignoreCloseAnimation);
            Find<BottomMenu>().Close(ignoreCloseAnimation);
            enemyPlayerStatus.Close(ignoreCloseAnimation);
            base.Close(ignoreCloseAnimation);
        }

        public void ShowBottomMenu(WorldInformation.World world, bool isInteractableExitButton = true)
        {
            var showExitButton = world.StageClearedId >= RequiredStageForExitButton;

            var bottomMenu = WidgetHandler.Instance.BottomMenu;
            if (!bottomMenu.isActiveAndEnabled)
            {
                WidgetHandler.Instance.BottomMenu.Show(
                    showExitButton ?
                        UINavigator.NavigationType.Exit :
                        UINavigator.NavigationType.None,
                    SubscribeOnExitButtonClick,
                    false,
                    BottomMenu.ToggleableType.Mail,
                    BottomMenu.ToggleableType.Quest,
                    BottomMenu.ToggleableType.Chat,
                    BottomMenu.ToggleableType.IllustratedBook,
                    BottomMenu.ToggleableType.Character,
                    BottomMenu.ToggleableType.Combination,
                    BottomMenu.ToggleableType.Settings);
            }

            WidgetHandler.Instance.BottomMenu.exitButton.SetToggleListener(this);
            WidgetHandler.Instance.BottomMenu.exitButton.SetInteractable(isInteractableExitButton);
        }

        public void ClearStage(int stageId, System.Action<bool> onComplete)
        {
            guidedQuest.ClearWorldQuest(stageId, cleared =>
            {
                if (!cleared)
                {
                    onComplete(false);
                    return;
                }

                guidedQuest.UpdateList(
                    States.Instance.CurrentAvatarState,
                    () => onComplete(true));
            });
        }

        private void SubscribeOnExitButtonClick(BottomMenu bottomMenu)
        {
            if (!CanClose)
            {
                return;
            }

            var stage = Game.Game.instance.Stage;
            if (stage.isExitReserved)
            {
                stage.isExitReserved = false;
                bottomMenu.exitButton.Toggleable = false;
                bottomMenu.exitButton.IsWidgetControllable = false;
                bottomMenu.exitButton.SharedModel.IsEnabled.Value = false;
                bottomMenu.exitButton.SetToggledOff();
            }
            else
            {
                bottomMenu.exitButton.Toggleable = true;
                bottomMenu.exitButton.IsWidgetControllable = true;

                var confirm = Find<Confirm>();
                confirm.Show("UI_BATTLE_EXIT_RESERVATION_TITLE", "UI_BATTLE_EXIT_RESERVATION_CONTENT");
                confirm.CloseCallback = result =>
                {
                    if (result == ConfirmResult.Yes)
                    {
                        stage.isExitReserved = true;
                        bottomMenu.exitButton.SharedModel.IsEnabled.Value = true;
                        repeatButton.SetToggledOff();
                    }
                };
            }
        }

        private static void SetExitButtonToggledOff()
        {
            Game.Game.instance.Stage.isExitReserved = false;
            Find<BottomMenu>().exitButton.SharedModel.IsEnabled.Value = false;
        }

        public void ShowComboText(bool attacked)
        {
            comboText.StopAllCoroutines();
            comboText.Show(attacked);
        }

        private void OnGetItem(DropItem dropItem)
        {
            var bottomMenu = Find<BottomMenu>();
            if (!bottomMenu)
            {
                throw new WidgetNotFoundException<BottomMenu>();
            }
            VFXController.instance.CreateAndChase<DropItemInventoryVFX>(bottomMenu.characterButton.transform, Vector3.zero);
        }

        protected override void OnCompleteOfCloseAnimationInternal()
        {
            base.OnCompleteOfCloseAnimationInternal();
            stageTitle.Close();
            stageProgressBar.Close();
        }

        #region IToggleListener for repeatButton.

        public void OnToggle(IToggleable toggleable)
        {
            if (toggleable.IsToggledOn)
            {
                RequestToggledOff(toggleable);
            }
            else
            {
                RequestToggledOn(toggleable);
            }
        }

        public void RequestToggledOff(IToggleable toggleable)
        {
            toggleable.SetToggledOff();
            if ((ToggleableButton) toggleable == repeatButton)
            {
                Game.Game.instance.Stage.repeatStage = false;
            }
        }

        public void RequestToggledOn(IToggleable toggleable)
        {
            toggleable.SetToggledOn();
            if ((ToggleableButton)toggleable == repeatButton)
            {
                Game.Game.instance.Stage.repeatStage = true;
                SetExitButtonToggledOff();
            }
        }

        #endregion

        #region tutorial

        public void ShowForTutorial()
        {
            stageTitle.gameObject.SetActive(false);
            guidedQuest.gameObject.SetActive(false);
            bossStatus.gameObject.SetActive(false);
            repeatButton.gameObject.SetActive(false);
            helpButton.gameObject.SetActive(false);
            bossStatus.gameObject.SetActive(false);
            stageProgressBar.gameObject.SetActive(false);
            comboText.gameObject.SetActive(false);
            enemyPlayerStatus.gameObject.SetActive(false);
            comboText.comboMax = 5;
            gameObject.SetActive(true);
        }
        #endregion
    }
}
