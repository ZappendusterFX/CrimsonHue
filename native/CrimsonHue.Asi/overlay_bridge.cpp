#include "overlay.h"
#include <atomic>
#include <deque>
#include <mutex>
#include <vector>

namespace crimson::overlay {
namespace {
std::mutex bridgeMutex;
Json latest = Json::object();
std::deque<std::string> commands;
std::atomic<read_fn> readState{nullptr};
std::atomic<send_fn> sendCommand{nullptr};
DWORD WINAPI GraphicsWorker(void*) {
    InstallGraphics();
    for (;;) { RefreshBridge(); MaintainGraphics(); Sleep(50); }
}
}
void Start() noexcept {
    const auto thread = CreateThread(nullptr, 0, GraphicsWorker, nullptr, 0, nullptr);
    if (thread) CloseHandle(thread);
}
void SetBridge(read_fn read, send_fn send) noexcept { readState = read; sendCommand = send; }
void Command(Json value) noexcept {
    try {
        auto text = value.dump();
        if (text.size() > 16384) return;
        std::lock_guard lock(bridgeMutex);
        if (value.value("action", std::string{}) == "stop" || value.value("action", std::string{}) == "close") {
            if (commands.size() >= 64) commands.pop_back();
            // Preserve FIFO: an already queued Start must run before this Stop,
            // never be replayed afterwards and silently re-arm the stream.
            commands.push_back(std::move(text)); return;
        }
        if (commands.size() < 64) commands.push_back(std::move(text));
    } catch (...) { }
}
bool TryRead(Json& view) {
    std::unique_lock lock(bridgeMutex, std::try_to_lock);
    if (!lock.owns_lock()) return false;
    view = latest; return true;
}
void RefreshBridge() noexcept {
    try {
        if (const auto read = readState.load()) {
            std::vector<char> bytes(256 * 1024);
            const int size = read(bytes.data(), static_cast<int>(bytes.size()));
            if (size > 0 && size <= static_cast<int>(bytes.size())) {
                auto data = Json::parse(bytes.data(), bytes.data() + size);
                std::lock_guard lock(bridgeMutex); latest = std::move(data);
            }
        }
        if (const auto send = sendCommand.load()) {
            std::deque<std::string> pending;
            { std::lock_guard lock(bridgeMutex); pending.swap(commands); }
            for (const auto& command : pending) {
                if (!send(command.data(), static_cast<int>(command.size()))) {
                    const auto value = Json::parse(command);
                    const auto action = value.value("action", std::string{});
                    // A temporarily full STA queue must not discard cleanup.
                    if (action == "stop" || action == "close") Command(value);
                }
            }
        }
    } catch (...) { }
}
}
