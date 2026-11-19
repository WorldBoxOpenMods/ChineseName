using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;

namespace Chinese_Name.Naming.Game;

internal static class NativeNamingTab
{
    private const string ModCategoryPrefix = "tab_onomastics_mod_";
    private static MapStats _map;
    private static readonly Dictionary<(long, string), OnomasticsData> Drafts = new();
    private static readonly Dictionary<(long, string), string> Originals = new();
    private static readonly Dictionary<(long, string), HashSet<string>> BoundDraftGroups = new();

    internal static void ShowBoundGroups(long culture, string type, CardNaming choice)
    {
        Sync();
        var key = (culture, type);
        if (choice == null) BoundDraftGroups.Remove(key);
        else BoundDraftGroups[key] = new HashSet<string>(choice.Roles.Keys.Concat(choice.Pools.Keys));
    }

    internal static void RememberDefault(long culture, string type, OnomasticsData data) =>
        Originals[(culture, type)] = data.getShortTemplate();

    internal static bool HadRuleSeparator(long culture, string type) =>
        Originals.TryGetValue((culture, type), out var original) &&
        original.Split('|')[0].Contains(NativeNamingGlyphs.RuleSeparatorCode);

    internal static void ForgetDraft(long culture, string type)
    {
        Sync();
        var key = (culture, type);
        Drafts.Remove(key);
        Originals.Remove(key);
        BoundDraftGroups.Remove(key);
    }

    internal static IEnumerable<(long Culture, string Type, string Template)> ChangedDrafts()
    {
        Sync();
        foreach (var item in Drafts)
        {
            string template = item.Value.getShortTemplate();
            if (!Originals.TryGetValue(item.Key, out string original) || original != template)
                yield return (item.Key.Item1, item.Key.Item2, template);
        }
    }

    internal static bool HasChanged(long culture, string type)
    {
        var template = DraftTemplate(culture, type);
        return template != null && (!Originals.TryGetValue((culture, type), out var original) ||
            template != original);
    }

    private static (MapStats Map, Culture Culture, string Type)? _opening;

    internal static void OpenCulture(Culture culture, string type)
    {
        if (!NativeOnomastics.Supports(type)) return;
        _opening = (World.world.map_stats, culture, type);
        OpenWhenReady();
    }

    internal static void OpenWhenReady()
    {
        if (!_opening.HasValue || ScrollWindow.isAnimationActive()) return;
        var request = _opening.Value;
        _opening = null;
        if (SmoothLoader.isLoading() || !ReferenceEquals(request.Map, World.world?.map_stats) || !request.Culture.isAlive()) return;
        SelectedMetas.selected_culture = request.Culture;
        ScrollWindow.showWindow("culture");
        var window = ScrollWindow.getCurrentWindow();
        window.tabs.showTab("Onomastics");
        var tab = window.GetComponentInChildren<OnomasticsTab>();
        if (tab == null) return;
        string category = Category(request.Type);
        tab._tab_groups._toggles.Find(toggle => toggle.name == category)?.toggle.click();
    }

    private static string Category(string type)
    {
        if (NativeOnomastics.Meta(type) is MetaType meta) return "tab_onomastics_" + meta.ToString().ToLowerInvariant();
        return NativeOnomastics.AdditionalCategories.Any(category => category.Type == type)
            ? "tab_onomastics_" + type.Substring("worldbox:".Length)
            : ModCategoryPrefix + type;
    }

    internal static string CurrentType(OnomasticsTab tab)
    {
        if (tab._name_set_type != MetaType.None) return NativeOnomastics.Type(tab._name_set_type);
        string button = tab._tab_groups.getCurrentButton()?.name;
        if (button?.StartsWith(ModCategoryPrefix, StringComparison.Ordinal) == true)
        {
            string type = button.Substring(ModCategoryPrefix.Length);
            return NamingApi.Current.CultureOnomastics.ContainsKey(type) ? type : null;
        }
        return NativeOnomastics.AdditionalCategories.FirstOrDefault(category => Category(category.Type) == button).Type;
    }

