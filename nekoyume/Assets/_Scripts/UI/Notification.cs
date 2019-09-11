using System;
using System.Collections.Generic;
using DefaultNamespace;
using Nekoyume.Game.Controller;
using Nekoyume.UI.Scroller;
using UniRx;
using UnityEngine;

namespace Nekoyume.UI
{
    /// <summary>
    /// Usage: Just to call the `Notification.Push()` method anywhere.
    /// </summary>
    public class Notification : SystemInfoWidget
    {
        private const float LifeTimeOfEachNotification = 10f;

        private static readonly ReactiveCollection<NotificationCellView.Model> Models =
            new ReactiveCollection<NotificationCellView.Model>();
        
        private static readonly List<Type> WidgetTypesForUX = new List<Type>();
        
        private static int _widgetEnableCount;
        

        public static IReadOnlyCollection<NotificationCellView.Model> SharedModels => Models;

        public NotificationScrollerController scroller;

        public static void Push(string message)
        {
            Push(message, string.Empty, null);
        }

        public static void Push(string message, string submitText, System.Action submitAction)
        {
            if (_widgetEnableCount > 0)
            {
                return;
            }
            
            Models.Add(new NotificationCellView.Model
            {
                message = message,
                submitText = submitText,
                submitAction = submitAction,
                addedAt = DateTime.Now
            });
        }
        
        /// <summary>
        /// This class consider if there is any widget raise `OnEnableSubject` subject in the `WidgetTypesForUX` property.
        /// Widget type can registered once and cannot unregistered.
        /// </summary>
        /// <param name="widget"></param>
        public static void RegisterWidgetTypeForUX<T>() where T : Widget
        {
            var type = typeof(T);
            if (WidgetTypesForUX.Contains(type))
            {
                return;
            }
            
            WidgetTypesForUX.Add(type);
        }

        #region Mono

        protected override void Awake()
        {
            base.Awake();
            OnEnableSubject.Subscribe(SubscribeOnEnable).AddTo(gameObject);
            OnDisableSubject.Subscribe(SubscribeOnDisable).AddTo(gameObject);
            scroller.onRequestToRemoveModelByIndex.Subscribe(SubscribeToRemoveModel).AddTo(gameObject);
            scroller.SetModel(Models);
        }

        private void Update()
        {
            var now = DateTime.Now;
            for (var i = 0; i < Models.Count; i++)
            {
                var model = Models[i];
                var timeSpan = now - model.addedAt;
                if (timeSpan.TotalSeconds < LifeTimeOfEachNotification)
                    continue;

                SubscribeToRemoveModel(i);
                break;
            }
        }
        
        #endregion

        private void SubscribeOnEnable(Widget widget)
        {
            var type = widget.GetType();
            if (!WidgetTypesForUX.Contains(type))
            {
                return;
            }

            _widgetEnableCount++;
            
            if (_widgetEnableCount == 1)
            {
                Models.Clear();
            }
        }
        
        private void SubscribeOnDisable(Widget widget)
        {
            var type = widget.GetType();
            if (!WidgetTypesForUX.Contains(type))
            {
                return;
            }
            
            if (_widgetEnableCount == 0)
            {
                return;
            }
            
            _widgetEnableCount--;
        }
        
        private void SubscribeToRemoveModel(int index)
        {
            if (index >= Models.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            Models.RemoveAt(index);
        }
    }
}