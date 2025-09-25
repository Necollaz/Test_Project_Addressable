using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class AssetLivePreviewer
{
    private const float UI_SPRITE_MAX_SIZE = 250f;

    private readonly AssetTypeProbe typeProbe;
    private readonly PrefabPreviewRenderer prefabPreviewRenderer;
    private readonly PreviewPanelSwitcher panelSwitcher;
    private readonly AddressableKeyNormalizer keyNormalizer;
    private readonly Image spritePreviewImage;

    private AsyncOperationHandle? _currentPreviewHandle;
    private string _currentPreviewKey;

    public AssetLivePreviewer(AssetTypeProbe typeProbe, PrefabPreviewRenderer prefabPreviewRenderer,
        PreviewPanelSwitcher panelSwitcher,
        Image spritePreviewImage, AddressableKeyNormalizer keyNormalizer)
    {
        this.typeProbe = typeProbe;
        this.prefabPreviewRenderer = prefabPreviewRenderer;
        this.panelSwitcher = panelSwitcher;
        this.spritePreviewImage = spritePreviewImage;
        this.keyNormalizer = keyNormalizer;
    }

    public async Task PreviewSelectedAssetAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.Log("[Preview] Empty key → hide all");
            panelSwitcher.TryHideAll();
            ReleasePreviewIfAny();
            return;
        }

        if (string.Equals(_currentPreviewKey, key, System.StringComparison.Ordinal))
        {
            Debug.Log($"[Preview] Same key '{key}' → skip");
            return;
        }

        ReleasePreviewIfAny();

        try
        {
            await Addressables.InitializeAsync().Task;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Preview] Addressables.Initialize failed: {e.Message}");
            panelSwitcher.TryHideAll();
            return;
        }

        var probeKey = keyNormalizer.Normalize(key);
        var (hasSprite, hasPrefab) = await typeProbe.ProbeExactAsync(probeKey);
        Debug.Log($"[Preview] Probe '{key}': sprite={hasSprite}, prefab={hasPrefab}");

        if (hasSprite)
        {
            var handle = Addressables.LoadAssetAsync<Sprite>(key);
            await handle.Task;

            Debug.Log($"[Preview] Load Sprite '{key}' status={handle.Status}");
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _currentPreviewHandle = handle;
                _currentPreviewKey = key;

                if (spritePreviewImage != null)
                {
                    ApplySpritePreviewSizing(key);
                    spritePreviewImage.sprite = handle.Result;
                    if (!key.StartsWith(GroupNameKeys.KEY_GROUP_UI, System.StringComparison.OrdinalIgnoreCase))
                        spritePreviewImage.SetNativeSize();
                }

                panelSwitcher.TryShowSpriteOnly();
            }
            else
            {
                Debug.LogError($"[Preview] Sprite load failed for '{key}': {handle.OperationException?.Message}");
                Addressables.Release(handle);
                panelSwitcher.TryHideAll();
            }

            return;
        }

        if (hasPrefab)
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(key);
            await handle.Task;

            Debug.Log($"[Preview] Load Prefab '{key}' status={handle.Status}");
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _currentPreviewHandle = handle;
                _currentPreviewKey = key;

                prefabPreviewRenderer?.ApplyCameraOverrideForKey(key);
                prefabPreviewRenderer?.TryShowPrefab(handle.Result);
                panelSwitcher.TryShowPrefabOnly();
            }
            else
            {
                Debug.LogError($"[Preview] Prefab load failed for '{key}': {handle.OperationException?.Message}");
                Addressables.Release(handle);
                panelSwitcher.TryHideAll();
            }

            return;
        }

        Debug.LogWarning($"[Preview] Key '{key}' is neither sprite nor prefab → hide all");
        panelSwitcher.TryHideAll();
    }

    public void ReleasePreviewIfAny()
    {
        if (_currentPreviewHandle.HasValue)
        {
            var h = _currentPreviewHandle.Value;

            if (h.IsValid())
                Addressables.Release(h);
        }

        _currentPreviewHandle = null;
        _currentPreviewKey = null;
    }

    private void ApplySpritePreviewSizing(string key)
    {
        if (spritePreviewImage == null)
            return;

        if (key.StartsWith(GroupNameKeys.KEY_GROUP_UI, System.StringComparison.OrdinalIgnoreCase))
        {
            spritePreviewImage.preserveAspect = true;
            RectTransform spriteTransform = spritePreviewImage.rectTransform;
            spriteTransform.sizeDelta = new Vector2(UI_SPRITE_MAX_SIZE, UI_SPRITE_MAX_SIZE);
        }
    }
}