    [HarmonyPatch(typeof(OnomasticsTab), nameof(OnomasticsTab.showCategoryGroups))]
    internal static class Categories
    {
        private static void Postfix(OnomasticsTab __instance)
        {
            Add(MetaType.Culture, "文化名称");
            Add(MetaType.Language, "语言名称");
            Add(MetaType.Religion, "宗教名称");
            var group = __instance._tab_groups;
            var service = NamingApi.Current;
            var types = service.Types.ToDictionary(type => type.Id);
            var categories = NativeOnomastics.AdditionalCategories.Select(category =>
                (category.Type, Icon: "ui/Icons/" + category.Icon, category.Description)).Concat(
                service.CultureOnomastics.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Where(entry => NativeOnomastics.Meta(entry.Key) == null && Category(entry.Key).StartsWith(ModCategoryPrefix, StringComparison.Ordinal))
                    .Select(entry => (Type: entry.Key, entry.Value.Icon, entry.Value.Description)));
            foreach (var category in categories)
            {
                string key = Category(category.Type);
                LM.AddToCurrentLocale(key, types[category.Type].Label);
                LM.AddToCurrentLocale(key + "_description", category.Description);
                group.tryAddButton(category.Icon, key, __instance.loadNameSet,
                    () => __instance._name_set_type = MetaType.None);
            }
            FitCategories(group);

            void Add(MetaType type, string label)
            {
                string tab = Category(NativeOnomastics.Type(type));
                LM.AddToCurrentLocale(tab, label);
                LM.AddToCurrentLocale(tab + "_description", "编辑本文化生成" + label + "的规则。");
                __instance._tab_groups.tryAddButton("ui/Icons/icon" + type, tab, __instance.loadNameSet,
                    () => __instance._name_set_type = type);
            }
        }

        private static void FitCategories(TabTogglesGroup group)
        {
            if (group.GetComponent<GridLayoutGroup>() != null) return;
            var original = group.GetComponent<HorizontalLayoutGroup>();
            var padding = original.padding;
            float spacing = original.spacing;
            var alignment = original.childAlignment;
            float height = ((RectTransform)group.transform).rect.height - padding.vertical;
            // Unity allows only one layout group on this container.
            UnityEngine.Object.DestroyImmediate(original);
            var layout = group.gameObject.AddComponent<GridLayoutGroup>();
            layout.padding = padding;
            layout.spacing = Vector2.one * spacing;
            layout.childAlignment = alignment;
            layout.cellSize = new Vector2(23, height);
            var fit = group.GetComponent<ContentSizeFitter>() ?? group.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    internal static string DraftTemplate(long culture, string type)
    {
        Sync();
        return type != null && Drafts.TryGetValue((culture, type), out var cards)
            ? cards.getShortTemplate() : null;
    }

    private static void Sync()
    {
        var map = World.world?.map_stats;
        if (ReferenceEquals(map, _map)) return;
        _map = map;
        Drafts.Clear();
        Originals.Clear();
        BoundDraftGroups.Clear();
    }

    [HarmonyPatch]
    internal static class BoundGroups
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods() => new[]
        {
            AccessTools.Method(typeof(OnomasticsTab), nameof(OnomasticsTab.clickAssetButton)),
            AccessTools.Method(typeof(OnomasticsAssetButton), nameof(OnomasticsAssetButton.doesGroupHaveContent))
        };

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            IndependentNames.ReplaceCall(instructions,
                AccessTools.Method(typeof(OnomasticsData), nameof(OnomasticsData.isGroupEmpty), new[] { typeof(string) }),
                AccessTools.Method(typeof(BoundGroups), nameof(IsEmptyInEditor)));

        // 只替换编辑器的添加限制和卡片着色。原版特殊字形仍按实际词组内容判断。
        private static bool IsEmptyInEditor(OnomasticsData data, string group)
        {
            if (!data.isGroupEmpty(group)) return false;
            if (SelectedMetas.selected_culture is not { } culture) return true;
            foreach (var item in Drafts)
            {
                if (item.Key.Item1 == culture.id && ReferenceEquals(data, item.Value) &&
                    BoundDraftGroups.TryGetValue(item.Key, out var bound)) return !bound.Contains(group);
            }
            return true;
        }
    }

    private static OnomasticsData Edit(Culture culture, MetaType type, bool reset, OnomasticsTab tab)
    {
        string id = CurrentType(tab);
        if (id == null) return culture.getOnomasticData(type, reset);
        Sync();
        var key = (culture.id, id);
        if (reset || !Drafts.TryGetValue(key, out var existing))
        {
            var applied = NativeOnomastics.Selection(NamingApi.Current.ActiveWorld, culture, id);
            var saved = reset ? null : NativeOnomastics.Selection(WorldNamingStore.GetSavedDraft(), culture, id)?.DraftTemplate;
            existing = saved == null ? NativeOnomastics.Read(culture, id, applied, reset) : NativeOnomastics.Parse(saved);
            Drafts[key] = existing;
            if (!reset)
            {
                using var stored = saved == null ? null : NativeOnomastics.Read(culture, id, applied);
                Originals[key] = (stored ?? existing).getShortTemplate();
            }
        }
        return existing;
    }

    [HarmonyPatch(typeof(OnomasticsTab), nameof(OnomasticsTab.loadFromTemplate))]
    internal static class ReadDraft
    {
        private static void Prefix(OnomasticsTab __instance) =>
            __instance.GetComponent<NamingWindow.OnomasticsEditor>()?.BeforeNativeLoad();

