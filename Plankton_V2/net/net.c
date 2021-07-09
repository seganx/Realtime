#if defined(_WIN32)

#include "net.h"
#include "../core/trace.h"
#include "../core/string.h"
#include "../core/platform.h"

#include <winsock2.h>
#pragma comment( lib, "ws2_32.lib" )


//////////////////////////////////////////////////////////////////////////
//	network functions
//////////////////////////////////////////////////////////////////////////
SEGAN_LIB_API bool sx_net_initialize()
{
	sx_trace();
	bool inited = true;

	//  initialize windows socket
	WSADATA wsaData;
	if( WSAStartup( MAKEWORD( 2, 2 ), &wsaData ) )
	{
		sx_print("Error: Network initialization on Windows failed! error code : %s !", sx_net_error_string(WSAGetLastError()));
		inited = false;
	}

	//  check initialized version
	bool incorrectVersion = LOBYTE(wsaData.wVersion) != 2 || HIBYTE(wsaData.wVersion) != 2;
	if ( inited && incorrectVersion )
	{
		sx_print("Error: Network initialization on Windows failed! Invalid version detected!");
		inited = false;
	}

#if 0
	// Get local host name
	char hostName[128] = {0};
	if( inited && gethostname( hostName, sizeof(hostName) ) )
	{
		sx_print("Error: function ::gethostname() failed with error code : %s !", sx_net_error_string(WSAGetLastError()));
		inited = false;
	}

	// Get local IP address
	struct hostent* pHost = gethostbyname( hostName );
	if( inited && !pHost )
	{
		sx_print("Error: function ::gethostbyname() failed with error code : %s !", sx_net_error_string(WSAGetLastError()));
		inited = false;
	}
#endif

	if ( inited )
	{
#if 0
		//  initialize internal net object
		char name[32] = {0};
		sx_str_copy( name, 32, hostName );

		byte ip_bytes[4] = { 0 };
		ip_bytes[0] = sx_1th_byte_of(pHost->h_addr_list[0]);
		ip_bytes[1] = sx_2th_byte_of(pHost->h_addr_list[0]);
		ip_bytes[2] = sx_3th_byte_of(pHost->h_addr_list[0]);
		ip_bytes[3] = sx_4th_byte_of(pHost->h_addr_list[0]);
		sx_print("Network system initialized successfully on Windows.");
		sx_print("	Name: %s", name);
		sx_print("	IP: %d.%d.%d.%d", ip_bytes[0], ip_bytes[1], ip_bytes[2], ip_bytes[3] );
#endif
	}
	else
	{
		sx_print("The network system is now disabled.");
		WSACleanup();
	}

	sx_return( inited );
}

SEGAN_LIB_API void sx_net_finalize( void )
{
	sx_trace();

	WSACleanup();

	sx_print("Network system Finalized.");

	sx_return();
}


//////////////////////////////////////////////////////////////////////////
//	additional functions
//////////////////////////////////////////////////////////////////////////
#if 0
SEGAN_LIB_INLINE word sx_net_compute_checksum(const void* buffer, const uint size)
{
	const byte* buf = (const byte*)buffer;
	word sum1 = 0;
	word sum2 = 0;
	for ( uint index = 0; index < size; ++index )
	{
	   sum1 = (sum1 + buf[index]) % 255;
	   sum2 = (sum2 + sum1) % 255;
	}
	return (sum2 << 8) | sum1;
}

bool sx_net_verify_packet(const void* buffer, const uint size)
{
	//	validate message size
	if ( sx_between_i( size, sizeof(NetHeader), SX_NET_BUFF_SIZE ) == false )
		return false;

	NetHeader* ch = (NetHeader*)buffer;

	//	validate net id
	if ( ch->netId != SX_NET_ID )
		return false;

	//	validate data checksum
	if ( size > sizeof(NetHeader) )
	{
		const byte* buf = (const byte*)buffer + sizeof(NetHeader);
		if ( ch->checksum != sx_net_compute_checksum( buf, size - sizeof(NetHeader)) )
			return false;
	}
	
	// now we can suppose that the package is valid
	return true;
}
#endif

SEGAN_LIB_API char* sx_net_error_string(const sint code)
{
	switch (code) {
		case WSAEINTR:				return "WSAEINTR";
		case WSAEBADF:				return "WSAEBADF";
		case WSAEACCES: 			return "WSAEACCES";
		case WSAEDISCON: 			return "WSAEDISCON";
		case WSAEFAULT: 			return "WSAEFAULT";
		case WSAEINVAL: 			return "WSAEINVAL";
		case WSAEMFILE: 			return "WSAEMFILE";
		case WSAEWOULDBLOCK: 		return "WSAEWOULDBLOCK";
		case WSAEINPROGRESS: 		return "WSAEINPROGRESS";
		case WSAEALREADY: 			return "WSAEALREADY";
		case WSAENOTSOCK: 			return "WSAENOTSOCK";
		case WSAEDESTADDRREQ: 		return "WSAEDESTADDRREQ";
		case WSAEMSGSIZE: 			return "WSAEMSGSIZE";
		case WSAEPROTOTYPE: 		return "WSAEPROTOTYPE";
		case WSAENOPROTOOPT: 		return "WSAENOPROTOOPT";
		case WSAEPROTONOSUPPORT: 	return "WSAEPROTONOSUPPORT";
		case WSAESOCKTNOSUPPORT: 	return "WSAESOCKTNOSUPPORT";
		case WSAEOPNOTSUPP: 		return "WSAEOPNOTSUPP";
		case WSAEPFNOSUPPORT: 		return "WSAEPFNOSUPPORT";
		case WSAEAFNOSUPPORT: 		return "WSAEAFNOSUPPORT";
		case WSAEADDRINUSE: 		return "WSAEADDRINUSE";
		case WSAEADDRNOTAVAIL: 		return "WSAEADDRNOTAVAIL";
		case WSAENETDOWN: 			return "WSAENETDOWN";
		case WSAENETUNREACH: 		return "WSAENETUNREACH";
		case WSAENETRESET: 			return "WSAENETRESET";
		case WSAECONNABORTED:		return "WSWSAECONNABORTEDAEINTR";
		case WSAECONNRESET: 		return "WSAECONNRESET";
		case WSAENOBUFS: 			return "WSAENOBUFS";
		case WSAEISCONN: 			return "WSAEISCONN";
		case WSAENOTCONN: 			return "WSAENOTCONN";
		case WSAESHUTDOWN: 			return "WSAESHUTDOWN";
		case WSAETOOMANYREFS: 		return "WSAETOOMANYREFS";
		case WSAETIMEDOUT: 			return "WSAETIMEDOUT";
		case WSAECONNREFUSED: 		return "WSAECONNREFUSED";
		case WSAELOOP: 				return "WSAELOOP";
		case WSAENAMETOOLONG: 		return "WSAENAMETOOLONG";
		case WSAEHOSTDOWN: 			return "WSAEHOSTDOWN";
		case WSASYSNOTREADY: 		return "WSASYSNOTREADY";
		case WSAVERNOTSUPPORTED: 	return "WSAVERNOTSUPPORTED";
		case WSANOTINITIALISED: 	return "WSANOTINITIALISED";
		case WSAHOST_NOT_FOUND: 	return "WSAHOST_NOT_FOUND";
		case WSATRY_AGAIN: 			return "WSATRY_AGAIN";
		case WSANO_RECOVERY: 		return "WSANO_RECOVERY";
		case WSANO_DATA: 			return "WSANO_DATA";
		default: 					return "UNKNOWN";
	}
}

#endif
