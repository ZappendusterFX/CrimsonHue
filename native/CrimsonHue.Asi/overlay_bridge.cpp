#include "overlay.h"
#include "overlay_shortcut.h"
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
    bool installed = false, firstFrame = false;
    const char* lastStatus = nullptr;
    unsigned attempts = 0;
    ULONGLONG nextAttempt = 0;
    for (;;) {
        if (!installed && attempts < 10 && GetTickCount64() >= nextAttempt) {
            installed = InstallGraphics(); ++attempts; nextAttempt = GetTickCount64() + 500;
            Diagnostic(installed ? "Graphics hooks installed." : "Graphics hook installation failed; retry is bounded to ten attempts.");
        }
        RefreshBridge(); MaintainGraphics();
        const auto status = GraphicsStatus();
        if (status != lastStatus) { Diagnostic(status); lastStatus = status; }
        if (!firstFrame && RenderedFrames()) { firstFrame = true; Diagnostic("First in-game overlay frame submitted."); }
        Sleep(50);
    }
}
}
void Start() noexcept {
    InitializeDiagnostics(); Diagnostic("Native graphics worker starting.");
    const auto thread = CreateThread(nullptr, 0, GraphicsWorker, nullptr, 0, nullptr);
    if (thread) CloseHandle(thread);
    else Diagnostic("Native graphics worker creation failed.");
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
                if (data.contains("shortcut")) {
                    const auto& key = data.at("shortcut");
                    UpdateShortcut({key.at("key").get<int>(), key.at("control").get<bool>(),
                        key.at("shift").get<bool>(), key.at("alt").get<bool>()});
                }
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