        private static void Postfix(OnomasticsTab __instance) =>
            __instance.GetComponent<NamingWindow.OnomasticsEditor>()?.AfterNativeLoad();

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            IndependentNames.ReplaceCall(instructions, AccessTools.Method(typeof(Culture), nameof(Culture.getOnomasticData)),
                AccessTools.Method(typeof(NativeNamingTab), nameof(Edit)), 0);
    }

    [HarmonyPatch(typeof(OnomasticsTab), nameof(OnomasticsTab.OnEnable))]
    internal static class AttachEditor
    {
        private static void Postfix(OnomasticsTab __instance)
        {
            var editor = __instance.GetComponent<NamingWindow.OnomasticsEditor>() ??
                __instance.gameObject.AddComponent<NamingWindow.OnomasticsEditor>();
            editor.Attach(__instance);
        }
    }

    [HarmonyPatch(typeof(OnomasticsAssetButton), nameof(OnomasticsAssetButton.loadAsset))]
    internal static class PreparePooledGlyph
    {
        // 折叠的排列区也会从原版对象池取字形，此时新按钮还没有执行 Awake。
        private static void Prefix(OnomasticsAssetButton __instance)
        {
            if (__instance.image == null) __instance.create();
        }
    }

    [HarmonyPatch(typeof(OnomasticsNameGenerator), nameof(OnomasticsNameGenerator.updateNameGeneration))]
    internal static class DraftPreview
    {
        private static bool Prefix(OnomasticsNameGenerator __instance) =>
            __instance is not OnomasticsTab tab || CurrentType(tab) == null ||
            tab.GetComponent<NamingWindow.OnomasticsEditor>() == null;
    }

    [HarmonyPatch(typeof(OnomasticsTab), nameof(OnomasticsTab.resetNameGenerationTextBox))]
    internal static class InvalidatePreview
    {
        private static void Postfix(OnomasticsTab __instance)
        {
            var editor = __instance.GetComponent<NamingWindow.OnomasticsEditor>();
            editor?.NativeChanged();
        }
    }

    [HarmonyPatch(typeof(OnomasticsTab), nameof(OnomasticsTab.clickAssetButton))]
    internal static class GlyphPalette
    {
        private static bool Prefix(OnomasticsTab __instance, OnomasticsAssetButton pButton, out int __state)
        {
            __state = __instance._pool_buttons.countActive();
            return NativeNamingGlyphs.AppliesTo(pButton.onomastics_asset.id, CurrentType(__instance));
        }

        private static void Postfix(OnomasticsTab __instance, int __state) =>
            __instance.GetComponent<NamingWindow.OnomasticsEditor>()?.PaletteClicked(__state);
    }

    [HarmonyPatch(typeof(OnomasticsTab), nameof(OnomasticsTab.loadTemplate))]
    internal static class GlyphImport
    {
        private static bool Prefix(OnomasticsTab __instance, string pTemplate)
        {
            if (pTemplate == null) return true;
            string type = CurrentType(__instance);
            if (pTemplate.Split('|')[0].Select(code => AssetManager.onomastics_library.getByShortId(code.ToString()))
                .Any(glyph => glyph != null && !NativeNamingGlyphs.AppliesTo(glyph.id, type)))
            {
                WorldTip.showNow("卡片含有其他命名目标的字形。", pTranslate: false, "top", 3f, "#FF637D");
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(NameInput), nameof(NameInput.validateOnomastics))]
    internal static class WordGroupCharacters
    {
        private static void Postfix(NameInput __instance, char pAddedChar, ref char __result)
        {
            // A written pool ID uses "/" in place of the ":" reserved by the native save format.
            if ((pAddedChar is '—' or '/' or '-' or '.' or >= '0' and <= '9') &&
                __instance.GetComponentInParent<OnomasticsTab>() is { } tab && CurrentType(tab) != null)
                __result = pAddedChar;
        }
    }

    [HarmonyPatch(typeof(OnomasticsTab), nameof(OnomasticsTab.resetTemplate))]
    internal static class ResetGlyphs
    {
        private static bool Prefix(OnomasticsTab __instance) =>
            __instance.GetComponent<NamingWindow.OnomasticsEditor>()?.ResetArrangement() != true;
    }

    [HarmonyPatch(typeof(OnomasticsTab), nameof(OnomasticsTab.openFirstGroup))]
    internal static class CloseWordEditorOnLoad
    {
        private static bool Prefix(OnomasticsTab __instance)
        {
            if (CurrentType(__instance) == null) return true;
            __instance._selected_editor_group = null;
            __instance._selected_editor_group_button = null;
            __instance.parent_asset_editor_group.gameObject.SetActive(false);
            __instance.selected_icon_effect.enabled = __instance.selected_icon_effect_2.enabled = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(OnomasticsTab), nameof(OnomasticsTab.openGroup))]
    internal static class WordGroupSelection
    {
        private static void Postfix(OnomasticsTab __instance) =>
            __instance.selected_icon_effect.enabled = __instance.selected_icon_effect_2.enabled = true;
    }
}
