using System;
using Assets.SimpleLocalization;
using UniRx;

namespace Nekoyume.UI.Model
{
    public class ItemInformationTooltip : Tooltip
    {
        public readonly ItemInformation itemInformation;

        public readonly ReactiveProperty<string> titleText = new ReactiveProperty<string>();
        public readonly ReactiveProperty<bool> priceEnabled = new ReactiveProperty<bool>(false);
        public readonly ReactiveProperty<decimal> price = new ReactiveProperty<decimal>(0m);

        public readonly ReactiveProperty<string> closeButtonText =
            new ReactiveProperty<string>(LocalizationManager.Localize("UI_CLOSE"));

        public readonly ReactiveProperty<Func<CountableItem, bool>> submitButtonEnabledFunc =
            new ReactiveProperty<Func<CountableItem, bool>>();

        public readonly ReactiveProperty<bool> submitButtonEnabled = new ReactiveProperty<bool>(false);
        public readonly ReactiveProperty<string> submitButtonText = new ReactiveProperty<string>(null);

        public readonly Subject<UI.ItemInformationTooltip> onSubmit = new Subject<UI.ItemInformationTooltip>();
        public readonly Subject<UI.ItemInformationTooltip> onClose = new Subject<UI.ItemInformationTooltip>();

        public ItemInformationTooltip(CountableItem countableItem = null)
        {
            itemInformation = new ItemInformation(countableItem);
            itemInformation.item.Subscribe(item =>
            {
                if (item is null)
                {
                    titleText.Value = "";

                    return;
                }

                titleText.Value = item.item.Value.Data.LocalizedName;

                if (!(item is ShopItem shopItem))
                {
                    priceEnabled.Value = false;

                    return;
                }

                priceEnabled.Value = true;
                price.Value = shopItem.price.Value;
            });

            submitButtonEnabledFunc.Value = SubmitButtonEnabledFunc;
            submitButtonEnabledFunc.Subscribe(func =>
            {
                if (func == null)
                {
                    submitButtonEnabledFunc.Value = SubmitButtonEnabledFunc;
                }

                submitButtonEnabled.Value = submitButtonEnabledFunc.Value(itemInformation.item.Value);
            });
        }

        public override void Dispose()
        {
            titleText.Dispose();
            closeButtonText.Dispose();
            submitButtonText.Dispose();

            onSubmit.Dispose();
            onClose.Dispose();

            base.Dispose();
        }

        private static bool SubmitButtonEnabledFunc(CountableItem model)
        {
            return false;
        }
    }
}
