#pragma once
#include <windows.h>
#include <filesystem>

// Extract application assemblies only. The installed .NET Desktop Runtime is
// discovered independently and is never part of this embedded payload.
std::filesystem::path ResolvePayload(HMODULE module, const wchar_t* smokePath);
int PayloadCacheState(); // 1 freshly extracted; 2 reused; 3 repaired.
