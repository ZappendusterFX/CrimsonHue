#include "controller_api.h"
#include <filesystem>
#include <fstream>
#include <mutex>

namespace crimson::overlay {
namespace {
std::mutex logMutex;
std::ofstream logFile;
ULONGLONG start{};
size_t written{};
}
void InitializeDiagnostics() noexcept {
    try {
        std::lock_guard lock(logMutex);
        if (start) return;
        start = GetTickCount64();
        wchar_t smokePath[32768]{}, local[32768]{};
        const auto smokeSize = GetEnvironmentVariableW(L"CRIMSONHUE_ASI_SMOKE_PATH", smokePath, 32768);
        const auto localSize = GetEnvironmentVariableW(L"LOCALAPPDATA", local, 32768);
        if (smokeSize >= 32768 || localSize >= 32768 || (!smokeSize && !localSize)) return;
        const auto directory = smokeSize ? std::filesystem::path(smokePath).parent_path() :
            std::filesystem::path(local) / L"CrimsonHue" / L"diagnostics";
        std::filesystem::create_directories(directory);
        logFile.open(directory / ("overlay-" + std::to_string(GetCurrentProcessId()) + "-" + std::to_string(start) + ".log"));
    } catch (...) { }
}
void Diagnostic(const std::string& message) noexcept {
    // Only native fixed status/error codes are passed here. No UI snapshots,
    // addresses, settings, credentials or managed exception text are logged.
    try {
        std::lock_guard lock(logMutex);
        if (!logFile || written >= 65536 || message.size() > 512) return;
        logFile << (GetTickCount64() - start) << " ms | " << message << '\n';
        logFile.flush(); written += message.size() + 32;
    } catch (...) { }
}
}
