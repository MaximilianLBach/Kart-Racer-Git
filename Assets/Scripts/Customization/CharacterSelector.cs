using UnityEngine;
using TMPro;

public class CharacterSelector : MonoBehaviour
{
    [Header("UI Setup")]
    public TextMeshProUGUI characterNameText;
    
    [Header("Available Characters")]
    // Just type the names in the Inspector (e.g., "Red Driver", "Blue Driver")
    public string[] characterNames; 

    private int currentIndex = 0;

    private void Start()
    {
        // Load the saved preference, default to 0 if they've never played before
        currentIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);
        UpdateUI();
    }

    public void NextCharacter()
    {
        if (characterNames.Length == 0) return;
        
        currentIndex++;
        if (currentIndex >= characterNames.Length) currentIndex = 0;
        
        SaveAndDisplay();
    }

    public void PreviousCharacter()
    {
        if (characterNames.Length == 0) return;
        
        currentIndex--;
        if (currentIndex < 0) currentIndex = characterNames.Length - 1;
        
        SaveAndDisplay();
    }

    private void SaveAndDisplay()
    {
        // Save to the hard drive instantly
        PlayerPrefs.SetInt("SelectedCharacter", currentIndex);
        PlayerPrefs.Save();
        
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (characterNameText != null && characterNames.Length > 0)
        {
            characterNameText.text = characterNames[currentIndex];
        }
    }
}