#include <windows.h>
#include <d3d12.h>
#include <dxgi1_4.h>
#include <wincodec.h>
#include <wrl/client.h>
#include <nlohmann/json.hpp>
#include <array>
#include <cmath>
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
        auto Control = [](const Json& view, const char* key) {
            for (const auto& field : view.at("controls")) if (field.at("key") == key) return field.at("value").get<double>();
            throw std::runtime_error(std::string("Controller snapshot omitted control: ") + key);
        };
        auto Await = [&](const auto& predicate, const char* failure) {
            const auto begin = GetTickCount64();
            auto view = State();
            while (!predicate(view) && GetTickCount64() - begin < 3000) { Sleep(10); view = State(); }
            Require(predicate(view), failure); return view;
        };
        auto state = State();
        Require(state.value("bridgeConnected", false) && state.value("bridgePhase", std::string{}) == "connected" &&
            !state.at("areas").empty() && !state.at("areas")[0].at("name").get<std::string>().empty(),
            "Connected status or named Entertainment areas missing from controller snapshot");
        Require(state.at("shortcut").at("key") == 'H', "Default shortcut must use H instead of a function key");
        Command({{"action", "shortcut"}, {"key", 'H'}, {"control", false}, {"shift", false}, {"alt", true}});
        Sleep(150); state = State();
        Require(state.at("shortcut").at("key") == 'H' && state.at("shortcut").at("alt") == true, "Shortcut command did not reach controller");
        Command({{"action", "shortcut"}, {"key", VK_CONTROL}, {"control", false}, {"shift", false}, {"alt", false}});
        Sleep(100); Require(State().at("shortcut").at("key") == 'H', "Invalid shortcut overwrote saved binding");
        Command({{"action", "shortcut"}, {"key", 'H'}, {"control", true}, {"shift", true}, {"alt", false}});
        Require(state.value("smoke", false) && state.value("demo", false) && !state.value("windowVisible", true), "Engine showed a WPF window or enabled live mode");
        Require(state.at("controls").size() == 31 && state.at("switches").empty() && state.at("channels").size() == 6, "Missing mixer/layout controls or obsolete axis switches remain");
        bool sphereDefault = false;
        for (const auto& value : state.at("controls")) if (value.at("key") == "SourceDiscRadiusDegrees")
            sphereDefault = value.at("value") == 30.0 && value.at("initial") == 30.0 && value.at("min") == 1.0 && value.at("max") == 180.0 && value.at("group") == "Space";
        Require(sphereDefault, "Spherical radius default or range missing from controller snapshot");

        // Exercise the real native-to-managed command ABI. Profile selection
        // must alter effective mapping while retaining the entire custom setup.
        Require(state.at("lighting").at("recommended") == true && state.at("lighting").at("revision") == 1 &&
            state.at("lighting").at("response") == "Balanced" && state.at("lighting").at("hasCustom") == true &&
            Control(state, "Brightness") == 85 && Control(state, "SmoothingMs") == 250 &&
            Control(state, "AmbientSmoothingMs") == 600 && Control(state, "Spread") == 4 &&
            Control(state, "SourceDiscSoftness") == 20, "New isolated controller did not start with the recommended Balanced profile");
        Require(state.at("assessment").at("code") == "demo" && state.at("assessment").at("warning") == false &&
            !state.at("assessment").at("summary").get<std::string>().empty() &&
            state.at("assessment").at("detail").get<std::string>().find("no physical output") != std::string::npos,
            "Assessment snapshot failed to distinguish synthetic preview from physical lighting");
        Command({{"action", "lighting"}, {"key", "Brightness"}, {"value", .32}});
        Command({{"action", "lighting"}, {"key", "LocalAmount"}, {"value", 1.35}});
        Command({{"action", "lighting"}, {"key", "BackgroundAmount"}, {"value", .22}});
        Command({{"action", "lighting"}, {"key", "Response"}, {"value", 2}});
        state = Await([&](const Json& view) { return view.at("lighting").at("response") == "Calm" &&
            Control(view, "Brightness") == 32 && Control(view, "LocalStrength") == 135 && Control(view, "AmbientOutput") == 22 &&
            Control(view, "SmoothingMs") == 500 && Control(view, "AmbientSmoothingMs") == 1000; },
            "Basic lighting commands did not change effective mapping or Calm response");
        const auto validLighting = state.at("lighting");
        const auto validControls = state.at("controls");
        Command({{"action", "lighting"}, {"key", "Brightness"}, {"value", 1.01}});
        Command({{"action", "lighting"}, {"key", "LocalAmount"}, {"value", -.1}});
        Command({{"action", "lighting"}, {"key", "Response"}, {"value", 99}});
        // This valid command is a FIFO barrier: its distinctive response proves
        // all earlier invalid commands have been processed before assertions.
        Command({{"action", "shortcut"}, {"key", 'H'}, {"control", false}, {"shift", true}, {"alt", true}});
        state = Await([](const Json& view) { return view.at("shortcut").at("control") == false &&
            view.at("shortcut").at("alt") == true && view.at("shortcut").at("shift") == true; },
            "Invalid-lighting command barrier did not complete");
        Require(state.at("lighting") == validLighting && state.at("controls") == validControls,
            "Invalid basic lighting value or response changed effective profile");
        Command({{"action", "profile"}, {"recommended", false}});
        state = Await([&](const Json& view) { return view.at("lighting").at("recommended") == false &&
            Control(view, "Brightness") == 85 && Control(view, "SmoothingMs") == 100; },
            "Returning to Custom lost its untouched defaults");
        Command({{"action", "set"}, {"key", "Brightness"}, {"value", 23}});
        Command({{"action", "set"}, {"key", "AmbientCutoff"}, {"value", 28.488}});
        Command({{"action", "set"}, {"key", "OutputGamma"}, {"value", .35}});
        Command({{"action", "set"}, {"key", "RedGain"}, {"value", 2}});
        Command({{"action", "set"}, {"key", "GreenGain"}, {"value", 1.6}});
        Command({{"action", "set"}, {"key", "BlueGain"}, {"value", .4}});
        Command({{"action", "set"}, {"key", "Saturation"}, {"value", 150}});
        Command({{"action", "set"}, {"key", "HueShiftDegrees"}, {"value", 47}});
        Command({{"action", "set"}, {"key", "AmbientTintHue"}, {"value", 210}});
        Command({{"action", "set"}, {"key", "AmbientTintSaturation"}, {"value", 65}});
        state = Await([&](const Json& view) { return Control(view, "AmbientTintSaturation") == 65 &&
            Control(view, "HueShiftDegrees") == 47 && std::abs(Control(view, "AmbientCutoff") - 28.488) < 1e-9; },
            "Custom cutoff and color controls did not reach mapping");
        const auto customControls = state.at("controls");
        Command({{"action", "profile"}, {"recommended", true}});
        state = Await([&](const Json& view) { return view.at("lighting").at("recommended") == true &&
            Control(view, "AmbientCutoff") == 0; }, "Recommended profile retained the custom Ambient black threshold");
        Require(Control(state, "Brightness") == 23 && state.at("lighting").at("brightness") == .23 &&
            Control(state, "LocalStrength") == 135 && Control(state, "AmbientOutput") == 22 &&
            Control(state, "OutputGamma") == 1 && Control(state, "RedGain") == 1 &&
            Control(state, "GreenGain") == 1 && Control(state, "BlueGain") == 1 &&
            Control(state, "Saturation") == 100 && Control(state, "HueShiftDegrees") == 0 &&
            Control(state, "AmbientTintSaturation") == 0,
            "Recommended profile changed master cap or retained custom gamma/RGB/color correction");
        Command({{"action", "profile"}, {"recommended", false}});
        state = Await([&](const Json& view) { return view.at("lighting").at("recommended") == false &&
            view.at("controls") == customControls; }, "Custom mapping was not restored exactly after Recommended");
        Command({{"action", "lighting"}, {"key", "Brightness"}, {"value", .8}});
        Command({{"action", "shortcut"}, {"key", 'H'}, {"control", true}, {"shift", true}, {"alt", false}});
        state = Await([](const Json& view) { return view.at("shortcut").at("control") == true &&
            view.at("shortcut").at("alt") == false; }, "Custom-mode rejection barrier did not complete");
        Require(state.at("lighting").at("recommended") == false && state.at("controls") == customControls,
            "Basic command silently replaced Custom tuning");
        for (const auto* group : {"Output", "Ambient", "Color", "Space"}) Command({{"action", "reset"}, {"group", group}});
        Command({{"action", "profile"}, {"recommended", true}});
        state = Await([&](const Json& view) { return view.at("lighting").at("recommended") == true &&
            Control(view, "Brightness") == 85 && Control(view, "AmbientCutoff") == 0 &&
            Control(view, "SourceDiscRadiusDegrees") == 30; }, "Profile smoke cleanup failed");
        Command({{"action", "lighting"}, {"key", "PointLightStrength"}, {"value", .4}});
        Command({{"action", "lighting"}, {"key", "SpotLightStrength"}, {"value", 1.6}});
        Command({{"action", "lighting"}, {"key", "PlayerLanternStrength"}, {"value", .05}});
        state = Await([&](const Json& view) { return Control(view,"PointLightStrength")==40 &&
            Control(view,"SpotLightStrength")==160 && Control(view,"PlayerLanternStrength")==5; }, "Recommended source weights failed");
        Command({{"action", "lighting"}, {"key", "PointLightStrength"}, {"value", 2.01}});
        Command({{"action", "lighting"}, {"key", "PlayerLanternStrength"}, {"value", 1.01}});
        Sleep(100); Require(State().at("lighting")==state.at("lighting"),"Invalid weights changed Recommended");
        Command({{"action", "profile"}, {"recommended", false}});
        state = Await([](const Json& view) { return view.at("lighting").at("recommended") == false; }, "Custom mode failed");
        Command({{"action", "lighting"}, {"key", "PointLightStrength"}, {"value", .7}});
        Command({{"action", "lighting"}, {"key", "SpotLightStrength"}, {"value", 1.2}});
        Command({{"action", "lighting"}, {"key", "PlayerLanternStrength"}, {"value", .02}});
        state = Await([&](const Json& view) { return Control(view,"PointLightStrength")==70 &&
            Control(view,"SpotLightStrength")==120 && Control(view,"PlayerLanternStrength")==2; }, "Custom source weights failed");
        Command({{"action", "profile"}, {"recommended", true}});
        state = Await([&](const Json& view) { return Control(view,"PointLightStrength")==40 &&
            Control(view,"SpotLightStrength")==160 && Control(view,"PlayerLanternStrength")==5 &&
            view.at("lighting").at("lanternMatches")==0; }, "Recommended weights were lost");
        Command({{"action", "lighting"}, {"key", "PointLightStrength"}, {"value", 1}});
        Command({{"action", "lighting"}, {"key", "SpotLightStrength"}, {"value", 1}});
        Command({{"action", "lighting"}, {"key", "PlayerLanternStrength"}, {"value", .1}});
        state = Await([&](const Json& view) {return Control(view,"PointLightStrength")==100 && Control(view,"PlayerLanternStrength")==10;},"Weight cleanup failed");
        std::cout << "PASS Recommended/Custom ABI, separate source and own-lantern weights, invalid-command rejection, neutral color, master-cap preservation, full custom restore and preview assessment.\n";

        Command({{"action", "set"}, {"key", "Brightness"}, {"value", 23.0}});
        Command({{"action", "set"}, {"key", "SourceDiscRadiusDegrees"}, {"value", 130.0}});
        Sleep(150); state = State();
        bool brightness = false, sphere = false;
        for (const auto& value : state.at("controls")) if (value.at("key") == "Brightness") brightness = value.at("value") == 23.0;
        for (const auto& value : state.at("controls")) if (value.at("key") == "SourceDiscRadiusDegrees") sphere = value.at("value") == 130.0;
        Require(brightness && sphere && state.at("lighting").at("recommended") == false,
            "Advanced overlay command did not reach live mapping and switch to Custom");
        Command({{"action", "set"}, {"key", "Brightness"}, {"value", 200.0}}); Sleep(100);
        state = State(); for (const auto& value : state.at("controls")) if (value.at("key") == "Brightness") Require(value.at("value") == 23.0, "Out-of-range command changed mapping");
        Command({{"action", "set"}, {"key", "SourceDiscRadiusDegrees"}, {"value", 181.0}}); Sleep(100);
        state = State(); for (const auto& value : state.at("controls")) if (value.at("key") == "SourceDiscRadiusDegrees") Require(value.at("value") == 130.0, "Out-of-range spherical radius changed mapping");
        Command({{"action", "reset"}, {"group", "Output"}}); Command({{"action", "reset"}, {"group", "Space"}}); Sleep(100);
        state = State();
        Require(Control(state, "SourceDiscRadiusDegrees") == 30 && Control(state, "Spread") == 4 &&
            Control(state, "SourceDiscSoftness") == 20, "Space reset did not restore the focused defaults");

        state = State(); Require(state.at("wallWashers").size() == 2, "Missing per-lamp wall calibration");
        for (const auto& washer : state.at("wallWashers")) {
            Require(washer.at("segmentCount") == 3 && washer.at("segments").size() == 3 &&
                washer.at("removedSegmentCount") == 0, "Imported Hue segment summary is incorrect");
            for (const auto& segment : washer.at("segments"))
                Require(segment.at("needsReview") == false, "Unchanged Hue segment unexpectedly needs review");
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
        const auto calibratedLeft = state.at("wallWashers")[0];
        Command({{"action", "wallWash"}, {"serviceId", "synthetic-left"}, {"key", "enabled"}, {"value", false}});
        Sleep(100); state = State();
        const auto pointLeft = state.at("wallWashers")[0];
        Require(pointLeft.at("enabled") == false && pointLeft.at("yaw") == calibratedLeft.at("yaw") &&
            pointLeft.at("horizontalSpan") == calibratedLeft.at("horizontalSpan") &&
            pointLeft.at("segments") == calibratedLeft.at("segments"), "Point mode discarded saved wall placement");
        Command({{"action", "wallWash"}, {"serviceId", "synthetic-left"}, {"key", "enabled"}, {"value", true}});
        Sleep(100); state = State();
        const auto restoredLeft = state.at("wallWashers")[0];
        Require(restoredLeft.at("enabled") == true && restoredLeft.at("yaw") == calibratedLeft.at("yaw") &&
            restoredLeft.at("horizontalSpan") == calibratedLeft.at("horizontalSpan") &&
            restoredLeft.at("segments") == calibratedLeft.at("segments"), "Wall mode did not restore saved placement");
        bool leftLit = false;
        for (const auto& channel : state.at("channels")) {
            const auto rgb = channel.at("rgb");
            if (channel.at("id").get<int>() < 3) leftLit |= rgb.at("x").get<double>() > 0;
            else Require(rgb.at("x") == 0 && rgb.at("y") == 0 && rgb.at("z") == 0, "Calibrated preview reached beyond its spherical radius");
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
        std::cout << "PASS segment summary, Point/Wall placement preservation, spherical direction preview and physical-output isolation.\n";

        const auto protocol = reinterpret_cast<Status>(GetProcAddress(module, "CrimsonHueUiProtocol"));
        Require(protocol && protocol() == 1 && frames() == 0 && toggle() == 0, "Controller still owns graphics/window input");
        Require(read(nullptr, -1) == 0 && read(nullptr, 256 * 1024 + 1) == 0 && send(nullptr, 1) == 0,
            "Invalid ABI buffers accepted");
        stop(); const auto stopStart = GetTickCount64();
        while (status() == 1 && GetTickCount64() - stopStart < 5000) Sleep(20);
        Require(status() == 2, "Engine shutdown failed");
        std::cout << "PASS headless controller ABI, hidden WPF, 31 advanced controls, lighting profiles/assessment, spherical radius and washer commands, isolated preview, validation and host-preserving shutdown.\n";
        return 0;
    } catch (const std::exception& error) { std::cerr << error.what() << '\n'; return 2; }
}
