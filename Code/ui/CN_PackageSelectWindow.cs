using System;
using System.Collections.Generic;
using System.IO;
using Chinese_Name.utils;
using NeoModLoader.api;
using NeoModLoader.api.attributes;
using NeoModLoader.General.UI.Prefabs;
using NeoModLoader.General.UI.Window;
using UnityEngine;
using UnityEngine.UI;

namespace Chinese_Name.ui;

internal class CN_PackageSelectWindow : AbstractWideWindow<CN_PackageSelectWindow>
{
    private readonly Dictionary<string, WorkPackageItem>    _shown_working_packages = new();
    private readonly Dictionary<string, WebPackageItem> _shown_web_packages = new();
    private ObjectPoolGenericMono<WorkPackageItem> _work_package_pool;
    private ObjectPoolGenericMono<WebPackageItem> _web_package_pool;
    private RectTransform WebPackageContentTransform;
    private RectTransform WorkPackageContentTransform;
    private CN_PackageMeta _selected_package;

    private SimpleButton to_work_button;
    private SimpleButton to_web_button;
    private SimpleButton load_earlier_button;
    private SimpleButton load_later_button;
    private SimpleText package_info;
    private PackageDownloadProgress download_progress;

    private const string MetaSource = "https://gitee.com/inmny/wb-cn-library/raw/master/index.json";
    protected override void Init()
    {
        var web_package_view = BackgroundTransform.Find("Scroll View").gameObject;
        web_package_view.name = "Web Package Scroll View";
        var rect_transform = web_package_view.GetComponent<RectTransform>();
        rect_transform.sizeDelta = new Vector2(108, 255);
        rect_transform.localPosition = new Vector3(-232, 0, 0);
        rect_transform.localScale = Vector3.one;
        var scroll_rect = web_package_view.GetComponent<ScrollRect>();
        scroll_rect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll_rect.verticalScrollbar.GetComponent<RectTransform>().sizeDelta = new Vector2(10, 0);
        var scroll_area_bg = web_package_view.GetComponent<Image>();
        scroll_area_bg.sprite = SpriteTextureLoader.getSprite("ui/special/windowEmptyFrame");
        scroll_area_bg.type = Image.Type.Sliced;
        scroll_area_bg.color = Color.white;

        WebPackageContentTransform = web_package_view.transform.Find("Viewport/Content").GetComponent<RectTransform>();
        var vert_layout = WebPackageContentTransform.gameObject.AddComponent<VerticalLayoutGroup>();
        vert_layout.childControlHeight = false;
        vert_layout.childControlWidth = false;
        vert_layout.childForceExpandHeight = false;
        vert_layout.childForceExpandWidth = false;
        vert_layout.childAlignment = TextAnchor.UpperCenter;
        vert_layout.spacing = 4;
        vert_layout.padding = new RectOffset(0, 0, 12, 12);

        var fitter = WebPackageContentTransform.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var fetch_button = Instantiate(NeoModLoader.General.UI.Prefabs.SimpleButton.Prefab, BackgroundTransform);
        fetch_button.Setup(FetchMetas, SpriteTextureLoader.getSprite("ui/icons/iconDamage"));
        fetch_button.transform.localPosition = new Vector3(-232, -140, 0);
        fetch_button.transform.localScale = Vector3.one;

        var work_package_view = Instantiate(web_package_view, BackgroundTransform);
        work_package_view.name = "Work Package Scroll View";
        rect_transform = work_package_view.GetComponent<RectTransform>();
        rect_transform.sizeDelta = new Vector2(108, 255);
        rect_transform.localPosition = new Vector3(232, 0, 0);
        rect_transform.localScale = Vector3.one;


        WorkPackageContentTransform = work_package_view.transform.Find("Viewport/Content").GetComponent<RectTransform>();

        GameObject control_part = new GameObject("Bottom Control", typeof(Image), typeof(HorizontalLayoutGroup));
        control_part.transform.SetParent(BackgroundTransform);
        rect_transform = control_part.GetComponent<RectTransform>();
        rect_transform.sizeDelta = new Vector2(300, 48);
        rect_transform.localPosition = new Vector3(0, -100, 0);
        rect_transform.localScale = Vector3.one;
        scroll_area_bg = control_part.GetComponent<Image>();
        scroll_area_bg.sprite = SpriteTextureLoader.getSprite("ui/special/button");
        scroll_area_bg.type = Image.Type.Sliced;
        scroll_area_bg.color = Color.white;
        var hori_layout = control_part.GetComponent<HorizontalLayoutGroup>();
        hori_layout.childControlHeight = false;
        hori_layout.childControlWidth = false;
        hori_layout.childForceExpandHeight = false;
        hori_layout.childForceExpandWidth = false;
        hori_layout.childAlignment = TextAnchor.MiddleLeft;
        hori_layout.spacing = 4;
        hori_layout.padding = new RectOffset(0, 0, 12, 12);

        to_work_button = Instantiate(SimpleButton.Prefab, rect_transform);
        to_work_button.Setup(() => ToWork(_selected_package), null, pText: "右");
        to_web_button = Instantiate(SimpleButton.Prefab, rect_transform);
        to_web_button.Setup(() => ToWeb(_selected_package), null, pText: "左");
        load_earlier_button = Instantiate(SimpleButton.Prefab, rect_transform);
        load_earlier_button.Setup(() => LoadEarlier(_selected_package), null, pText: "上");
        load_later_button = Instantiate(SimpleButton.Prefab, rect_transform);
        load_later_button.Setup(() => LoadLater(_selected_package), null, pText: "下");


        package_info = Instantiate(SimpleText.Prefab, BackgroundTransform);
        package_info.Setup("", TextAnchor.UpperLeft, new(300, 160));
        package_info.transform.localPosition = new(0, 40);
        package_info.transform.localScale = Vector3.one;

        download_progress = Instantiate(PackageDownloadProgress.Prefab, BackgroundTransform);
        download_progress.SetSize(new Vector2(300, 20));
        download_progress.transform.localScale = Vector3.one;
        download_progress.transform.localPosition = new Vector3(0, -60);
        download_progress.gameObject.SetActive(false);


        _web_package_pool = new ObjectPoolGenericMono<WebPackageItem>(WebPackageItem.Prefab, WebPackageContentTransform);
        _work_package_pool = new ObjectPoolGenericMono<WorkPackageItem>(WorkPackageItem.Prefab, WorkPackageContentTransform);
    }

