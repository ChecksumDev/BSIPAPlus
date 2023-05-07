#pragma once

#pragma warning( disable : 4028 28251 6001 )

#include <debugapi.h>

HANDLE hHeap;

#define memalloc(size) HeapAlloc(hHeap, HEAP_GENERATE_EXCEPTIONS, size)
#define memcalloc(size) HeapAlloc(hHeap, HEAP_ZERO_MEMORY, size)
#define memfree(mem) HeapFree(hHeap, 0, mem)

#define STR_LEN(str) (sizeof(str) / sizeof(str[0]))

#define DEBUG_BREAK { if (IsDebuggerPresent()) { __debugbreak(); } }

inline void* wmemcpy(wchar_t* dst, const wchar_t* src, size_t n)
{
    wchar_t* d = dst;
    const wchar_t* s = src;
    while (n--)
        *d++ = *s++;
    return dst;
}

inline void* wmemset(wchar_t* dst, wchar_t c, size_t n)
{
    wchar_t* d = dst;
    while (n--)
        *(d++) = c;
    return dst;
}

void* memset(void* dst, char c, int n)
{
    char* d = dst;
    while (n--)
        *d = c;
    return dst;
}

inline void* memcpy(void* dst, const void* src, int n)
{
    char* d = dst;
    const char* s = src;
    while (n--)
        *d++ = *s++;
    return dst;
}

inline size_t wcslen(const wchar_t* str)
{
    size_t result = 0;
    while (*str++)
        result++;
    return result;
}

inline size_t strlen(const char* str)
{
    size_t result = 0;
    while (*str++)
        result++;
    return result;
}
