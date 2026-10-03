#include "overlay.h"
#include "overlay_shortcut.h"
#include <cstring>
#include <iostream>
#include <stdexcept>
#include <vector>

using namespace crimson::overlay;
namespace {
std::vector<std::string> received;
bool refuseCleanup = false;
int __stdcall Read(void* destination, int capacity) {
    constexpr char state[] = "{\"ready\":true}";
    if (capacity < static_cast<int>(sizeof(state) - 1)) return 0;
    memcpy(destination, state, sizeof(state) - 1); return sizeof(state) - 1;
}
int __stdcall Send(const void* bytes, int size) {
    const auto data = static_cast<const char*>(bytes);
    const auto action = Json::parse(data, data + size).at("action").get<std::string>();
    if (refuseCleanup && (action == "stop" || action == "close")) { refuseCleanup = false; return 0; }
    received.push_back(action); return 1;
}
void Require(bool value, const char* message) { if (!value) throw std::runtime_error(message); }
}
int main() {
    try {
        ShortcutGate gate;
        const Shortcut binding{};
        Require(!gate.Update(binding, true, false, false, false, true), "Shortcut ignored required modifiers");
        Require(!gate.Update(binding, true, true, true, false, true), "Adding modifiers to a held key toggled overlay");
        gate.Update(binding, false, false, false, false, true);
        Require(gate.Update(binding, true, true, true, false, true), "Configured shortcut did not toggle");
        Require(!gate.Update(binding, true, true, true, false, true), "Held shortcut repeated");
        gate.Update(binding, false, false, false, false, false);
        Require(!gate.Update(binding, true, true, true, false, false), "Shortcut toggled outside game focus");
        Require(!gate.Update(binding, true, true, true, false, true), "Returning focus with held key toggled");
        const Shortcut rebound{'H', false, false, true};
        Require(!gate.Update(rebound, true, false, false, true, true), "Rebinding to a held key toggled");
        gate.Update(rebound, false, false, false, false, true);
        Require(gate.Update(rebound, true, false, false, true, true), "Alt shortcut failed");
        gate.Update(rebound, false, false, false, false, true);
        Require(!gate.Update(rebound, true, true, false, true, true), "Extra modifier toggled shortcut");
        UpdateShortcut(rebound); Require(CurrentShortcut() == rebound, "Configured shortcut not published");
        UpdateShortcut({VK_CONTROL, false, false, false}); Require(CurrentShortcut() == rebound, "Modifier accepted as main key");
        UpdateShortcut(binding);
        SetBridge(Read, Send);
        Command({{"action", "start"}}); Command({{"action", "stop"}}); RefreshBridge();
        Require(received == std::vector<std::string>{"start", "stop"}, "Queued Start replayed after Stop");
        received.clear(); refuseCleanup = true;
        Command({{"action", "close"}}); RefreshBridge(); Require(received.empty(), "Mock backpressure failed");
        RefreshBridge(); Require(received == std::vector<std::string>{"close"}, "Cleanup was lost under STA backpressure");
        received.clear();
        for (int i = 0; i < 80; ++i) Command({{"action", "set"}, {"value", i}});
        Command({{"action", "stop"}}); RefreshBridge();
        Require(received.size() == 64 && received.back() == "stop", "Full render queue dropped cleanup or exceeded its bound");
        Json state; Require(TryRead(state) && state.value("ready", false), "Snapshot did not reach renderer");
        SetBridge(nullptr, nullptr);
        std::cout << "PASS configured shortcut edges/focus/modifiers/rebind and UI bridge bounds/FIFO/cleanup/snapshot; no runtime, GPU, network or lamps.\n";
        return 0;
    } catch (const std::exception& error) { std::cerr << error.what() << '\n'; return 1; }
}
