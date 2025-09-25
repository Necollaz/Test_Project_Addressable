using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class ExitToMainSceneButton : MonoBehaviour
{
    [SerializeField] private Button _exitButton;
    [SerializeField] private string _mainSceneKey = "scenes/Main";

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
        await _assetLoader.UnloadAllAssetsAsync();
        await _sceneLoader.LoadSceneAsync(_mainSceneKey, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}