using NeoModLoader.api.attributes;
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
    internal class WorkPackageItem : APrefab<WorkPackageItem>
    {
        private static void _init()
        {
            GameObject obj = new GameObject("WorkPackageItem", typeof(Button), typeof(Image));
            obj.transform.SetParent(ModClass.Instance.transform);
            var bg = obj.GetComponent<Image>();
            bg.sprite = SpriteTextureLoader.getSprite("ui/special/button");
            bg.type = Image.Type.Sliced;
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(90, 48);

            var name = Instantiate(SimpleText.Prefab, obj.transform);
            name.name = "Name";
            name.transform.localPosition = new Vector3(0, 9);
            var author = Instantiate(SimpleText.Prefab, obj.transform);
            author.name = "Author";
            author.transform.localPosition = new Vector3(0, -9);

            Prefab = obj.AddComponent<WorkPackageItem>();
        }
        [Hotfixable]
        protected override void Init()
        {
            if (Initialized) return;
            base.Init();

            Name = transform.Find("Name").GetComponent<SimpleText>();
            Author = transform.Find("Author").GetComponent<SimpleText>();
            GetComponent<Button>().onClick.AddListener(() => CN_PackageSelectWindow.Instance.SelectPackage(Meta));
            BG = GetComponent<Image>();
        }
        public Image BG { get; private set; }
        public SimpleText Name { get; private set; }
        public SimpleText Author { get; private set; }
        public CN_PackageMeta Meta { get; private set; }
        [Hotfixable]
        public void Setup(CN_PackageMeta meta)
        {
            Init();
            Meta = meta;
            Name.Setup(meta.name, TextAnchor.MiddleCenter, new(80, 16));
            Author.Setup(meta.author, TextAnchor.MiddleCenter, new(80, 16));
            Name.text.resizeTextMaxSize = 10;
            Author.text.resizeTextMaxSize = 10;
        }
    }
}
