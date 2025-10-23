using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessDeck
{
    public class SceneController : MonoBehaviour
    {
        public void GoToMainMenu()    => SceneManager.LoadScene("MainMenu");
        public void GoToDeckBuilder() => SceneManager.LoadScene("DeckBuilder");
        public void GoToGame()        => SceneManager.LoadScene("Game");

        public void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; 
#else
            Application.Quit(); 
#endif
        }
    }
}
