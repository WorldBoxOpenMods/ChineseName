using Chinese_Name.utils;
using NeoModLoader.General;
using NeoModLoader.General.UI.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Chinese_Name.ui
{
    internal class PackageDownloadProgress : APrefab<PackageDownloadProgress>, IDoubleProgress<DownloadUtils.MultiDownloadProgress, DownloadUtils.SingleDownloadProgress>
    {
        private static void _init()
        {
            GameObject obj = new GameObject(nameof(PackageDownloadProgress), typeof(Image), typeof(Mask));
            obj.transform.SetParent(ModClass.Instance.transform);
            var bg = obj.GetComponent<Image>();
            bg.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
            bg.type = Image.Type.Sliced;
            bg.color = Color.gray;

            var bar = new GameObject("Bar", typeof(Image)).GetComponent<Image>();
            bar.transform.SetParent(obj.transform);
            bar.color = Color.green;
            bar.GetComponent<RectTransform>().SetPivot(PivotPresets.MiddleLeft);

            var text = new GameObject("Text", typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(obj.transform);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 0;
            text.resizeTextMaxSize = 10;
            text.color = Color.white;
            text.font = LocalizedTextManager.currentFont;
            text.alignment = TextAnchor.MiddleCenter;

            Prefab = obj.AddComponent<PackageDownloadProgress>();
        }
        protected override void Init()
        {
            if (Initialized) return;
            base.Init();
            BG = GetComponent<Image>();
            Bar = transform.Find("Bar").GetComponent<RectTransform>();
            Progress = transform.Find("Text").GetComponent<Text>();
            RectTransform = GetComponent<RectTransform>();
        }
        public override void SetSize(Vector2 pSize)
        {
            Init();
            base.SetSize(pSize);
            Bar.sizeDelta = pSize;
            Bar.localPosition = new(-pSize.x / 2, 0);
            Progress.GetComponent<RectTransform>().sizeDelta = pSize;
        }
        public Image BG { get; private set; }
        public RectTransform Bar { get; private set; }
        public RectTransform RectTransform { get; private set; }
        public Text Progress { get; private set; }
        private DownloadUtils.MultiDownloadProgress TopProgress;
        private DownloadUtils.SingleDownloadProgress BottomProgress;
        public void Report(DownloadUtils.MultiDownloadProgress top)
        {
            TopProgress = top;
            BottomProgress.BytesReceived = 0;
            BottomProgress.TotalBytesToReceive = 0;
            UpdateInfo();
        }

        public void Report(DownloadUtils.SingleDownloadProgress value)
        {
            BottomProgress = value;
            UpdateInfo();
        }
        public void Report(string tip)
        {
            var progress = 0;
            Bar.sizeDelta = new(RectTransform.sizeDelta.x * progress, RectTransform.sizeDelta.y);
            Progress.text = tip;

            gameObject.SetActive(true);
        }
        public void UpdateInfo()
        {
            var progress = ((float)(TopProgress.BytesReceived + BottomProgress.BytesReceived)) / (TopProgress.TotalBytesToReceive ?? (TopProgress.BytesReceived + BottomProgress.BytesReceived));
            Bar.sizeDelta = new(RectTransform.sizeDelta.x * progress, RectTransform.sizeDelta.y);
            Progress.text = $"{BottomProgress.BytesReceived}/{BottomProgress.TotalBytesToReceive}; {TopProgress.BytesReceived + BottomProgress.BytesReceived} / {TopProgress.TotalBytesToReceive}; {TopProgress.CurrentFileIndex} / {TopProgress.TotalFiles}";

            if (TopProgress.CurrentFileIndex >= TopProgress.TotalFiles)
            {
                gameObject.SetActive(false);
            }
            else if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }
    }
}
