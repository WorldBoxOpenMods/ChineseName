using NeoModLoader.General.UI.Prefabs;
using UnityEngine;
using UnityEngine.UI;

namespace Chinese_Name.ui;

internal class PackageSelectItem : APrefab<PackageSelectItem>
{
    private SwitchButton _selectButton;

    private Text _text;

    protected override void Init()
    {
        if (Initialized) return;

        _selectButton = transform.Find("Toggle").GetComponent<SwitchButton>();
        _text = transform.Find("Text").GetComponent<Text>();

        base.Init();
    }

    public void Setup(CN_PackageAsset pPackageAsset, bool pSelected)
    {
        Init();
        _text.text = pPackageAsset.name;
        _selectButton.Setup(pSelected, () => CN_PackageSelectWindow.Instance.SelectPackage(pPackageAsset.id));
    }

    public void SetButtonState(bool pState)
    {
        //_selectButton.
    }

    private static void _init()
    {
        var obj = new GameObject("PackageSelectItem", typeof(Image));
        obj.transform.SetParent(ModClass.Instance.transform);
        var bg = obj.GetComponent<Image>();
        bg.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        bg.type = Image.Type.Sliced;

        var rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(190, 50);

        var text_obj = new GameObject("Text", typeof(Text));
        text_obj.transform.SetParent(obj.transform);
        var text = text_obj.GetComponent<Text>();
        text.text = "默认";
        text.alignment = TextAnchor.MiddleCenter;
        text.font = LocalizedTextManager.currentFont;
        text.resizeTextForBestFit = true;
        text.resizeTextMaxSize = 10;
        text.resizeTextMinSize = 4;

        var text_rt = text_obj.GetComponent<RectTransform>();
        text_rt.sizeDelta = new Vector2(120, 50);

        SwitchButton select_button = Instantiate(SwitchButton.Prefab, obj.transform);
        select_button.name = "Toggle";
        select_button.SetSize(new Vector2(48, 48));
        select_button.transform.localPosition = new Vector3(90, 0);

        Prefab = obj.AddComponent<PackageSelectItem>();
    }
}