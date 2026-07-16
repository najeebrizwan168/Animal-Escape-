using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class MainmenuManager : MonoBehaviour
{
    [Header("Mainmenu")]
    public Button playButton;

    public void play(){
        gameObject.SetActive(false);
    }

}