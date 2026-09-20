using UnityEngine;
using UnityEngine.SceneManagement;

public class menuPrincipal : MonoBehaviour
{
    public void PlayGame()
    {
        if (SceneFader.Instance != null)
            SceneFader.Instance.LoadScene("SampleScene");
        else
            SceneManager.LoadScene("SampleScene"); // respaldo si el SceneFader no está en la escena
    }

    public void QuitGame()
    {
        Application.Quit();
    }

}