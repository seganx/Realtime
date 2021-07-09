#include "timer.h"
#include "platform.h"
#include <time.h>

SEGAN_LIB_API sx_time sx_time_now()
{
    return time(null);
}

SEGAN_LIB_API sx_time sx_time_diff(const sx_time endtime, const sx_time starttime)
{
    return endtime - starttime;// difftime(endtime, starttime);
}

SEGAN_LIB_API void sx_time_print(char* dest, const uint destsize, const sx_time timeval)
{
    struct tm timeInfo;
    localtime_s(&timeInfo, &timeval);
    strftime(dest, destsize, "%Y-%m-%d %H:%M:%S", &timeInfo);
}
