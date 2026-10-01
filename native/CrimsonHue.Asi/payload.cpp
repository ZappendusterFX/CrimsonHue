#include "payload.h"
#include <setupapi.h>
#include <shlobj.h>
#include <bcrypt.h>
#include <atomic>
#include <array>
#include <fstream>
#include <map>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
namespace fs = std::filesystem;
std::atomic<int> cacheState{0};
constexpr unsigned int CabinetResource = 101;
constexpr unsigned int ManifestResource = 102;

void Check(bool value) { if (!value) throw std::runtime_error("Payload validation failed."); }

class Sha256 {
    BCRYPT_ALG_HANDLE algorithm = nullptr;
    BCRYPT_HASH_HANDLE hash = nullptr;
public:
    Sha256() {
        Check(BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) >= 0);
        if (BCryptCreateHash(algorithm, &hash, nullptr, 0, nullptr, 0, 0) < 0) {
            BCryptCloseAlgorithmProvider(algorithm, 0);
            throw std::runtime_error("Hash initialization failed.");
        }
    }
    ~Sha256() { if (hash) BCryptDestroyHash(hash); if (algorithm) BCryptCloseAlgorithmProvider(algorithm, 0); }
    void Add(const void* bytes, ULONG size) { Check(BCryptHashData(hash, const_cast<PUCHAR>(static_cast<const UCHAR*>(bytes)), size, 0) >= 0); }
    std::string Finish() {
        std::array<UCHAR, 32> digest{};
        Check(BCryptFinishHash(hash, digest.data(), static_cast<ULONG>(digest.size()), 0) >= 0);
        std::string text;
        constexpr char hex[] = "0123456789abcdef";
        for (const auto value : digest) { text += hex[value >> 4]; text += hex[value & 15]; }
        return text;
    }
};

std::string FileHash(const fs::path& path) {
    std::ifstream input(path, std::ios::binary);
    Check(input.good());
    Sha256 hash;
    std::array<char, 65536> buffer{};
    while (input.read(buffer.data(), buffer.size()) || input.gcount()) hash.Add(buffer.data(), static_cast<ULONG>(input.gcount()));
    Check(input.eof());
    return hash.Finish();
}

std::pair<const void*, DWORD> Resource(HMODULE module, unsigned int id) {
    const auto resource = FindResourceW(module, MAKEINTRESOURCEW(id), RT_RCDATA);
    Check(resource != nullptr);
    const auto handle = LoadResource(module, resource);
    const auto size = SizeofResource(module, resource);
    const auto bytes = LockResource(handle);
    Check(bytes && size && size < 32 * 1024 * 1024);
    return {bytes, size};
}

fs::path SafeRelative(const std::wstring& name) {
    Check(!name.empty() && name.find(L':') == std::wstring::npos);
    const fs::path path(name);
    Check(!path.has_root_path());
    for (const auto& part : path) Check(part != L"..");
    const auto relative = path.lexically_normal();
    Check(relative != L"." && relative.extension() != L".exe");
    return relative;
}

struct File { std::uintmax_t size; std::string hash; };
struct Insensitive { bool operator()(const std::wstring& a, const std::wstring& b) const { return _wcsicmp(a.c_str(), b.c_str()) < 0; } };
using Manifest = std::map<std::wstring, File, Insensitive>;

Manifest ReadManifest(const void* bytes, DWORD size) {
    Check(size < 65536);
    std::istringstream lines(std::string(static_cast<const char*>(bytes), size));
    Manifest files;
    std::string line;
    while (std::getline(lines, line)) {
        if (!line.empty() && line.back() == '\r') line.pop_back();
        std::istringstream fields(line);
        File file{};
        std::string name;
        Check(static_cast<bool>(fields >> file.hash >> file.size));
        fields >> std::ws;
        std::getline(fields, name);
        Check(file.hash.size() == 64 && file.hash.find_first_not_of("0123456789abcdef") == std::string::npos);
        Check(file.size < 16 * 1024 * 1024 && !name.empty());
        // Published component filenames are ASCII; the build rejects other names.
        const auto path = SafeRelative(std::wstring(name.begin(), name.end()));
        Check(files.emplace(path.wstring(), file).second);
    }
    Check(files.size() >= 4 && files.size() <= 32 && files.contains(L"CrimsonHue.dll") &&
          files.contains(L"CrimsonHue.runtimeconfig.json") && files.contains(L"CrimsonHue.deps.json"));
    return files;
}

