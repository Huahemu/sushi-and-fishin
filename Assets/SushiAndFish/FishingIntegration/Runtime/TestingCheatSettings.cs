using Kamgam.SettingsGenerator;
using Kamgam.UGUIComponentsForSettings;
using Michsky.MUIP;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace SushiAndFish.FishingIntegration
{
    [DisallowMultipleComponent]
    public sealed class TestingCheatSettings : MonoBehaviour
    {
        Toggle toggle;
        TMP_Text status;
        RectTransform category;
        float originalHeight;
        public Toggle Control => toggle;
        public TMP_Text Status => status;

        public void Build(RectTransform gameplay)
        {
            if (toggle || !gameplay) return;
            var content = gameplay.Find("Content") as RectTransform;
            var template = content ? content.Find("ShowHudToggle") : null;
            if (!template) return;
            category = gameplay;
            originalHeight = gameplay.sizeDelta.y;

            // Keep the clone inactive until the original setting resolver and
            // localization are disconnected: it must never register as showHud.
            var staging = new GameObject("Cheat settings staging");
            staging.SetActive(false);
            var row = Instantiate(template.gameObject, staging.transform);
            row.name = "TestingCheatModeToggle";
            foreach (var resolver in row.GetComponentsInChildren<ToggleUGUIResolver>(true))
            {
                resolver.enabled = false;
                Remove(resolver);
            }
            foreach (var localizer in row.GetComponentsInChildren<LocalizeStringEvent>(true)) localizer.enabled = false;
            toggle = row.GetComponent<Toggle>();
            toggle.group = null;
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.SetIsOnWithoutNotify(false);
            var wrapper = row.GetComponent<ToggleUGUI>();
            if (wrapper) wrapper.Toggle = toggle;
            var custom = row.GetComponent<CustomSwitch>();
            if (custom) custom.invokeOnAwake = false;
            var label = row.transform.Find("Label").GetComponent<TMP_Text>();
            label.text = "作弊模式";
            var config = MigrationConfig.Load();
            if (config && config.font) label.font = config.font;
            row.transform.SetParent(content, false);
            row.SetActive(true);
            Remove(staging);

            status = Instantiate(label, row.transform);
            status.name = "TestingCheatModeDescription";
            status.text = "金币 / 零件各 999999 · 仅房主可用";
            status.fontSize = 24;
            status.enableAutoSizing = false;
            status.alignment = TextAlignmentOptions.MidlineLeft;
            status.textWrappingMode = TextWrappingModes.Normal;
            status.raycastTarget = false;
            var rect = status.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(900, 70);
            rect.anchoredPosition = new Vector2(-425, -32);
            toggle.onValueChanged.AddListener(OnChanged);
            Refresh();
            RefreshLayout();
        }

        public void Refresh()
        {
            if (!toggle) return;
            bool ready = TestingCheatMode.TryGetHostEconomy(out var economy);
            var mode = ready ? economy.GetComponent<TestingCheatMode>() : null;
            Render(ready, mode && mode.Enabled);
        }

        // Presentation only, also used by the static editor preview.
        public void Render(bool ready, bool enabled)
        {
            if (!toggle) return;
            toggle.interactable = ready;
            bool changed = toggle.isOn != enabled;
            toggle.SetIsOnWithoutNotify(enabled);
            if (changed && Application.isPlaying) toggle.GetComponent<CustomSwitch>()?.UpdateState();
            status.text = !ready ? "进入存档后仅房主可开启 · 金币 / 零件各 999999"
                : enabled ? "金币 / 零件已发放 · 关闭再开启可重新补充"
                : "开启时金币 / 零件各设为 999999 · 关闭不扣除";
        }

        void OnChanged(bool enabled)
        {
            TestingCheatMode.SetForCurrentSession(enabled);
            Refresh();
        }
        void OnEnable() { if (Application.isPlaying) { Refresh(); RefreshLayout(); } }
        void LateUpdate() { if (Application.isPlaying) Refresh(); }
        public void RefreshLayout()
        {
            if (!category || !category.gameObject.activeInHierarchy) return;
            var content = category.Find("Content") as RectTransform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var corners = new Vector3[4];
            float bottom = originalHeight;
            foreach (var row in content.GetComponentsInChildren<RectTransform>())
            {
                if (!row.gameObject.activeSelf) continue;
                row.GetWorldCorners(corners);
                foreach (var corner in corners) bottom = Mathf.Max(bottom, -category.InverseTransformPoint(corner).y + 55);
            }
            category.sizeDelta = new Vector2(category.sizeDelta.x, bottom);
        }
        void OnDestroy()
        {
            if (toggle) toggle.onValueChanged.RemoveListener(OnChanged);
        }
        static void Remove(Object target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
