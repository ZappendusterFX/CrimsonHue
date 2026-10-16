#include <windows.h>
#include <d3d12.h>
#include <dxgi1_4.h>
#include <wincodec.h>
#include <wrl/client.h>
#include <nlohmann/json.hpp>
#include <array>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <vector>

using Microsoft::WRL::ComPtr;
using Json = nlohmann::json;
void Require(bool condition, const char* message) { if (!condition) throw std::runtime_error(message); }
void Check(HRESULT hr) { Require(SUCCEEDED(hr), "D3D12 smoke operation failed"); }
int wmain(int argc, wchar_t** argv) {
    if (argc != 3) return 1;
    try {
        const auto asi = std::filesystem::absolute(argv[1]);
        const auto png = std::filesystem::absolute(argv[2]);
        std::filesystem::create_directories(png.parent_path());
        SetEnvironmentVariableW(L"CRIMSONHUE_ASI_SMOKE_PATH", png.c_str());
        SetEnvironmentVariableW(L"CRIMSONHUE_ASI_OVERLAY_SMOKE", L"1");
        const auto module = LoadLibraryExW(asi.c_str(), nullptr, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32);
        Require(module != nullptr, "ASI load failed");
        using Status = int(__stdcall*)(); using Read = int(__stdcall*)(void*, int); using Send = int(__stdcall*)(const char*, int);
        using Frames = unsigned long long(__stdcall*)(); using Graphics = const char*(__stdcall*)();
        const auto status = reinterpret_cast<Status>(GetProcAddress(module, "CrimsonHueStatus"));
        const auto stop = reinterpret_cast<Status>(GetProcAddress(module, "CrimsonHueStop"));
        const auto toggle = reinterpret_cast<Status>(GetProcAddress(module, "CrimsonHueToggleWindow"));
        const auto read = reinterpret_cast<Read>(GetProcAddress(module, "CrimsonHueReadState"));
        const auto send = reinterpret_cast<Send>(GetProcAddress(module, "CrimsonHueSendCommand"));
        const auto frames = reinterpret_cast<Frames>(GetProcAddress(module, "CrimsonHueRenderedFrames"));
        const auto graphics = reinterpret_cast<Graphics>(GetProcAddress(module, "CrimsonHueGraphicsStatus"));
        Require(status && stop && toggle && read && send && frames && graphics, "Missing ASI exports");
        const auto start = GetTickCount64();
        while (status() == 0 && GetTickCount64() - start < 20000) Sleep(20);
        Require(status() == 1, "Managed engine not ready");

        auto State = [&] { std::vector<char> bytes(256 * 1024); const int size = read(bytes.data(), static_cast<int>(bytes.size()));
            Require(size > 0, "No UI snapshot"); return Json::parse(bytes.data(), bytes.data() + size); };
        auto Command = [&](Json command) { const auto text = command.dump(); Require(send(text.data(), static_cast<int>(text.size())) == 1, "Command not accepted"); };
        auto state = State();
        Require(state.at("shortcut").at("key") == 'H', "Default shortcut must use H instead of a function key");
        Command({{"action", "shortcut"}, {"key", 'H'}, {"control", false}, {"shift", false}, {"alt", true}});
        Sleep(150); state = State();
        Require(state.at("shortcut").at("key") == 'H' && state.at("shortcut").at("alt") == true, "Shortcut command did not reach controller");
        Command({{"action", "shortcut"}, {"key", VK_CONTROL}, {"control", false}, {"shift", false}, {"alt", false}});
        Sleep(100); Require(State().at("shortcut").at("key") == 'H', "Invalid shortcut overwrote saved binding");
        Command({{"action", "shortcut"}, {"key", 'H'}, {"control", true}, {"shift", true}, {"alt", false}});
        Require(state.value("smoke", false) && state.value("demo", false) && !state.value("windowVisible", true), "Engine showed a WPF window or enabled live mode");
        Require(state.at("controls").size() == 28 && state.at("switches").size() == 2 && state.at("channels").size() == 6, "Missing mixer/layout controls");
        Command({{"action", "set"}, {"key", "Brightness"}, {"value", 23.0}});
        Command({{"action", "switch"}, {"key", "SeparateFrontRear"}, {"value", false}});
        Sleep(150); state = State();
        bool brightness = false, boundary = false;
        for (const auto& value : state.at("controls")) if (value.at("key") == "Brightness") brightness = value.at("value") == 23.0;
        for (const auto& value : state.at("switches")) if (value.at("key") == "SeparateFrontRear") boundary = !value.at("value").get<bool>();
        Require(brightness && boundary, "Overlay command did not reach live mapping");
        Command({{"action", "set"}, {"key", "Brightness"}, {"value", 200.0}}); Sleep(100);
        state = State(); for (const auto& value : state.at("controls")) if (value.at("key") == "Brightness") Require(value.at("value") == 23.0, "Out-of-range command changed mapping");
        Command({{"action", "reset"}, {"group", "Output"}}); Command({{"action", "reset"}, {"group", "Space"}}); Sleep(100);

        state = State(); Require(state.at("wallWashers").size() == 2, "Missing per-lamp wall calibration");
        for (const auto& washer : state.at("wallWashers")) {
            const auto id = washer.at("serviceId");
            Command({{"action", "wallWash"}, {"serviceId", id}, {"key", "enabled"}, {"value", true}});
            Command({{"action", "wallWash"}, {"serviceId", id}, {"key", "yaw"}, {"value", id == "synthetic-left" ? -60 : 60}});
            Command({{"action", "wallWash"}, {"serviceId", id}, {"key", "span"}, {"value", 60}});
        }
        Command({{"action", "wallWash"}, {"serviceId", "synthetic-left"}, {"key", "elevation"}, {"index", 0}, {"value", -30}});
        Command({{"action", "wallPreview"}, {"key", "enabled"}, {"value", true}});
        Command({{"action", "wallPreview"}, {"key", "yaw"}, {"value", -75}});
        Sleep(200); state = State();
        Require(state.value("wallPreview", false) && !state.value("canIdentify", true), "Synthetic preview enabled physical testing");
        Require(state.at("wallWashers")[0].at("yaw") == -60 && state.at("wallWashers")[0].at("segments")[0].at("elevation") == -30,
            "Lamp or segment calibration command did not apply");
        bool leftLit = false;
        for (const auto& channel : state.at("channels")) {
            const auto rgb = channel.at("rgb");
            if (channel.at("id").get<int>() < 3) leftLit |= rgb.at("x").get<double>() > 0;
            else Require(rgb.at("x") == 0 && rgb.at("y") == 0 && rgb.at("z") == 0, "Calibrated preview crossed the left/right boundary");
        }
        Require(leftLit, "Outward wall preview received no light");
        Command({{"action", "wallWash"}, {"serviceId", "synthetic-left"}, {"key", "yaw"}, {"value", 400}});
        Command({{"action", "identify"}, {"channel", 0}});
        Sleep(100); state = State();
        Require(state.at("wallWashers")[0].at("yaw") == -60 && !state.value("canStop", true), "Invalid calibration or identify escaped smoke isolation");
        Command({{"action", "wallWash"}, {"serviceId", "synthetic-right"}, {"key", "reset"}});
        Sleep(100); Require(!State().at("wallWashers")[1].at("enabled").get<bool>(), "Reset did not restore Hue point mapping");
        Command({{"action", "wallWash"}, {"serviceId", "synthetic-right"}, {"key", "enabled"}, {"value", true}});
        Command({{"action", "wallWash"}, {"serviceId", "synthetic-right"}, {"key", "yaw"}, {"value", 60}});
        Command({{"action", "wallPreview"}, {"key", "yaw"}, {"value", 75}});
        Sleep(100); state = State();
        for (const auto& channel : state.at("channels")) if (channel.at("id").get<int>() < 3)
            Require(channel.at("rgb").at("x") == 0, "Right preview source leaked to left washer");
        Command({{"action", "wallPreview"}, {"key", "yaw"}, {"value", -75}});
        std::cout << "PASS wall area/segment commands, direction preview, strict separation and physical-output isolation.\n";

        const auto protocol = reinterpret_cast<Status>(GetProcAddress(module, "CrimsonHueUiProtocol"));
        Require(protocol && protocol() == 1 && frames() == 0 && toggle() == 0, "Controller still owns graphics/window input");
        Require(read(nullptr, -1) == 0 && read(nullptr, 256 * 1024 + 1) == 0 && send(nullptr, 1) == 0,
            "Invalid ABI buffers accepted");
        stop(); const auto stopStart = GetTickCount64();
        while (status() == 1 && GetTickCount64() - stopStart < 5000) Sleep(20);
        Require(status() == 2, "Engine shutdown failed");
        std::cout << "PASS headless controller ABI, hidden WPF, 30 controls, washer commands, isolated preview, validation and host-preserving shutdown.\n";
        return 0;
    } catch (const std::exception& error) { std::cerr << error.what() << '\n'; return 2; }
}