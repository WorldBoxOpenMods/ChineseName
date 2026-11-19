using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Chinese_Name.Naming.Game;

public sealed partial class NamingWindow
{
    public sealed partial class OnomasticsEditor
    {
        private const float PreviewButtonWidth = 18;
        private const float FlagSpacing = 2;

        private static RectTransform Stack(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            obj.transform.SetParent(parent, false);
            var layout = obj.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 2;
            return (RectTransform)obj.transform;
        }

        private static void Inset(RectTransform rect, float horizontal, float vertical)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        private static void Clear(Transform parent)
        {
            foreach (Transform child in parent)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private static RectTransform Row(Transform parent, string name, float height, float spacing)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            var layout = obj.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = spacing;
            Measure(obj, PreviewWidth, height);
            return (RectTransform)obj.transform;
        }

        private static void Measure(GameObject obj, float width, float height)
        {
            var layout = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            ((RectTransform)obj.transform).sizeDelta = new Vector2(width, height);
        }

        private static Text TextAt(Transform parent, string name, string value, int size, TextAnchor alignment)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>();
            text.font = EditorFont() ?? LocalizedTextManager.current_font;
            text.fontSize = size;
            text.alignment = alignment;
            text.supportRichText = false;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static Button PreviewButton(Transform parent, string id, string tooltip, bool selected, Action action)
        {
            var obj = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            Measure(obj, PreviewButtonWidth, PreviewToolbarHeight);
            var button = obj.GetComponent<Button>();
            button.targetGraphic = obj.GetComponent<Image>();
            Surface(button.image, selected ? "ui/tab_button_sort_selected" : "ui/tab_button_sort");
            button.onClick.AddListener(() => { MusicBox.playSoundUI("event:/SFX/UI/Click"); action(); });
            var tip = obj.AddComponent<TipButton>();
            tip.textOnClick = tooltip;
            tip.textOnClickDescription = "";
            tip.showOnClick = false;
            return button;
        }

        private static void Surface(Image image, string path)
        {
            image.sprite = SpriteTextureLoader.getSprite(path);
            image.type = Image.Type.Sliced;
        }

        private static void Icon(Transform parent, string name, string path, float size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.sprite = SpriteTextureLoader.getSprite(path);
            image.preserveAspect = true;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = Vector2.one * size;
            rect.anchoredPosition = Vector2.zero;
        }

        private void LanguageFlags(Transform parent)
        {
            var page = _page;
            var languages = new Language[] { null }.Concat(World.world.languages.OrderBy(language => language.id)).ToArray();
            var selected = Array.FindIndex(languages, language => language?.id == page.ReferenceLanguage);
            if (selected < 0) selected = 0;
            float width = languages.Length * (PreviewButtonWidth + FlagSpacing) - FlagSpacing;
            float viewportWidth = Mathf.Min(width, PreviewWidth - PreviewButtonWidth - 3);
            var flags = Row(parent, "PreviewLanguageFlags", PreviewToolbarHeight, FlagSpacing);
            Measure(flags.gameObject, width, PreviewToolbarHeight);
            var bannerAsset = AssetManager.meta_customization_library.getAsset(MetaType.Language);
            for (int index = 0; index < languages.Length; index++)
            {
                var language = languages[index];
                long? id = language?.id;
                var button = PreviewButton(flags, "PreviewLanguage." + (id?.ToString() ?? "Default"), language?.name ?? "世界默认",
                    index == selected, () => Change(() => page.ReferenceLanguage = id));
                if (language == null) Icon(button.transform, "World", "ui/Icons/iconWorldInfo", 10);
                else
                {
                    var banner = (LanguageBanner)bannerAsset.get_banner(bannerAsset, language, button.transform);
                    banner.name = "Flag";
                    var rect = (RectTransform)banner.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.localScale = Vector3.one * ((PreviewToolbarHeight - 1) / rect.rect.height);
                    banner.GetComponent<Button>().enabled = false;
                    foreach (var graphic in banner.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
                }
            }
            float overflow = width - viewportWidth;
            if (overflow <= 0) return;

            var viewport = new GameObject("PreviewLanguageScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(parent, false);
            Measure(viewport, viewportWidth, PreviewToolbarHeight);
            viewport.GetComponent<Image>().color = Color.clear;
            flags.SetParent(viewport.transform, false);
            flags.anchorMin = flags.anchorMax = new Vector2(0, .5f);
            flags.pivot = new Vector2(0, .5f);
            float offset = (selected + 1) * (PreviewButtonWidth + FlagSpacing) - FlagSpacing - viewportWidth;
            flags.anchoredPosition = new Vector2(-Mathf.Clamp(page.LanguagePosition ?? offset, 0, overflow), 0);
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = flags;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = PreviewButtonWidth + FlagSpacing;
            scroll.onValueChanged.AddListener(_ => page.LanguagePosition = -flags.anchoredPosition.x);
            foreach (Transform flag in flags) flag.gameObject.AddComponent<ScrollableButton>();
        }

    }
}
