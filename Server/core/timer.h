/********************************************************************
	created:	2017/05/22
	filename: 	Time.h
	Author:		Sajad Beigjani
	eMail:		sajad.b@gmail.com
	Site:		www.SeganX.com
	Desc:		This file contain a simple class and functions to handle times
*********************************************************************/
#ifndef DEFINED_TIME
#define DEFINED_TIME

#include "def.h"

#ifdef __cplusplus
extern "C" {
#endif // __cplusplus

SEGAN_LIB_API ulong sx_time_now();
SEGAN_LIB_API ulong sx_time_diff(const ulong t1, const ulong t2);
SEGAN_LIB_API void sx_time_print(char* dest, const uint destsize, const ulong timeval);


#ifdef __cplusplus
}
#endif // __cplusplus

#endif	//	DEFINED_TIME