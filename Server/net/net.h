/********************************************************************
	created:	2012/04/11
	filename: 	Net.h
	Author:		Sajad Beigjani
	eMail:		sajad.b@gmail.com
	Site:		www.SeganX.com
	Desc:		This file contain some basic parts of network system
				these parts are include some functions to initialize &
				finalize				
*********************************************************************/
#pragma once

#include "../core/def.h"


#ifdef __cplusplus
extern "C" {
#endif // __cplusplus

//! initialize network system
SEGAN_LIB_API bool sx_net_initialize( void );

//! finalize network system
SEGAN_LIB_API void sx_net_finalize( void );

//!	additional functions
SEGAN_LIB_API char* sx_net_error_string( const sint code );

#if 0
//! simple function to compute checksum
word sx_net_compute_checksum( const void* buffer, const uint size );

//! return true if the packet is a valid message
bool sx_net_verify_packet( const void* buffer, const uint size );
#endif


#ifdef __cplusplus
}
#endif // __cplusplus