    public override void OnNormalEnable()
    {
        SelectPackage(null);
    }
    private void OnDisable()
    {
        if (Initialized && CN_PackageManager.IsDirty)
        {
            CN_PackageManager.UnloadAllPackages();
            CN_PackageManager.LoadAllPackages();
        }
    }

    struct WebPackageBaseMeta
    {
        public string repo;
        public string branch;
        public string path;
    }
    [Hotfixable]
    internal async void FetchMetas()
    {
        _web_package_pool.clear();
        var raw_list = await DownloadUtils.GetHttpContent(MetaSource);
        var meta_list = GeneralUtils.DeserializeFromJson<List<WebPackageBaseMeta>>(raw_list);

        foreach (var base_meta in meta_list)
        {
            var meta_url = GeneralUtils.CombinePath($"https://gitee.com/{base_meta.repo}/raw/{base_meta.branch}", base_meta.path, "meta.json").Replace("\\", "/");
            var raw_meta = await DownloadUtils.GetHttpContent(meta_url);
            var read_meta = GeneralUtils.DeserializeFromJson<CN_PackageMeta>(raw_meta);
            var meta = new CN_PackageMeta(meta_url);
            meta.BaseInfoFromAnother(read_meta);

            var item = _web_package_pool.getNext(0);
            item.Setup(meta);
        }
    }
    private void UpdateButtons()
    {
        if (_selected_package == null)
        {
            to_work_button.gameObject.SetActive(false);
            to_web_button.gameObject.SetActive(false);
            load_earlier_button.gameObject.SetActive(false);
            load_later_button.gameObject.SetActive(false);
            return;
        }
        bool working = _shown_working_packages.ContainsKey(_selected_package.UID);
        to_work_button.gameObject.SetActive(!working);
        to_web_button.gameObject.SetActive(working);

        load_earlier_button.gameObject.SetActive(!CN_PackageManager.IsEarliestLoadedPackage(_selected_package));
        load_later_button.gameObject.SetActive(!CN_PackageManager.IsLatestLoadedPackage(_selected_package));
    }
    private void UpdateLayout()
    {
        UpdateButtons();

        _shown_working_packages.Clear();
        _work_package_pool.clear();
        int idx = 0;
        foreach(var package in CN_PackageManager.packages_to_load)
        {
            var item = _work_package_pool.getNext(idx++);
            item.Setup(package);
            _shown_working_packages[package.UID] = item;
        }
    }
    internal void LoadEarlier(CN_PackageMeta meta)
    {
        CN_PackageManager.LiftDown(meta);

        UpdateLayout();
    }
    internal void LoadLater(CN_PackageMeta meta)
    {
        CN_PackageManager.LiftDown(meta);

        UpdateLayout();
    }
    internal void ToWeb(CN_PackageMeta meta)
    {
        CN_PackageManager.RemovePackageToLoad(meta);

        UpdateLayout();
    }
    [Hotfixable]
    internal async void ToWork(CN_PackageMeta meta)
    {
        if (_shown_working_packages.ContainsKey(meta.UID)) return;

        await meta.DownloadAsync(download_progress);
        CN_PackageManager.AddPackageToLoad(meta);

        UpdateLayout();
    }
    internal void SelectPackage(CN_PackageMeta meta)
    {
        _selected_package = meta;
        var non_select_bg = "ui/special/button";
        var select_bg = "ui/special/button2";
        foreach (var item in _shown_web_packages.Values)
        {
            item.BG.sprite = SpriteTextureLoader.getSprite(item.Meta == meta ? select_bg : non_select_bg);
        }
        foreach(var item in _shown_working_packages.Values)
        {
            item.BG.sprite = SpriteTextureLoader.getSprite(item.Meta == meta ? select_bg : non_select_bg);
        }
        DisplaySelectPackage();
        UpdateLayout();
    }
    private void DisplaySelectPackage()
    {
        if (_selected_package == null)
        {
            package_info.text.text = "";
            return;
        }
        package_info.text.text = $"""
            包名: {_selected_package.name}
            作者: {_selected_package.author}
            版本: {_selected_package.version}

            {_selected_package.description}
            """;
    }
}