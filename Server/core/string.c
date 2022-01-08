#include "string.h"

#include <ctype.h>
#include <string.h>

SEGAN_LIB_INLINE char sx_str_upper(char c) { return toupper(c); }
SEGAN_LIB_INLINE char sx_str_lower(char c) { return tolower(c); }

SEGAN_LIB_INLINE uint sx_str_len(const char* str) { return str ? (uint)strlen(str) : 0; }
SEGAN_LIB_INLINE sint sx_str_cmp(const char* str1, const char* str2) { return str1 && str2 ? strcmp(str1, str2) : (str1 ? 1 : (str2 ? -1 : 0)); }
SEGAN_LIB_INLINE const char* sx_str_str(const char* str, const char* what) { return str && what ? strstr(str, what) : null; }
SEGAN_LIB_INLINE sint sx_str_copy(char* dest, const sint dest_size_in_byte, const char* src) { return strcpy_s(dest, dest_size_in_byte, src); }

SEGAN_LIB_INLINE sint sx_str_split_count(const char* str, const char* split)
{
    if (!str || !split) return 0;
    sint res = 1;
    for (const char* f = strstr(str, split); f != null; f = strstr(++f, split))
        res++;
    return res;
}

SEGAN_LIB_INLINE sint sx_str_split(char* dest, const uint destsize, const char* str, const char* split, const uint index)
{
    if (!str || !split) return 0;

    uint splitlen = sx_str_len(split);
    const char *start = str, *end = strstr(str, split);
    for (uint i = 0; i < index && end != null; ++i)
    {
        start = end + splitlen;
        end = strstr(++end, split);
    }

    if (end == null)
        end = str + strlen(str);

    if (start)
    {
        int res = _snprintf_s(dest, destsize, _TRUNCATE, "%.*s", (uint)(end - start), start);
        return res < 0 ? destsize - 1 : res;
    }
    else return 0;
}


SEGAN_LIB_INLINE sint sx_str_to_int(const char* str, const sint defaul_val /*= 0 */)
{
    if (!str) return defaul_val;
    sint res = defaul_val;
    sscanf_s(str, "%d", &res);
    return res;
}

SEGAN_LIB_INLINE uint sx_str_to_uint(const char* str, const uint defaul_val /*= 0 */)
{
    if (!str) return defaul_val;
    uint res = defaul_val;
    sscanf_s(str, "%u", &res);
    return res;
}

SEGAN_LIB_INLINE uint64 sx_str_to_uint64(const char* str, const uint64 defaul_val /*= 0 */)
{
    if (!str) return defaul_val;
    uint64 res = defaul_val;
    sscanf_s(str, "%llu", &res);
    return res;
}

SEGAN_LIB_INLINE const char* sx_str_get_filename(const char* filename)
{
    const char* res = filename;
    for (const char* c = filename; *c != 0; ++c)
        if (*c == '/' || *c == '\\')
            res = c + 1;
    return res;
}


SEGAN_LIB_INLINE uint sx_wchar_to_utf8(char* dest, const uint destsize, const short ch)
{//	code from : http://www.opensource.apple.com/source/OpenLDAP/OpenLDAP-186/OpenLDAP/libraries/libldap/utf-8-conv.c
    uint len = 0;
    if (!dest || !destsize)   /* just determine the required UTF-8 char length. */
    {
        if (ch < 0)         return 0;
        if (ch < 0x80)      return 1;
        if (ch < 0x800)     return 2;
        if (ch < 0x10000)   return 3;
        if (ch < 0x200000)  return 4;
        if (ch < 0x4000000) return 5;
    }
    else if (ch < 0)
    {
        len = 0;
    }
    else if (ch < 0x80)
    {
        if (destsize >= 1)
        {
            dest[len++] = (char)ch;
        }

    }
    else if (ch < 0x800)
    {
        if (destsize >= 2)
        {
            dest[len++] = 0xc0 | (ch >> 6);
            dest[len++] = 0x80 | (ch & 0x3f);
        }

    }
    else if (ch < 0x10000)
    {
        if (destsize >= 3)
        {
            dest[len++] = 0xe0 | (ch >> 12);
            dest[len++] = 0x80 | ((ch >> 6) & 0x3f);
            dest[len++] = 0x80 | (ch & 0x3f);
        }

    }
    else if (ch < 0x200000)
    {
        if (destsize >= 4)
        {
            dest[len++] = 0xf0 | (ch >> 18);
            dest[len++] = 0x80 | ((ch >> 12) & 0x3f);
            dest[len++] = 0x80 | ((ch >> 6) & 0x3f);
            dest[len++] = 0x80 | (ch & 0x3f);
        }

    }
    else if (ch < 0x4000000)
    {
        if (destsize >= 5)
        {
            dest[len++] = 0xf8 | (ch >> 24);
            dest[len++] = 0x80 | ((ch >> 18) & 0x3f);
            dest[len++] = 0x80 | ((ch >> 12) & 0x3f);
            dest[len++] = 0x80 | ((ch >> 6) & 0x3f);
            dest[len++] = 0x80 | (ch & 0x3f);
        }
    }
    else
    {
        if (destsize >= 6)
        {
            dest[len++] = 0xfc | (ch >> 30);
            dest[len++] = 0x80 | ((ch >> 24) & 0x3f);
            dest[len++] = 0x80 | ((ch >> 18) & 0x3f);
            dest[len++] = 0x80 | ((ch >> 12) & 0x3f);
            dest[len++] = 0x80 | ((ch >> 6) & 0x3f);
            dest[len++] = 0x80 | (ch & 0x3f);
        }
    }
    return len;
}

