#include <windows.h>
#include <hostfxr.h>
#include <coreclr_delegates.h>
#include <nethost.h>
#include "payload.h"
#include "overlay.h"
#include <atomic>
#include <filesystem>
#include <string>

namespace {
HMODULE module = nullptr;
std::atomic<int> status{0}; // 0 starting; 1 ready; 2 stopped; negative = failure.
std::atomic<bool> stopRequested{false};
using run_fn = int(CORECLR_DELEGATE_CALLTYPE*)(const wchar_t*, void*);
using command_fn = int(CORECLR_DELEGATE_CALLTYPE*)();
std::atomic<command_fn> stop{nullptr};
std::atomic<command_fn> toggle{nullptr};

// Thread attach also calls DllMain, including WPF render threads with small
// stacks. Keep the large process-path buffer out of its shared stack frame.
__declspec(noinline) bool IsSupportedHost() {
    wchar_t process[32768]{};
    const auto length = GetModuleFileNameW(nullptr, process, 32768);
    if (!length || length >= 32768) return false;
    const auto separator = wcsrchr(process, L'\\');
    const auto name = separator ? separator + 1 : process;
    return _wcsicmp(name, L"CrimsonDesert.exe") == 0 || _wcsicmp(name, L"CrimsonHue.AsiSmoke.exe") == 0 ||
        _wcsicmp(name, L"CrimsonHue.OverlaySmoke.exe") == 0;
}

void __stdcall ReadyCallback() {
    status = 1;
    if (stopRequested) { if (const auto request = stop.load()) request(); }
}

// Host diagnostics can contain paths; suppress them and expose fixed status codes.
void HOSTFXR_CALLTYPE IgnoreHostError(const wchar_t*) {}

std::filesystem::path ModuleDirectory() {
    std::wstring path(32768, L'\0');
    const DWORD size = GetModuleFileNameW(module, path.data(), static_cast<DWORD>(path.size()));
    if (!size || size >= path.size()) return {};
    path.resize(size);
    return std::filesystem::path(path).parent_path();
}

DWORD WINAPI Worker(void*) {
    // CoreCLR and callbacks live until process exit. Prevent unsafe FreeLibrary.
    HMODULE pinned = nullptr;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_PIN | GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,
                           reinterpret_cast<LPCWSTR>(&Worker), &pinned)) {
        status = -1; return 0;
    }
    try {
        const auto directory = ModuleDirectory();
        if (directory.empty()) { status = -2; return 0; }
        wchar_t smokePath[32768]{};
        const auto size = GetEnvironmentVariableW(L"CRIMSONHUE_ASI_SMOKE_PATH", smokePath, 32768);
        if (size >= 32768) { status = -8; return 0; }
        if (stopRequested) { status = 2; return 0; }
        // Capture creation before loading CoreCLR so early game swapchains are
        // not missed. The legacy WPF smoke path remains developer-only.
        wchar_t overlaySmoke[2]{};
        const bool testingOverlay = size && GetEnvironmentVariableW(L"CRIMSONHUE_ASI_OVERLAY_SMOKE", overlaySmoke, 2) == 1 && overlaySmoke[0] == L'1';
        if (!size || testingOverlay) crimson::overlay::Start();
        std::filesystem::path payload;
        try { payload = ResolvePayload(module, size ? smokePath : nullptr); }
        catch (...) { status = -12; return 0; }
        std::wstring hostPath(32768, L'\0');
        size_t hostSize = hostPath.size();
        // Static nethost finds the user's installed x64 .NET. No runtime DLLs,
        // dotnet.exe, or modifications to the DLL search path are packaged.
        wchar_t overrideRoot[32768]{};
        const auto overrideSize = size ? GetEnvironmentVariableW(L"CRIMSONHUE_ASI_SMOKE_DOTNET_ROOT", overrideRoot, 32768) : 0;
        get_hostfxr_parameters discovery{sizeof(discovery), nullptr, overrideSize && overrideSize < 32768 ? overrideRoot : nullptr};
        if (get_hostfxr_path(hostPath.data(), &hostSize, &discovery) != 0) {
            status = -3;
            if (!size) MessageBoxW(nullptr, L"CrimsonHue requires .NET 8 Desktop Runtime (x64). Install it from Microsoft's .NET 8 download page, then restart the game.", L"CrimsonHue", MB_OK | MB_ICONINFORMATION);
            return 0;
        }
        hostPath.resize(wcslen(hostPath.c_str()));
        const auto assembly = payload / L"CrimsonHue.dll";
        const auto fxr = LoadLibraryExW(hostPath.c_str(), nullptr,
                                       LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32);
        if (!fxr) { status = -3; return 0; }
        const auto initialize = reinterpret_cast<hostfxr_initialize_for_dotnet_command_line_fn>(
            GetProcAddress(fxr, "hostfxr_initialize_for_dotnet_command_line"));
        const auto getDelegate = reinterpret_cast<hostfxr_get_runtime_delegate_fn>(
            GetProcAddress(fxr, "hostfxr_get_runtime_delegate"));
        const auto close = reinterpret_cast<hostfxr_close_fn>(GetProcAddress(fxr, "hostfxr_close"));
        const auto writer = reinterpret_cast<hostfxr_set_error_writer_fn>(GetProcAddress(fxr, "hostfxr_set_error_writer"));
        if (!initialize || !getDelegate || !close || !writer) { status = -4; return 0; }
        const auto previousWriter = writer(IgnoreHostError);
        const auto host = directory / L"CrimsonHue.asi";
        // hostfxr lives at <dotnet>/host/fxr/<version>/hostfxr.dll.
        const auto dotnetRoot = std::filesystem::path(hostPath).parent_path().parent_path().parent_path().parent_path();
        hostfxr_initialize_parameters parameters{sizeof(parameters), host.c_str(), dotnetRoot.c_str()};
        const wchar_t* arguments[]{assembly.c_str()};
        hostfxr_handle context = nullptr;
        // The command-line hosting API resolves the installed Desktop Runtime and
        // dependencies without executing Main or launching a separate process.
        int result = initialize(1, arguments, &parameters, &context);
        if (result < 0 || !context) {
            if (context) close(context);
            writer(previousWriter); status = -5;
            if (!size) MessageBoxW(nullptr, L"CrimsonHue could not initialize .NET. Check that .NET 8 Desktop Runtime (x64) is installed, then restart the game.", L"CrimsonHue", MB_OK | MB_ICONINFORMATION);
            return 0;
        }
        get_function_pointer_fn function = nullptr;
        result = getDelegate(context, hdt_get_function_pointer, reinterpret_cast<void**>(&function));
        close(context);
        if (result < 0 || !function) { writer(previousWriter); status = -6; return 0; }
        run_fn run = nullptr;
        command_fn requestStop = nullptr;
        command_fn toggleWindow = nullptr;
        crimson::overlay::read_fn readState = nullptr;
        crimson::overlay::send_fn sendCommand = nullptr;
        constexpr auto type = L"CrimsonHue.App.AsiEntry, CrimsonHue";
        result = function(type, L"Run", UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr, reinterpret_cast<void**>(&run));
        if (result >= 0) result = function(type, L"RequestStop", UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr, reinterpret_cast<void**>(&requestStop));
        if (result >= 0) result = function(type, L"ToggleWindow", UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr, reinterpret_cast<void**>(&toggleWindow));
        if (result >= 0) result = function(type, L"ReadState", UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr, reinterpret_cast<void**>(&readState));
        if (result >= 0) result = function(type, L"SendCommand", UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr, reinterpret_cast<void**>(&sendCommand));
        writer(previousWriter);
        if (result < 0 || !run || !requestStop || !toggleWindow || !readState || !sendCommand) { status = -7; return 0; }
        stop = requestStop; toggle = toggleWindow;
        crimson::overlay::SetBridge(readState, sendCommand);
        // Only the developer harness sets this. A smoke run never loads settings,
        // opens network connections or sends physical output.
        if (stopRequested) { status = 2; return 0; }
        const int managedResult = run(size ? smokePath : nullptr, reinterpret_cast<void*>(&ReadyCallback));
        status = managedResult == 0 ? 2 : -9;
        crimson::overlay::SetBridge(nullptr, nullptr);
        crimson::overlay::SetVisibleForTest(false);
        toggle = nullptr; stop = nullptr;
    } catch (...) {
        status = -10;
    }
    return 0;
}
}

