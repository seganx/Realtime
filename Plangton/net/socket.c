#if defined(_WIN32)

#include "net.h"
#include "socket.h"
#include "../core/platform.h"
#include "../core/trace.h"
#include "../core/memory.h"
#include <winsock2.h>
#pragma comment( lib, "ws2_32.lib" )

//////////////////////////////////////////////////////////////////////////
//	socket implementation
//////////////////////////////////////////////////////////////////////////

SEGAN_LIB_API uint sx_socket_open( const word port, const bool bindtoport, const bool broadcast )
{
	sx_trace();

	// create socket
	uint result = (uint)socket( AF_INET, SOCK_DGRAM, IPPROTO_UDP );

	if ( result == INVALID_SOCKET )
	{
		sx_print( "Error: Can't initialize socket. error code : %s !" , sx_net_error_string( WSAGetLastError() ) );
		sx_return(0);
	}

	if (broadcast)
	{
		// make it broadcast capable
		int i = 1;
		if ( setsockopt(result, SOL_SOCKET, SO_BROADCAST, (char *)&i, sizeof(i)) == SOCKET_ERROR )
			sx_print( "Error: Unable to make socket broadcast! error code : %s !", sx_net_error_string( WSAGetLastError() ) );
	}

	if (bindtoport)
	{
		// bind to port
		struct sockaddr_in address;
		address.sin_family = AF_INET;
		address.sin_addr.s_addr = INADDR_ANY;
		address.sin_port = htons(port);
		if ( bind( result, (const struct sockaddr*)&address, sizeof(struct sockaddr_in)) == SOCKET_ERROR )
			sx_print( "Error: Unable to bind socket! error code : %s !", sx_net_error_string( WSAGetLastError() ) );
	}

	// make non-blocking socket
	//DWORD nonBlocking = 1;
	//if ( ioctlsocket( result, FIONBIO, &nonBlocking ) == SOCKET_ERROR )
	//{
	//	sx_print( "Error: Unable to make non-blocking socket! error code : %s !", sx_net_error_string( WSAGetLastError() ) );
	//	sx_socket_close( result );
	//	sx_return(0);
	//}

	sx_print( "Info: Socket has been opened on port : %d", port );
	sx_return( result );
}

SEGAN_LIB_API void sx_socket_close( uint socket )
{
	if ( !socket ) return;
	closesocket( socket );
}

SEGAN_LIB_API bool sx_socket_send(uint socket, const uint ip, const word port, const void* buffer, const int size )
{
	sx_assert( socket || buffer || size > 0 );
	sx_trace();

	struct sockaddr_in address = { 0 };
	address.sin_family = AF_INET;
	address.sin_port = htons( port );
	address.sin_addr.s_addr = ip;

	int sentBytes = sendto( socket, (char*)buffer, size, 0, (struct sockaddr*)&address, sizeof(address) );
	sx_return( sentBytes == size );
}

SEGAN_LIB_API sint sx_socket_receive( uint socket, void* buffer, const int size )
{
	sx_assert( socket || buffer || size > 0 );
	sx_trace();

	dword receivedBytes = 0;
	receivedBytes = recvfrom( socket, (char*)buffer, size, 0, NULL, NULL );

	sx_return( receivedBytes <= 0 ? 0 : receivedBytes );
}


#endif
