﻿using Nekoyume.Game.Controller;

namespace Nekoyume.UI
{
    public class Title : Widget
    {
        public bool ready = false;

        public override void Show()
        {
            base.Show();
            AudioController.instance.PlayMusic(AudioController.MusicCode.Title);
        }

        public void OnClick()
        {
            if (!ready)
                return;

            Find<Synopsis>().Show();
            Close();
        }

        public void Ready()
        {
            ready = true;
        }
    }
}
