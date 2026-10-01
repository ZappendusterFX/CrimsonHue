#include <windows.h>
#include <filesystem>
#include <iostream>
#include <string>

int wmain(int argc, wchar_t** argv) {
    if (argc < 3 || argc > 4) { std::wcerr << L"Usage: AsiSmoke ASI_PATH PNG_PATH [--stop-early|--stop-ready|--missing-payload|--missing-runtime|--expect-cache|--expect-repair|--ignored-host]\n"; return 1; }
    const std::wstring mode = argc == 4 ? argv[3] : L"";
    if (!mode.empty() && mode != L"--stop-early" && mode != L"--stop-ready" && mode != L"--missing-payload" &&
        mode != L"--missing-runtime" && mode != L"--expect-cache" && mode != L"--expect-repair" && mode != L"--ignored-host") return 1;
    auto asi = std::filesystem::absolute(argv[1]);
    const auto png = std::filesystem::absolute(argv[2]);
    if (!SetEnvironmentVariableW(L"CRIMSONHUE_ASI_SMOKE_PATH", png.c_str())) return 2;
    SetEnvironmentVariableW(L"CRIMSONHUE_ASI_SMOKE_DOTNET_ROOT", nullptr);
    if (mode == L"--missing-runtime") {
        const auto emptyRoot = png.parent_path() / L"missing-dotnet";
        std::filesystem::create_directories(emptyRoot);
        SetEnvironmentVariableW(L"CRIMSONHUE_ASI_SMOKE_DOTNET_ROOT", emptyRoot.c_str());
    }
    if (mode == L"--missing-payload") {
        // Mutate a new developer-only copy; never overwrite the packaged ASI.
        const auto copy = png.parent_path() / (L"bad-resource-" + std::to_wstring(GetTickCount64())) / L"CrimsonHue.asi";
        std::filesystem::create_directories(copy.parent_path());
        std::filesystem::copy_file(asi, copy);
        const auto update = BeginUpdateResourceW(copy.c_str(), FALSE);
        if (!update || !UpdateResourceW(update, RT_RCDATA, MAKEINTRESOURCEW(101), 0, nullptr, 0)) return 2;
        if (!EndUpdateResourceW(update, FALSE)) return 2;
        asi = copy;
    }
    const auto started = GetTickCount64();
    const auto module = LoadLibraryExW(asi.c_str(), nullptr,
                                       LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32);
    if (!module) { std::wcerr << L"ASI could not load: " << GetLastError() << L'\n'; return 3; }
    using status_fn = int(__stdcall*)();
    using stop_fn = int(__stdcall*)();
    const auto status = reinterpret_cast<status_fn>(GetProcAddress(module, "CrimsonHueStatus"));
    const auto stop = reinterpret_cast<stop_fn>(GetProcAddress(module, "CrimsonHueStop"));
    const auto cache = reinterpret_cast<status_fn>(GetProcAddress(module, "CrimsonHueCacheState"));
    if (!status || !stop || !cache || !GetProcAddress(module, "CrimsonHueToggleWindow")) return 4;
    if (mode == L"--ignored-host") {
        if (status() != 3 || cache() != 0) return 5;
        std::wcout << L"PASS: non-game helper process ignored without extracting or initializing the plugin.\n"; return 0;
    }
    bool ready = false;
    if (mode == L"--stop-early") stop();
    while (GetTickCount64() - started < 45000) {
        const int current = status();
        if (current == 1 && !ready) {
            ready = true;
            if (mode == L"--stop-ready") stop();
        }
        if (current < 0) {
            if ((mode == L"--missing-payload" && current == -12) || (mode == L"--missing-runtime" && current == -3)) {
                std::wcout << L"PASS: " << mode << L" reported without terminating the native host.\n"; return 0;
            }
            std::wcerr << L"ASI status: " << current << L'\n'; return 5;
        }
        if (current == 2) {
            if (mode == L"--stop-early" || (mode == L"--stop-ready" && ready)) {
                std::wcout << L"PASS: " << mode << L" reached stopped status without terminating the native host.\n"; return 0;
            }
            if (mode == L"--missing-payload") return 6;
            if (mode == L"--missing-runtime" || (mode == L"--expect-cache" && cache() != 2) || (mode == L"--expect-repair" && cache() != 3)) return 6;
            if (!ready || !std::filesystem::is_regular_file(png)) return 6;
            for (const auto* name : {L"ambient", L"color", L"space", L"setup", L"raw"}) {
                const auto panel = png.parent_path() / (png.stem().wstring() + L"-" + name + L".png");
                if (!std::filesystem::is_regular_file(panel)) return 7;
            }
            std::wcout << L"PASS: native ASI loaded managed UI in PID " << GetCurrentProcessId()
                       << L"; six synthetic panels rendered; clean shutdown; cache state " << cache() << L".\n";
            // An ASI must not terminate its host when the WPF application stops.
            Sleep(100);
            return 0;
        }
        Sleep(20);
    }
    stop();
    std::wcerr << L"ASI smoke timed out\n";
    return 8;
}
