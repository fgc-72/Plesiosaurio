using UnityEngine;
using UnityEngine.SceneManagement;

public class menuPrincipal : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadScene("SampleScene");
    }

    public void QuitGame()
    {
        Application.Quit();
    }

}
