using System.Collections.Generic;
using NeoModLoader.General.UI.Window;

namespace Chinese_Name.ui;

internal class CN_PackageSelectWindow : AutoLayoutWindow<CN_PackageSelectWindow>
{
    private readonly Dictionary<string, PackageSelectItem>    _shown_packages = new();
    private          ObjectPoolGenericMono<PackageSelectItem> _pool;
    public static    CN_PackageSelectWindow                   Instance { get; private set; }

    protected override void Init()
    {
        _pool = new ObjectPoolGenericMono<PackageSelectItem>(PackageSelectItem.Prefab, ContentTransform);
        Instance = this;
    }

    public override void OnNormalEnable()
    {
        if (!CN_PackageLibrary.Instance.dirty) return;
        CN_PackageLibrary.Instance.dirty = false;

        foreach (CN_PackageAsset package in CN_PackageLibrary.Instance.dict.Values)
        {
            if (_shown_packages.ContainsKey(package.id)) continue;
            PackageSelectItem item = _pool.getNext();
            item.Setup(package, package == CN_PackageLibrary.CurrentPackage);

            _shown_packages.Add(package.id, item);
        }
    }

    internal void SelectPackage(string pID)
    {
        CN_PackageLibrary.Instance.SwitchPackage(pID);
        foreach (var item in _shown_packages) item.Value.SetButtonState(item.Key == pID);
    }
}