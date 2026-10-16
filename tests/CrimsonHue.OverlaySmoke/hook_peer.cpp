// Developer-only: each DLL owns a separate static MinHook, as the two ASIs do.
#include <windows.h>
#include <MinHook.h>
namespace {
using Call = int(WINAPI*)(int);
Call original{};
void* target{};
unsigned calls{};
int WINAPI Detour(int value) { ++calls; return original(value); }
}
extern "C" __declspec(dllexport) int __stdcall PreparePeer(void* function) {
    target = function; calls = 0;
    return MH_Initialize() == MH_OK && MH_CreateHook(target, reinterpret_cast<void*>(&Detour), reinterpret_cast<void**>(&original)) == MH_OK;
}
extern "C" __declspec(dllexport) int __stdcall EnablePeer() { return MH_EnableHook(target) == MH_OK; }
extern "C" __declspec(dllexport) int __stdcall PeerCalls() { return static_cast<int>(calls); }
extern "C" __declspec(dllexport) int __stdcall ClearPeer() {
    const auto result = MH_Uninitialize(); original = nullptr; target = nullptr; return result == MH_OK;
}
