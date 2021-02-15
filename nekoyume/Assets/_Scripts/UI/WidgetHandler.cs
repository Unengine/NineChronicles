using Nekoyume.UI.Module;
using UnityEditor;

namespace Nekoyume.UI
{
    public class WidgetHandler
    {
        private static WidgetHandler _instance;
        public static WidgetHandler Instance => _instance ?? (_instance = new WidgetHandler());

        public bool isActiveTutorialMaskWidget { get; set; }

        private MessageCatManager _messageCatManager;

        public MessageCatManager MessageCatManager =>
            _messageCatManager
                ? _messageCatManager
                : (_messageCatManager = Widget.Find<MessageCatManager>());

        private BottomMenu _bottomMenu;

        public BottomMenu BottomMenu =>
            _bottomMenu ? _bottomMenu : (_bottomMenu = Widget.Find<BottomMenu>());


        public void HideAllMessageCat()
        {
            MessageCatManager?.HideAll(false);
        }
    }
}
