/********************************************************************
    created:	2012/04/11
    filename: 	Socket.h
    Author:		Sajad Beigjani
    eMail:		sajad.b@gmail.com
    Site:		www.SeganX.com
    Desc:		This file contain a basic part of network system

                NOTE: this module implemented to use in single thread.
                using in multi threaded system may cause to unpredicted
                behavior

*********************************************************************/
#pragma once

#include "net.h"


#ifdef __cplusplus
extern "C" {
#endif // __cplusplus

//! open a UPD socket and bind it to the specified port
SEGAN_LIB_API uint sx_socket_open(const word port, const bool bind, const bool broadcast);

//! close opened socket
SEGAN_LIB_API void sx_socket_close(uint socket);

//! send data to the destination address
SEGAN_LIB_API bool sx_socket_send(uint socket, const uint ip, const word port, const void* buffer, const int size);

SEGAN_LIB_API bool sx_socket_send_in(uint socket, struct sockaddr* address, const void* buffer, const int size);

//! pick up data on the port and fill out address of sender
SEGAN_LIB_API sint sx_socket_receive( uint socket, void* buffer, const int size, struct sockaddr* from );

#ifdef __cplusplus
}
#endif // __cplusplus
