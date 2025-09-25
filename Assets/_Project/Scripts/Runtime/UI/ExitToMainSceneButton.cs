using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class ExitToMainSceneButton : MonoBehaviour
{
    [SerializeField] private Button _exitButton;
    [SerializeField] private string _mainSceneKey = "scenes/main";

    private AddressablesSceneLoader _sceneLoader;
    private AddressablesAssetLoader _assetLoader;

    [Inject]
    private void Construct(AddressablesSceneLoader sceneLoader, AddressablesAssetLoader assetLoader)
    {
        _sceneLoader = sceneLoader;
        _assetLoader = assetLoader;
    }

    private void OnEnable()
    {
        if (_exitButton != null)
            _exitButton.onClick.AddListener(ExitToMainScene);
    }

    private void OnDisable()
    {
        if (_exitButton != null)
            _exitButton.onClick.RemoveAllListeners();
    }

    private async void ExitToMainScene()
    {
        if (string.IsNullOrWhiteSpace(_mainSceneKey))
        {
            Debug.LogError("[ExitToMain] Main scene key is empty.");
            return;
        }

        try
        {
            Debug.Log("[ExitToMain] Unloading assets…");
            await _assetLoader.UnloadAllAssetsAsync();

            Debug.Log($"[ExitToMain] Loading '{_mainSceneKey}'…");
            await _sceneLoader.LoadSceneAsync(_mainSceneKey, UnityEngine.SceneManagement.LoadSceneMode.Single);

            Debug.Log("[ExitToMain] Done.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ExitToMain] Failed: {e.Message}");
        }
    }
}