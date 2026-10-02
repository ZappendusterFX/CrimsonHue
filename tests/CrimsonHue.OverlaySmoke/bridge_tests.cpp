#include "overlay.h"
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
        std::cout << "PASS UI bridge: bounded queue, FIFO Start/Stop, cleanup retry under STA backpressure and snapshot exchange; no runtime, GPU, network or lamps.\n";
        return 0;
    } catch (const std::exception& error) { std::cerr << error.what() << '\n'; return 1; }
}
