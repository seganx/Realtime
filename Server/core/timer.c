#include "timer.h"
#include "platform.h"
#include <time.h>
#include <sys/timeb.h>

SEGAN_LIB_API ulong sx_time_now()
{
#if defined(_WIN32) || defined(_WIN64)
    struct _timeb timebuffer;
    _ftime64_s(&timebuffer);
    return (ulong)(((timebuffer.time * 1000) + timebuffer.millitm));
#else
    struct timeb timebuffer;
    ftime(&timebuffer);
    return (ulong)(((timebuffer.time * 1000) + timebuffer.millitm));
#endif
}

SEGAN_LIB_API ulong sx_time_diff(const ulong endtime, const ulong starttime)
{
    return endtime - starttime;// difftime(endtime, starttime);
}

SEGAN_LIB_API void sx_time_print(char* dest, const uint destsize, const ulong timeval)
{
    struct tm timeInfo;
    ulong t = timeval / 1000;
    localtime_s(&timeInfo, &t);
    strftime(dest, destsize, "%Y-%m-%d %H:%M:%S", &timeInfo);
}
