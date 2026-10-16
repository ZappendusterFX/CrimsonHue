#include <windows.h>
#include <filesystem>
#include <iostream>
#include <stdexcept>
volatile int factor = 7;
__declspec(noinline) int WINAPI Target(int value) { return value * factor + 3; }
void Require(bool value, const char* reason) { if (!value) throw std::runtime_error(reason); }
struct Peer {
    using Prepare = int(__stdcall*)(void*);
    using Action = int(__stdcall*)();
    Prepare prepare{}; Action enable{}, calls{}, clear{};
    explicit Peer(const std::filesystem::path& path) {
        const auto module = LoadLibraryExW(path.c_str(), nullptr, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32);
        Require(module != nullptr, "Peer DLL load failed");
        prepare = reinterpret_cast<Prepare>(GetProcAddress(module, "PreparePeer"));
        enable = reinterpret_cast<Action>(GetProcAddress(module, "EnablePeer"));
        calls = reinterpret_cast<Action>(GetProcAddress(module, "PeerCalls"));
        clear = reinterpret_cast<Action>(GetProcAddress(module, "ClearPeer"));
        Require(prepare && enable && calls && clear, "Peer exports missing");
    }
};
int wmain(int, wchar_t** argv) {
    try {
        const auto directory = std::filesystem::absolute(argv[0]).parent_path();
        Peer first(directory / L"CrimsonHue.HookPeerA.dll"), second(directory / L"CrimsonHue.HookPeerB.dll");
        using Call = int(WINAPI*)(int); Call volatile invoke = &Target;
        auto* target = reinterpret_cast<void*>(&Target);
        // Deterministic interleaving: both trampolines capture the unpatched entry.
        Require(first.prepare(target) && second.prepare(target), "Prepare failed");
        Require(first.enable() && second.enable(), "Enable failed");
        Require(invoke(11) == 80, "Original function behavior changed");
        std::cout << "Interleaved Create/Create/Enable/Enable: first=" << first.calls() << " second=" << second.calls() << '\n';
        Require(first.calls() == 0 && second.calls() == 1, "Expected lost hook was not reproduced");
        Require(second.clear() && first.clear(), "Cleanup failed");
        // Existing before/after tests cover only this serialized schedule.
        Require(first.prepare(target) && first.enable() && second.prepare(target) && second.enable(), "Serial install failed");
        Require(invoke(11) == 80, "Serial original function behavior changed");
        std::cout << "Serialized Create/Enable/Create/Enable: first=" << first.calls() << " second=" << second.calls() << '\n';
        Require(first.calls() == 1 && second.calls() == 1, "Serialized chain failed");
        Require(second.clear() && first.clear(), "Serial cleanup failed");
        std::cout << "PASS deterministic independent-MinHook lost-hook reproduction and serialized control; no game/network/lamps.\n";
        return 0;
    } catch (const std::exception& error) { std::cerr << error.what() << '\n'; return 2; }
}
