using UnityEngine;
using UnityEngine.UI;

namespace ChessDeck
{
    public class MainMenuController : MonoBehaviour
    {
        public Button playButton;

        void OnEnable() => UpdatePlayButton();

        void UpdatePlayButton()
        {
            bool ready = PlayerLoadout.I != null && PlayerLoadout.I.IsReady();
            if (playButton) playButton.interactable = ready;
        }
    }
}
