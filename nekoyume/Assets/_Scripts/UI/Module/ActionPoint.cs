using System;
using System.Collections.Generic;
using Nekoyume.State;
using Nekoyume.State.Subjects;
using Nekoyume.UI.Module.Common;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nekoyume.UI.Module
{
    public class ActionPoint : AlphaAnimateModule
    {
        [SerializeField]
        private SliderAnimator sliderAnimator = null;

        [SerializeField]
        private TextMeshProUGUI text = null;

        [SerializeField]
        private Image image = null;

        [SerializeField]
        private RectTransform tooltipArea = null;

        [SerializeField]
        private bool syncWithAvatarState = true;

        [SerializeField]
        private EventTrigger eventTrigger = null;

        [SerializeField]
        private GameObject loading;

        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private int _currentActionPoint;

        public bool IsRemained => _currentActionPoint > 0;

        public Image Image => image;

        #region Mono

        private void Awake()
        {
            sliderAnimator.OnSliderChange
                .Subscribe(_ => OnSliderChange())
                .AddTo(gameObject);
            sliderAnimator.SetMaxValue(States.Instance.GameConfigState.ActionPointMax);
            sliderAnimator.SetValue(0f, false);

            GameConfigStateSubject.GameConfigState
                .Subscribe(state => sliderAnimator.SetMaxValue(state.ActionPointMax))
                .AddTo(gameObject);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!syncWithAvatarState)
                return;

            if (!(States.Instance.CurrentAvatarState is null))
            {
                SetActionPoint(States.Instance.CurrentAvatarState.actionPoint, false);
            }

            ReactiveAvatarState.ActionPoint
                .Subscribe(x => SetActionPoint(x, true))
                .AddTo(_disposables);

            OnSliderChange();
        }

        protected override void OnDisable()
        {
            sliderAnimator.Stop();
            _disposables.DisposeAllAndClear();
            base.OnDisable();
        }

        #endregion

        private void SetActionPoint(int actionPoint, bool useAnimation)
        {
            if (_currentActionPoint == actionPoint)
            {
                return;
            }

            _currentActionPoint = actionPoint;
            sliderAnimator.SetValue(_currentActionPoint, useAnimation);
        }

        private void OnSliderChange()
        {
            text.text = $"{(int) sliderAnimator.Value} / {(int) sliderAnimator.MaxValue}";
        }

        public void ShowTooltip()
        {
            Widget.Find<VanilaTooltip>()
                .Show("UI_BLESS_OF_GODDESS", "UI_BLESS_OF_GODDESS_DESCRIPTION", tooltipArea.position);
        }

        public void HideTooltip()
        {
            Widget.Find<VanilaTooltip>().Close();
        }

        public void SetActionPoint(int actionPoint)
        {
            SetActionPoint(actionPoint, false);
        }

        public void SetEventTriggerEnabled(bool value)
        {
            eventTrigger.enabled = value;
        }

        public void SetActiveLoading(bool value)
        {
            loading.SetActive(value);
            text.enabled = !value;
        }
    }
}
