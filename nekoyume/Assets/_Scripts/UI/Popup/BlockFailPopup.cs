using Assets.SimpleLocalization;
using System.Collections;
using UnityEngine;

namespace Nekoyume.UI
{
    public class BlockFailPopup : Alert
    {
        private long _index;

        public void Show(long idx)
        {
            var errorMsg = string.Format(LocalizationManager.Localize("UI_ERROR_FORMAT"),
                LocalizationManager.Localize("BLOCK_DOWNLOAD"));

            base.Show(LocalizationManager.Localize("UI_ERROR"), errorMsg,
                LocalizationManager.Localize("UI_OK"), false);
#if UNITY_EDITOR
            CloseCallback = UnityEditor.EditorApplication.ExitPlaymode;
#else
            CloseCallback = Application.Quit;
#endif
            _index = idx;
            StartCoroutine(CoCheckBlockIndex());
        }

        private IEnumerator CoCheckBlockIndex()
        {
            yield return new WaitWhile(() => Game.Game.instance.agent.BlockIndex == _index);
            CloseCallback = null;
            Close();
        }
    }
}