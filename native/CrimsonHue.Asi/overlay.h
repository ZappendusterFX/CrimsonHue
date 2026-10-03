#pragma once
#include <windows.h>
#include <chrono>
#include <filesystem>
#include <string>
#include <nlohmann/json.hpp>

namespace crimson::overlay {
using Clock = std::chrono::steady_clock;
using Json = nlohmann::json;
using read_fn = int(__stdcall*)(void*, int);
using send_fn = int(__stdcall*)(const void*, int);
void Start() noexcept;
void InitializeDiagnostics() noexcept;
void Diagnostic(const std::string& message) noexcept;
void SetBridge(read_fn read, send_fn send) noexcept;
void RefreshBridge() noexcept;
void Command(Json value) noexcept;
void Toggle() noexcept;
bool InstallGraphics() noexcept;
void MaintainGraphics() noexcept;
const char* GraphicsStatus() noexcept;
unsigned long long RenderedFrames() noexcept;
void SetVisibleForTest(bool visible) noexcept;
// Snapshot copying and input queuing never call CoreCLR or a device in Present.
bool TryRead(Json& view);
void DrawUi(const Json& view, bool& visible);
void SetInputWindow(HWND window) noexcept;
void FeedInput(HWND window, bool visible);
bool HasInputFocus() noexcept;
void ReleaseInput() noexcept;
}
