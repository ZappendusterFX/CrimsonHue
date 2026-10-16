#pragma once
#include <windows.h>
#include <string>
namespace crimson::overlay {
using read_fn = int(__stdcall*)(void*, int);
using send_fn = int(__stdcall*)(const void*, int);
void InitializeDiagnostics() noexcept;
void Diagnostic(const std::string& message) noexcept;
}
