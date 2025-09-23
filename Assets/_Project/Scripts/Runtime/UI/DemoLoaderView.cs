using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class DemoLoaderView : MonoBehaviour
{
    private const long BYTES_IN_KILOBYTE = 1024;
    private const long BYTES_IN_MEGABYTE = BYTES_IN_KILOBYTE * 1024;

    private const float PROGRESS_COMPLETE_THRESHOLD = 0.999f;
    private const float PROGRESS_COMPLETE_VALUE = 1f;
    
    [SerializeField] private InputField _assetKeyInputField;
    [SerializeField] private Button _loadSpriteButton;
    [SerializeField] private Button _loadPrefabButton;
    [SerializeField] private Button _unloadAllButton;
    [SerializeField] private Button _loadSceneButton;
    [SerializeField] private Slider _sceneProgressSlider;
    [SerializeField] private Image _spritePreviewImage;
    [SerializeField] private Transform _spawnRootTransform;
    [SerializeField] private UILogView _uiLogView;

    [Inject] private readonly AddressablesAssetLoader assetLoader;
    [Inject] private readonly AddressablesSceneLoader sceneLoader;

    private GameObject _spawnedPrefabInstance;

    private void OnEnable()
    {
        _loadSpriteButton.onClick.AddListener(HandleLoadSpriteClicked);
        _loadPrefabButton.onClick.AddListener(HandleLoadPrefabClicked);
        _unloadAllButton.onClick.AddListener(HandleUnloadAllClicked);
        _loadSceneButton.onClick.AddListener(HandleLoadSceneClicked);
    }

    private void OnDisable()
    {
        _loadSpriteButton.onClick.RemoveAllListeners();
        _loadPrefabButton.onClick.RemoveAllListeners();
        _unloadAllButton.onClick.RemoveAllListeners();
        _loadSceneButton.onClick.RemoveAllListeners();
    }

    private async void HandleLoadSpriteClicked()
    {
        string assetKey = _assetKeyInputField.text;
        long expectedBytes = await assetLoader.GetExpectedDownloadBytesAsync(assetKey);
        await assetLoader.EnsureDependenciesDownloadedAsync(assetKey);

        var sprite = await assetLoader.LoadAssetAsync<Sprite>(assetKey);
        
        if (_spritePreviewImage != null)
            _spritePreviewImage.sprite = sprite;

        _uiLogView.Append($"Sprite loaded: {assetKey} | Size: {FormatBytes(expectedBytes)}");
    }

    private async void HandleLoadPrefabClicked()
    {
        string assetKey = _assetKeyInputField.text;
        long expectedBytes = await assetLoader.GetExpectedDownloadBytesAsync(assetKey);
        await assetLoader.EnsureDependenciesDownloadedAsync(assetKey);

        var prefab = await assetLoader.LoadAssetAsync<GameObject>(assetKey);

        if (_spawnedPrefabInstance != null)
            Destroy(_spawnedPrefabInstance);

        _spawnedPrefabInstance = Instantiate(prefab, _spawnRootTransform);
        _uiLogView.Append($"Prefab instantiated: {assetKey} | Size: {FormatBytes(expectedBytes)}");
    }

    private async void HandleUnloadAllClicked()
    {
        if (_spawnedPrefabInstance != null)
        {
            Destroy(_spawnedPrefabInstance);
            
            _spawnedPrefabInstance = null;
        }

        await assetLoader.UnloadAllAssetsAsync();

        if (_spritePreviewImage != null)
            _spritePreviewImage.sprite = null;

        _sceneProgressSlider.value = 0f;
        _uiLogView.Append("All assets unloaded and memory freed");
    }

    private async void HandleLoadSceneClicked()
    {
        string sceneKey = _assetKeyInputField.text;

        var progressTask = TrackSceneProgressAsync();
        await sceneLoader.LoadSceneAsync(sceneKey, UnityEngine.SceneManagement.LoadSceneMode.Single);
        await progressTask;

        _uiLogView.Append($"Scene loaded: {sceneKey}");
    }

    private async Task TrackSceneProgressAsync()
    {
        while (sceneLoader.CurrentSceneLoadingProgress < PROGRESS_COMPLETE_THRESHOLD)
        {
            _sceneProgressSlider.value = sceneLoader.CurrentSceneLoadingProgress;
            await Task.Yield();
        }
        
        _sceneProgressSlider.value = PROGRESS_COMPLETE_VALUE;
    }

    private string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "0 B";
        
        if (bytes >= BYTES_IN_MEGABYTE)
            return $"{bytes / (float)BYTES_IN_MEGABYTE:F2} MB";
        
        if (bytes >= BYTES_IN_KILOBYTE)
            return $"{bytes / (float)BYTES_IN_KILOBYTE:F2} KB";
        
        return $"{bytes} B";
    }
}