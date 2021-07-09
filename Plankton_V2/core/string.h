/********************************************************************
	created:	201705/23
	filename: 	String.h
	Author:		Sajad Beigjani
	eMail:		sajad.b@gmail.com
	Site:		www.SeganX.com
	Desc:		This file contain advanced string functions
*********************************************************************/
#ifndef String_DEFINED
#define String_DEFINED


#include "def.h"


#if	defined(_WIN32)
	#define	PATH_PART	'/'
#elif defined(_MAC)
	//#define	PATH_PART
#else
	//#define	PATH_PART
#endif

#ifdef __cplusplus
extern "C" {
#endif // __cplusplus

SEGAN_LIB_API char sx_str_upper( char c );
SEGAN_LIB_API char sx_str_lower( char c );

SEGAN_LIB_API uint sx_str_len( const char* str );
SEGAN_LIB_API sint sx_str_cmp( const char* str1, const char* str2 );
SEGAN_LIB_API const char* sx_str_str( const char* str, const char* what );

//! copy the src string to the destination string and return number of characters which have copied contain null character
SEGAN_LIB_API sint sx_str_copy( char* dest, const sint dest_size_in_byte, const char* src );

//! return number of items in splittable string
SEGAN_LIB_API sint sx_str_split_count( const char* str, const char* split );

//! split string and copy substring to the destination buffer. return number of characters copied to destination
SEGAN_LIB_API sint sx_str_split( char* dest, const uint destsize, const char* str, const char* split, const uint index );

SEGAN_LIB_API sint sx_str_to_int( const char* str, const sint defaul_val);

SEGAN_LIB_API uint sx_str_to_uint(const char* str, const uint defaul_val);

SEGAN_LIB_API uint64 sx_str_to_uint64( const char* str, const uint64 defaul_val );

SEGAN_LIB_API const char* sx_str_get_filename(const char* filename);


/*! convert one wide char to a UTF-8 character and return the length of the converted UTF-8 character in bytes.*/
SEGAN_LIB_API uint sx_wchar_to_utf8( char* dest, const uint destsize, const short ch );

/*! convert one UTF-8 char to a wide character and return the length of the UTF-8 input character in bytes.*/
SEGAN_LIB_API uint sx_utf8_to_wchar( wchar dest, const uint destwords, const char* src );

/*! convert wide char string to a UTF-8 character string and return the length of the converted UTF-8 characters in bytes.*/
SEGAN_LIB_API uint sx_str_to_utf8( char* dest, const uint destsize, const wchar* src );

/*! convert UTF-8 string to the wide character string and return the size of the converted string in wide chars.*/
SEGAN_LIB_API uint sx_utf8_to_str( wchar* dest, const uint destwords, const char* src );


#ifdef __cplusplus
}
#endif // __cplusplus

#endif	//	String_DEFINED
