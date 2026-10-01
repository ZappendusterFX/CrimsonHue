#include <windows.h>
#include <hostfxr.h>
#include <coreclr_delegates.h>
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
        const auto payload = directory / L"CrimsonHue";
        const auto hostPath = payload / L"hostfxr.dll";
        const auto assembly = payload / L"CrimsonHue.dll";
        // Resolve only beside this ASI; never depend on the game's working directory.
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
        hostfxr_initialize_parameters parameters{sizeof(parameters), host.c_str(), payload.c_str()};
        const wchar_t* arguments[]{assembly.c_str()};
        hostfxr_handle context = nullptr;
        // The command-line hosting API supports an app-local runtime. It prepares
        // dependencies without executing Main or launching a separate process.
        int result = initialize(1, arguments, &parameters, &context);
        if (result < 0 || !context) {
            if (context) close(context);
            writer(previousWriter); status = -5; return 0;
        }
        get_function_pointer_fn function = nullptr;
        result = getDelegate(context, hdt_get_function_pointer, reinterpret_cast<void**>(&function));
        close(context);
        if (result < 0 || !function) { writer(previousWriter); status = -6; return 0; }
        run_fn run = nullptr;
        command_fn requestStop = nullptr;
        command_fn toggleWindow = nullptr;
        constexpr auto type = L"CrimsonHue.App.AsiEntry, CrimsonHue";
        result = function(type, L"Run", UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr, reinterpret_cast<void**>(&run));
        if (result >= 0) result = function(type, L"RequestStop", UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr, reinterpret_cast<void**>(&requestStop));
        if (result >= 0) result = function(type, L"ToggleWindow", UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr, reinterpret_cast<void**>(&toggleWindow));
        writer(previousWriter);
        if (result < 0 || !run || !requestStop || !toggleWindow) { status = -7; return 0; }
        stop = requestStop; toggle = toggleWindow;
        // Only the developer harness sets this. A smoke run never loads settings,
        // opens network connections or sends physical output.
        wchar_t smokePath[32768]{};
        const auto size = GetEnvironmentVariableW(L"CRIMSONHUE_ASI_SMOKE_PATH", smokePath, 32768);
        if (size >= 32768) { status = -8; return 0; }
        if (stopRequested) { status = 2; return 0; }
        const int managedResult = run(size ? smokePath : nullptr, reinterpret_cast<void*>(&ReadyCallback));
        status = managedResult == 0 ? 2 : -9;
        toggle = nullptr; stop = nullptr;
    } catch (...) {
        status = -10;
    }
    return 0;
}
}

extern "C" __declspec(dllexport) int __stdcall CrimsonHueStatus() { return status.load(); }
extern "C" __declspec(dllexport) int __stdcall CrimsonHueStop() {
    stopRequested = true;
    if (const auto request = stop.load()) return request();
    return 0;
}
extern "C" __declspec(dllexport) int __stdcall CrimsonHueToggleWindow() {
    if (const auto command = toggle.load()) return command();
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        module = instance;
        // No CLR, UI, networking or waits under the Windows loader lock.
        const HANDLE thread = CreateThread(nullptr, 0, Worker, nullptr, 0, nullptr);
        if (!thread) status = -11;
        else CloseHandle(thread);
    }
    return TRUE;
}
