#include "overlay.h"
#include <imgui.h>
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdio>
#include <cstring>

namespace crimson::overlay {
namespace {
std::string Text(const Json& value, const char* key) { return value.value(key, std::string{}); }
void Wrap(const std::string& text) { ImGui::TextWrapped("%s", text.c_str()); }
void Action(const char* name) { Command({{"action", name}}); }
bool Button(const char* label, bool enabled = true) {
    ImGui::BeginDisabled(!enabled); const bool clicked = ImGui::Button(label); ImGui::EndDisabled(); return clicked;
}
void Room(const Json& view) {
    const ImVec2 size{std::max(200.0f, ImGui::GetContentRegionAvail().x), 260};
    const auto at = ImGui::GetCursorScreenPos();
    ImGui::InvisibleButton("Room", size);
    auto* draw = ImGui::GetWindowDrawList();
    const ImVec2 center{at.x + size.x / 2, at.y + size.y / 2};
    const float sx = size.x * .43f, sy = size.y * .35f;
    draw->AddRectFilled(at, {at.x + size.x, at.y + size.y}, IM_COL32(21, 25, 34, 255), 8);
    draw->AddLine({center.x - 55, center.y - sy}, {center.x + 55, center.y - sy}, IM_COL32(165, 180, 210, 255), 4);
    draw->AddText({center.x - 28, center.y - sy - 25}, IM_COL32(175, 185, 200, 255), "SCREEN");
    draw->AddCircleFilled(center, 5, IM_COL32(215, 220, 235, 255));
    if (view.value("leftRight", true)) draw->AddLine({center.x, center.y - sy + 10}, {center.x, center.y + sy}, IM_COL32(55, 70, 85, 255));
    if (view.value("frontRear", true)) draw->AddLine({center.x - sx, center.y}, {center.x + sx, center.y}, IM_COL32(55, 70, 85, 255));
    for (const auto& source : view.value("sources", Json::array())) {
        const auto direction = source.at("direction"); const auto fade = source.value("fade", 0.0f);
        if (fade > 0) draw->AddCircleFilled({center.x + direction.value("x", 0.0f) * sx * .7f,
            center.y - direction.value("y", 0.0f) * sy * .7f}, 2.0f, IM_COL32(155, 110, 90, static_cast<int>(fade * 255)));
    }
    for (const auto& channel : view.value("channels", Json::array())) {
        const auto rgb = channel.at("rgb");
        const ImVec2 point{center.x + channel.value("x", 0.0f) * sx, center.y - channel.value("y", 0.0f) * sy};
        const auto color = ImGui::ColorConvertFloat4ToU32({rgb.value("x", 0.0f), rgb.value("y", 0.0f), rgb.value("z", 0.0f), 1});
        draw->AddCircleFilled(point, 10, color); draw->AddCircle(point, 10, IM_COL32(170, 180, 195, 255));
        const auto label = channel.value("label", std::string{});
        draw->AddText({point.x - 20, point.y + 13}, IM_COL32(200, 205, 220, 255), label.c_str());
    }
    if (view.value("demo", false)) draw->AddText({at.x + 12, at.y + size.y - 25}, IM_COL32(239, 150, 170, 255), "SIMULATED DATA - NO LAMP OUTPUT");
}
void Controls(const Json& view, const char* group) {
    if (strcmp(group, "Space") == 0) {
        std::vector<float> curve; for (const auto& value : view.value("curve", Json::array())) curve.push_back(value.get<float>());
        ImGui::PlotLines("Distance fade", curve.data(), static_cast<int>(curve.size()), 0, nullptr, 0, 1, {0, 90});
        ImGui::Text("0 to %.2f game units", view.value("curveEnd", 35.0));
    }
    for (const auto& flag : view.value("switches", Json::array())) {
        if (flag.value("group", std::string{}) != group) continue;
        const auto key = Text(flag, "key"); bool value = flag.value("value", false);
        ImGui::PushID(key.c_str());
        if (ImGui::Checkbox(Text(flag, "title").c_str(), &value)) Command({{"action", "switch"}, {"key", key}, {"value", value}});
        Wrap(Text(flag, "description")); ImGui::PopID();
    }
    for (const auto& field : view.value("controls", Json::array())) {
        if (field.value("group", std::string{}) != group) continue;
        const auto key = Text(field, "key"); double value = field.value("value", 0.0);
        const double minimum = field.value("min", 0.0), maximum = field.value("max", 1.0);
        ImGui::PushID(key.c_str());
        ImGui::Text("%s %s", Text(field, "title").c_str(), Text(field, "unit").c_str());
        ImGui::SetNextItemWidth(std::max(120.0f, ImGui::GetContentRegionAvail().x - 170));
        bool changed = ImGui::SliderScalar("##slider", ImGuiDataType_Double, &value, &minimum, &maximum, "%.3f");
        ImGui::SameLine(); ImGui::SetNextItemWidth(95);
        // InputScalar's Enter commits exact values; arrows use each field's step.
        changed |= ImGui::InputScalar("##number", ImGuiDataType_Double, &value, nullptr, nullptr, "%.6g", ImGuiInputTextFlags_EnterReturnsTrue);
        ImGui::SameLine(); if (ImGui::SmallButton("Reset")) { value = field.value("initial", 0.0); changed = true; }
        if (changed && std::isfinite(value) && value >= minimum && value <= maximum)
            Command({{"action", "set"}, {"key", key}, {"value", value}});
        Wrap(Text(field, "description")); ImGui::Spacing(); ImGui::PopID();
    }
    if (Button("Reset this tab")) Command({{"action", "reset"}, {"group", group}});
    ImGui::SameLine(); if (Button("Save settings")) Action("save");
}
void Setup(const Json& view) {
    static std::array<char, 2048> bridge{}, telemetry{};
    static bool seeded = false;
    if (!seeded && view.value("ready", false)) {
        strcpy_s(bridge.data(), bridge.size(), Text(view, "bridgeAddress").c_str());
        strcpy_s(telemetry.data(), telemetry.size(), Text(view, "telemetryAddress").c_str()); seeded = true;
    }
    const bool enabled = view.value("canSetup", false);
    ImGui::BeginDisabled(!enabled);
    ImGui::InputText("Bridge IPv4", bridge.data(), bridge.size());
    ImGui::InputText("Telemetry WebSocket", telemetry.data(), telemetry.size());
    if (Button("Apply addresses")) Command({{"action", "setup"}, {"bridge", bridge.data()}, {"telemetry", telemetry.data()}});
    ImGui::EndDisabled();
    if (Button("Find bridge", enabled)) Action("find"); ImGui::SameLine();
    if (Button("Reconnect telemetry", enabled)) Action("reconnect");
    Wrap(Text(view, "bridgeStatus"));
    if (view.value("pairing", false)) {
        Wrap("Press the bridge link button, then pair within 30 seconds. Check the bridge identity and certificate first.");
        Wrap(Text(view, "certificate"));
        if (Button("Trust bridge & pair", enabled)) Action("pair");
    }
    auto selectedName = Text(view, "selectedArea");
    for (const auto& area : view.value("areas", Json::array())) if (Text(area, "id") == selectedName) { selectedName = Text(area, "name"); break; }
    if (ImGui::BeginCombo("Entertainment area", selectedName.c_str())) {
        for (const auto& area : view.value("areas", Json::array())) {
            ImGui::BeginDisabled(!enabled);
            if (ImGui::Selectable(Text(area, "name").c_str(), Text(area, "id") == Text(view, "selectedArea")))
                Command({{"action", "area"}, {"id", Text(area, "id")}});
            ImGui::EndDisabled();
        }
        ImGui::EndCombo();
    }
    if (Button("Refresh areas", view.value("canRefresh", false) && enabled)) Action("refresh");
    Wrap(Text(view, "layout")); Wrap("Create and position channels in the Hue app. Imported positions are preserved.");
}
void Presets(const Json& view) {
    static std::array<char, 2048> path{};
    static bool overwrite = false;
    static std::array<bool, 4> groups{true, true, true, false};
    ImGui::InputText("Preset path (.json)", path.data(), path.size());
    Wrap("Enter an absolute file path. Exports contain mixer controls only.");
    ImGui::Checkbox("Allow overwrite of this preset file", &overwrite);
    if (Button("Export preset")) Command({{"action", "export"}, {"path", path.data()}, {"overwrite", overwrite}});
    ImGui::SameLine(); if (Button("Preview import")) { groups = {true, true, true, false}; Command({{"action", "import"}, {"path", path.data()}}); }
    const auto name = Text(view, "presetName");
    if (!name.empty()) {
        ImGui::SeparatorText(name.c_str());
        const char* names[]{"Output", "Ambient", "Color", "Space"};
        int mask = 0;
        for (int i = 0; i < 4; ++i) { ImGui::Checkbox(names[i], &groups[i]); ImGui::SameLine(); if (groups[i]) mask |= 1 << i; }
        ImGui::NewLine();
        for (const auto& change : view.value("presetChanges", Json::array())) {
            const auto text = change.get<std::string>();
            for (int i = 0; i < 4; ++i) if (groups[i] && text.rfind(names[i], 0) == 0) Wrap(text);
        }
        if (Button("Apply selected", mask != 0)) Command({{"action", "applyImport"}, {"groups", mask}});
        ImGui::SameLine(); if (Button("Cancel import")) Action("cancelImport");
    }
}
}
void DrawUi(const Json& view, bool& visible) {
    ImGui::SetNextWindowSize({1120, 740}, ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowPos({60, 40}, ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowSizeConstraints({720, 480}, ImGui::GetIO().DisplaySize);
    bool open = true;
    if (ImGui::Begin("CrimsonHue - live lighting mixer", &open, ImGuiWindowFlags_NoCollapse)) {
        ImGui::TextColored({.95f, .55f, .65f, 1}, "IN-GAME ASI  /  Ctrl + Shift + F10");
        Wrap(Text(view, "telemetry")); Wrap(Text(view, "capture")); Wrap(Text(view, "ambient"));
        if (!view.value("ready", false)) { Wrap("Starting CrimsonHue. Requires .NET 8 Desktop Runtime x64."); Wrap(GraphicsStatus()); }
        else {
            const float left = std::max(350.0f, ImGui::GetContentRegionAvail().x * .47f);
            ImGui::BeginChild("Mixer", {left, -145}, ImGuiChildFlags_Borders);
            if (ImGui::BeginTabBar("MixerTabs")) {
                for (const auto* group : {"Output", "Ambient", "Color", "Space", "Setup", "Presets"}) {
                    if (ImGui::BeginTabItem(group)) {
                        if (strcmp(group, "Setup") == 0) Setup(view);
                        else if (strcmp(group, "Presets") == 0) Presets(view);
                        else Controls(view, group);
                        ImGui::EndTabItem();
                    }
                }
                ImGui::EndTabBar();
            }
            ImGui::EndChild(); ImGui::SameLine();
            ImGui::BeginChild("Monitor", {0, -145}, ImGuiChildFlags_Borders);
            bool demo = view.value("demo", false);
            ImGui::BeginDisabled(view.value("canStop", false) || view.value("smoke", false));
            if (ImGui::Checkbox("Demo preview only", &demo)) Command({{"action", "demo"}, {"value", demo}});
            ImGui::EndDisabled();
            if (ImGui::BeginTabBar("MonitorTabs")) {
                if (ImGui::BeginTabItem("Room & output")) {
                    Room(view); Wrap(Text(view, "boundary"));
                    for (const auto& channel : view.value("channels", Json::array())) {
                        const auto rgb = channel.at("rgb");
                        ImGui::Text("%s  CH %d  Z %.2f", Text(channel, "label").c_str(), channel.value("id", 0), channel.value("z", 0.0));
                        ImGui::Text("RGB %.4f / %.4f / %.4f", rgb.value("x", 0.0), rgb.value("y", 0.0), rgb.value("z", 0.0));
                    }
                    ImGui::EndTabItem();
                }
                if (ImGui::BeginTabItem("CDT raw input")) {
                    bool frozen = view.value("frozen", false);
                    if (ImGui::Checkbox("Freeze readout", &frozen)) Command({{"action", "freeze"}, {"value", frozen}});
                    Wrap(Text(view, "rawAmbient")); Wrap(Text(view, "rawSummary"));
                    const auto lights = view.value("rawLights", Json::array());
                    if (lights.is_array()) for (const auto& light : lights) {
                        ImGui::Text("%s gu  R %s  G %s  B %s", Text(light, "distance").c_str(), Text(light, "red").c_str(), Text(light, "green").c_str(), Text(light, "blue").c_str());
                        Wrap(Text(light, "position"));
                    }
                    ImGui::EndTabItem();
                }
                ImGui::EndTabBar();
            }
            ImGui::EndChild();
            Wrap(Text(view, "stream"));
            if (Button("Start lighting sync", view.value("canStart", false))) Action("start");
            ImGui::SameLine(); if (Button("Stop", view.value("canStop", false))) Action("stop");
            ImGui::SameLine(); if (Button("Hide settings")) visible = false;
            Wrap(Text(view, "tuning"));
        }
        Wrap("Stop and wait for stopped status before leaving the game. Close stops sync; Hide keeps it running.");
    }
    ImGui::End();
    // Window close means stop + save; the explicit Hide button keeps sync armed.
    if (!open) { visible = false; Action("close"); }
}
}