bool Reparse(const fs::path& path) { return (GetFileAttributesW(path.c_str()) & FILE_ATTRIBUTE_REPARSE_POINT) != 0; }
bool Verify(const fs::path& directory, const Manifest& manifest) {
    try {
        if (!fs::is_directory(directory) || Reparse(directory)) return false;
        std::size_t count = 0;
        for (const auto& entry : fs::recursive_directory_iterator(directory)) {
            if (Reparse(entry.path())) return false;
            if (!entry.is_regular_file()) continue;
            const auto name = entry.path().lexically_relative(directory).wstring();
            const auto expected = manifest.find(name);
            if (expected == manifest.end() || entry.file_size() != expected->second.size || FileHash(entry.path()) != expected->second.hash) return false;
            ++count;
        }
        return count == manifest.size();
    } catch (...) { return false; }
}

struct Extraction { fs::path directory; const Manifest* manifest; bool failed = false; };
UINT CALLBACK Extract(void* context, UINT notification, UINT_PTR first, UINT_PTR) {
    auto& extraction = *static_cast<Extraction*>(context);
    try {
        if (notification == SPFILENOTIFY_FILEINCABINET) {
            const auto info = reinterpret_cast<FILE_IN_CABINET_INFO_W*>(first);
            const auto relative = SafeRelative(info->NameInCabinet);
            Check(extraction.manifest->contains(relative.wstring()));
            const auto target = extraction.directory / relative;
            Check(target.wstring().size() < MAX_PATH);
            fs::create_directories(target.parent_path());
            Check(wcscpy_s(info->FullTargetName, target.c_str()) == 0);
            return FILEOP_DOIT;
        }
        if (notification == SPFILENOTIFY_FILEEXTRACTED) {
            const auto paths = reinterpret_cast<FILEPATHS_W*>(first);
            Check(paths->Win32Error == NO_ERROR);
        }
        Check(notification != SPFILENOTIFY_NEEDNEWCABINET);
        return NO_ERROR;
    } catch (...) { extraction.failed = true; return FILEOP_ABORT; }
}

// Every directory move is checked against the selected cache root. Interrupted
// extraction never becomes the canonical cache, and invalid caches are preserved.
void Within(const fs::path& root, const fs::path& target) {
    const auto relative = fs::absolute(target).lexically_normal().lexically_relative(fs::absolute(root).lexically_normal());
    Check(!relative.empty() && !relative.has_root_path());
    for (const auto& part : relative) Check(part != L"..");
}
}

std::filesystem::path ResolvePayload(HMODULE module, const wchar_t* smokePath) {
    const auto [cabinet, cabinetSize] = Resource(module, CabinetResource);
    const auto [manifestBytes, manifestSize] = Resource(module, ManifestResource);
    const auto manifest = ReadManifest(manifestBytes, manifestSize);
    Sha256 sha;
    sha.Add(cabinet, cabinetSize);
    const auto digest = sha.Finish();
    const std::wstring key(digest.begin(), digest.end());
    fs::path root;
    if (smokePath) root = fs::absolute(smokePath).parent_path() / L"asi-runtime-cache";
    else {
        PWSTR local = nullptr;
        Check(SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &local)));
        root = fs::path(local) / L"CrimsonHue" / L"asi-cache";
        CoTaskMemFree(local);
    }
    fs::create_directories(root);
    Check(!Reparse(root));
    const auto cached = root / key;
    if (Verify(cached, manifest)) { cacheState = 2; return cached; }
    const auto suffix = std::to_wstring(GetCurrentProcessId()) + L"-" + std::to_wstring(GetTickCount64());
    const auto stage = root / (key + L"-staging-" + suffix);
    const auto payload = stage / L"payload";
    fs::create_directories(payload);
    const auto cabPath = stage / L"component.cab";
    {
        std::ofstream output(cabPath, std::ios::binary);
        output.write(static_cast<const char*>(cabinet), cabinetSize);
        Check(output.good());
    }
    Extraction extraction{payload, &manifest};
    Check(SetupIterateCabinetW(cabPath.c_str(), 0, Extract, &extraction) && !extraction.failed);
    Check(Verify(payload, manifest));
    std::error_code error;
    const auto existed = fs::exists(cached);
    if (existed) {
        // Another process might have published the same complete cache meanwhile.
        if (Verify(cached, manifest)) { cacheState = 2; return cached; }
        const auto old = root / (key + L"-invalid-" + suffix);
        Within(root, cached); Within(root, old);
        fs::rename(cached, old, error);
        // A cache locked by another process is preserved; use this fresh copy.
        if (error) { cacheState = 3; return payload; }
    }
    Within(root, payload); Within(root, cached);
    fs::rename(payload, cached, error);
    if (error) { Check(Verify(cached, manifest)); cacheState = 2; return cached; }
    // Only our known temporary cabinet and empty stage are removed, never a
    // recursive cache tree or files belonging to a running host.
    fs::remove(cabPath, error);
    fs::remove(stage, error);
    cacheState = existed ? 3 : 1;
    return cached;
}

int PayloadCacheState() { return cacheState.load(); }
