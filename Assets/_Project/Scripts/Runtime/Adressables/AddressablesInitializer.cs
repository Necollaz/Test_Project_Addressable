using System.Threading.Tasks;
using UnityEngine.AddressableAssets;

public class AddressablesInitializer
{
    public async Task InitializeAndUpdateCatalogsAsync()
    {
        await Addressables.InitializeAsync().Task;

        var checkHandle = Addressables.CheckForCatalogUpdates(false);
        var catalogs = await checkHandle.Task;
        
        Addressables.Release(checkHandle);

        if (catalogs != null && catalogs.Count > 0)
        {
            var updateHandle = Addressables.UpdateCatalogs(catalogs);
            
            await updateHandle.Task;
            
            Addressables.Release(updateHandle);
        }
    }
}