SEGAN_LIB_INLINE uint sx_str_to_utf8(char* dest, const uint destsize, const wchar* src)
{
    int r = 0;
    char tmp[32];
    char* d = dest;
    while (*src)
    {
        r = sx_wchar_to_utf8(tmp, 32, *src++);
        if (r > 0)
        {
            memcpy(d, tmp, r);
            d += r;
        }
        else
        {
            *d++ = (char)*src++;
        }
    }
    *d = 0;
    return (uint)(d - dest);
}


//////////////////////////////////////////////////////////////////////////
//	code from :
//	http://www.opensource.apple.com/source/OpenLDAP/OpenLDAP-186/OpenLDAP/libraries/libldap/utf-8.c

const char ldap_utf8_lentab[] = {
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    0, 0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
    2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
    3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
    4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 0, 0 };

const char ldap_utf8_mintab[] = {
    (char)0x20, (char)0x80, (char)0x80, (char)0x80, (char)0x80, (char)0x80, (char)0x80, (char)0x80,
    (char)0x80, (char)0x80, (char)0x80, (char)0x80, (char)0x80, (char)0x80, (char)0x80, (char)0x80,
    (char)0x30, (char)0x80, (char)0x80, (char)0x80, (char)0x80, (char)0x80, (char)0x80, (char)0x80,
    (char)0x38, (char)0x80, (char)0x80, (char)0x80, (char)0x3c, (char)0x80, (char)0x00, (char)0x00 };

/* LDAP_MAX_UTF8_LEN is 3 or 6 depending on size of wchar_t */
#define LDAP_MAX_UTF8_LEN		 ( sizeof(wchar) * 3/2 )
#define LDAP_UTF8_ISASCII(p)	 ( !(*(const unsigned char *)(p) & 0x80 ) )
#define LDAP_UTF8_CHARLEN(p)	 ( LDAP_UTF8_ISASCII(p) ? 1 : ldap_utf8_lentab[*(const unsigned char *)(p) ^ 0x80] )
#define LDAP_UTF8_CHARLEN2(p, l) ( ( ( l = LDAP_UTF8_CHARLEN( p )) < 3 || ( ldap_utf8_mintab[*(const unsigned char *)(p) & 0x1f] & (p)[1] ) ) ? l : 0 )

SEGAN_LIB_INLINE uint sx_utf8_to_wchar(wchar dest, const uint destwords, const char* src)
{
    if (!src) return 0;

    /* Get UTF-8 sequence length from 1st byte */
    sint utflen = LDAP_UTF8_CHARLEN2(src, utflen);

    if (utflen == 0 || utflen > (int)LDAP_MAX_UTF8_LEN) return 0;

    /* First byte minus length tag */
    unsigned char mask[] = { 0, 0x7f, 0x1f, 0x0f, 0x07, 0x03, 0x01 };
    wchar ch = (wchar)(src[0] & mask[utflen]);

    for (sint i = 1; i < utflen; ++i)
    {
        /* Subsequent bytes must start with 10 */
        if ((src[i] & 0xc0) != 0x80) return 0;

        ch <<= 6;			/* 6 bits of data in each subsequent byte */
        ch |= (wchar)(src[i] & 0x3f);
    }

    dest = ch;
    return utflen;
}

SEGAN_LIB_INLINE uint sx_utf8_to_str(wchar* dest, const uint destwords, const char* src)
{
    /* If input ptr is NULL or empty... */
    if (!src || !*src)
    {
        if (dest) *dest = 0;
        return 0;
    }

    /* Examine next UTF-8 character.  If output buffer is NULL, ignore count */
    uint wclen = 0;
    while (*src && (!dest || wclen < destwords))
    {
        /* Get UTF-8 sequence length from 1st byte */
        sint utflen = LDAP_UTF8_CHARLEN2(src, utflen);

        if (!utflen || utflen >(sint)LDAP_MAX_UTF8_LEN) return 0;

        /* First byte minus length tag */
        unsigned char mask[] = { 0, 0x7f, 0x1f, 0x0f, 0x07, 0x03, 0x01 };
        wchar ch = (wchar)(src[0] & mask[utflen]);

        for (sint i = 1; i < utflen; ++i)
        {
            /* Subsequent bytes must start with 10 */
            if ((src[i] & 0xc0) != 0x80) return 0;

            ch <<= 6;			/* 6 bits of data in each subsequent byte */
            ch |= (wchar)(src[i] & 0x3f);
        }

        if (dest) dest[wclen] = ch;

        src += utflen;		/* Move to next UTF-8 character */
        wclen++;			/* Count number of wide chars stored/required */
    }

    /* Add null terminator if there's room in the buffer. */
    if (dest && wclen < destwords) dest[wclen] = 0;

    return wclen;
}