extern "C" __declspec(dllexport) int __stdcall CrimsonHueStatus() { return status.load(); }
extern "C" __declspec(dllexport) int __stdcall CrimsonHueCacheState() { return PayloadCacheState(); }
extern "C" __declspec(dllexport) int __stdcall CrimsonHueStop() {
    stopRequested = true;
    if (const auto request = stop.load()) return request();
    return 0;
}
extern "C" __declspec(dllexport) int __stdcall CrimsonHueToggleWindow() {
    crimson::overlay::Toggle(); return 1;
}
extern "C" __declspec(dllexport) int __stdcall CrimsonHueReadState(void* destination, int capacity) {
    try {
    crimson::overlay::RefreshBridge();
    crimson::overlay::Json state;
    if (!crimson::overlay::TryRead(state)) return 0;
    const auto bytes = state.dump();
    if (!destination || capacity < static_cast<int>(bytes.size())) return -static_cast<int>(bytes.size());
    memcpy(destination, bytes.data(), bytes.size()); return static_cast<int>(bytes.size());
    } catch (...) { return 0; }
}
extern "C" __declspec(dllexport) int __stdcall CrimsonHueSendCommand(const char* json, int size) {
    if (!json || size <= 0 || size > 16384) return 0;
    try { crimson::overlay::Command(crimson::overlay::Json::parse(json, json + size)); crimson::overlay::RefreshBridge(); return 1; }
    catch (...) { return 0; }
}
extern "C" __declspec(dllexport) unsigned long long __stdcall CrimsonHueRenderedFrames() { return crimson::overlay::RenderedFrames(); }
extern "C" __declspec(dllexport) const char* __stdcall CrimsonHueGraphicsStatus() { return crimson::overlay::GraphicsStatus(); }

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        module = instance;
        // The game's winmm ASI loader can also run in crashpad_handler.exe.
        // Initialize only in the game or our explicit developer smoke harness.
        if (!IsSupportedHost()) {
            status = 3; return TRUE;
        }
        // No CLR, UI, networking or waits under the Windows loader lock.
        const HANDLE thread = CreateThread(nullptr, 0, Worker, nullptr, 0, nullptr);
        if (!thread) status = -11;
        else CloseHandle(thread);
    }
    return TRUE;
}
