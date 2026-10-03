// Synthetic coexistence control: a second DLL owns a separate static MinHook,
// hooks the same DXGI methods and forwards them. No game or telemetry code.
#include <windows.h>
#include <dxgi1_4.h>
#include <wrl/client.h>
#include <MinHook.h>
#include <atomic>

using Microsoft::WRL::ComPtr;
namespace {
using Create = HRESULT(STDMETHODCALLTYPE*)(IDXGIFactory2*, IUnknown*, HWND, const DXGI_SWAP_CHAIN_DESC1*,
    const DXGI_SWAP_CHAIN_FULLSCREEN_DESC*, IDXGIOutput*, IDXGISwapChain1**);
using Present = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain*, UINT, UINT);
Create create{};
Present present{};
std::atomic<unsigned> creations{}, presentations{};
std::atomic<bool> failed{};
HRESULT STDMETHODCALLTYPE OnPresent(IDXGISwapChain* chain, UINT sync, UINT flags) {
    ++presentations; return present(chain, sync, flags);
}
HRESULT STDMETHODCALLTYPE OnCreate(IDXGIFactory2* factory, IUnknown* device, HWND window, const DXGI_SWAP_CHAIN_DESC1* desc,
    const DXGI_SWAP_CHAIN_FULLSCREEN_DESC* fullscreen, IDXGIOutput* output, IDXGISwapChain1** chain) {
    const auto result = create(factory, device, window, desc, fullscreen, output, chain);
    if (SUCCEEDED(result) && chain && *chain) {
        ++creations;
        if (!present) {
            auto** table = *reinterpret_cast<void***>(*chain);
            if (MH_CreateHook(table[8], reinterpret_cast<void*>(&OnPresent), reinterpret_cast<void**>(&present)) != MH_OK ||
                MH_EnableHook(table[8]) != MH_OK) failed = true;
        }
    }
    return result;
}
}
extern "C" __declspec(dllexport) int __stdcall InstallForeignOverlay() {
    if (MH_Initialize() != MH_OK) return 0;
    ComPtr<IDXGIFactory2> factory;
    if (FAILED(CreateDXGIFactory1(IID_PPV_ARGS(&factory)))) return 0;
    auto** table = *reinterpret_cast<void***>(factory.Get());
    return MH_CreateHook(table[15], reinterpret_cast<void*>(&OnCreate), reinterpret_cast<void**>(&create)) == MH_OK &&
        MH_EnableHook(table[15]) == MH_OK;
}
extern "C" __declspec(dllexport) int __stdcall ForeignOverlayPassed() {
    return creations > 0 && presentations > 5 && !failed;
}
