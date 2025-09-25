using System;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoadingFlow
{
    private const float PROGRESS_COMPLETE_THRESHOLD = 0.999f;
    private const float PROGRESS_COMPLETE_VALUE = 1f;

    private readonly AddressablesSceneLoader sceneLoader;
    private readonly Slider progressSlider;

    public SceneLoadingFlow(AddressablesSceneLoader sceneLoader, Slider progressSlider)
    {
        this.sceneLoader = sceneLoader;
        this.progressSlider = progressSlider;
    }

    public async Task<bool> IsSceneKeyAsync(object keyOrLabel)
    {
        await Addressables.InitializeAsync().Task;
        
        var handle = Addressables.LoadResourceLocationsAsync(keyOrLabel, typeof(SceneInstance));
        var locations = await handle.Task;
        
        Addressables.Release(handle);
        
        return locations != null && locations.Count > 0;
    }

    public async Task LoadSceneByKeyAsync(string sceneKey)
    {
        if (progressSlider != null)
        {
            progressSlider.gameObject.SetActive(true);
            progressSlider.value = 0f;
        }

        var progressTask = TrackSceneProgressAsync();

        try
        {
            await sceneLoader.LoadSceneAsync(sceneKey, LoadSceneMode.Single);
        }
        catch (Exception _)
        {
            if (progressSlider != null)
                progressSlider.value = 0f;
            
            return;
        }

        await progressTask;
    }

    private async Task TrackSceneProgressAsync()
    {
        if (progressSlider == null)
            return;

        while (sceneLoader.CurrentSceneLoadingProgress < PROGRESS_COMPLETE_THRESHOLD)
        {
            progressSlider.value = sceneLoader.CurrentSceneLoadingProgress;
            await Task.Yield();
        }

        progressSlider.value = PROGRESS_COMPLETE_VALUE;
    }
}