using Zenject;

public class AddressablesInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<AddressablesInitializer>().AsSingle();
        Container.Bind<AddressablesDiagnostics>().AsSingle();
        Container.Bind<AddressablesAssetLoader>().AsSingle();
        Container.Bind<AddressablesSceneLoader>().AsSingle();
    }
}
