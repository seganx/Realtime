#pragma once

#include "core/def.h"

#define TYPE_PING           1
#define TYPE_LOGIN          2
#define TYPE_LOGOUT         3
#define TYPE_ROOMS          4
#define TYPE_JOIN           5
#define TYPE_LEAVE          6
#define TYPE_MESSAGE        20
#define TYPE_ERR_EXPIRED    30
#define TYPE_ERR_IS_FULL    31

#define DEVICE_LEN          32
#define THREAD_COUNTS       32
#define ADDRESS_LEN         32
#define DATA_MAXLEN         230
#define ROOM_COUNT          1024
#define ROOM_PLAYER_COUNT   16
#define LOBBY_PLAYER_COUNT  (ROOM_COUNT * ROOM_PLAYER_COUNT)

typedef struct Player
{
    char    device[DEVICE_LEN];
    byte    from[ADDRESS_LEN];
    uint    token;
    ulong   active_time;
    short   id;
    short   room;
    sbyte   index;
}
Player;

typedef struct Lobby
{
    uint    count;
    Player  players[LOBBY_PLAYER_COUNT];
}
Lobby;

typedef struct Room
{
    sbyte   count;
    sbyte   capacity;
    Player* players[ROOM_PLAYER_COUNT];
}
Room;

typedef struct Config
{
    short   port;
    sbyte   room_capacity;
    uint    player_timeout;
} 
Config;

typedef struct Server
{
    Config  config;
    uint    socket;
    uint    token;
    Lobby   lobby;
    Room    rooms[ROOM_COUNT];

    struct sx_mutex* mutex_token;
    struct sx_mutex* mutex_lobby;
    struct sx_mutex* mutex_room;
}
Server;

#pragma pack(push,1)
typedef struct Ping
{
    byte    type;
    ulong   time;
}
Ping;

typedef struct Login
{
    byte    type;
    char    device[DEVICE_LEN];
    uint    checksum;
}
Login;

typedef struct LoginResponse
{
    byte    type;
    uint    token;
    short   id;
    uint    checksum;
}
LoginResponse;

typedef struct Logout
{
    byte    type;
    uint    token;
    short   id;
    short   room;
    sbyte   index;
    uint    checksum;
}
Logout;

typedef struct Rooms
{
    byte    type;
    uint    token;
    short   id;
    byte    option;
    short   start;
    byte    count;
}
Rooms;

typedef struct RoomsResponse
{
    byte    type;
    byte    count;
    sbyte   players[256];
}
RoomsResponse;

typedef struct Join
{
    byte    type;
    uint    token;
    short   id;
    short   room;
}
Join;

typedef struct JoinResponse
{
    byte    type;
    short   room;
    sbyte   player;
}
JoinResponse;

typedef struct Leave
{
    byte    type;
    uint    token;
    short   id;
    short   room;
    sbyte   index;
}
Leave;

typedef struct LeaveResponse
{
    byte    type;
}
LeaveResponse;

typedef struct Packet
{
    byte    type;
    uint    token;
    short   id;
    short   room;
    sbyte   index;
    byte    option;
    sbyte   other;
    byte    datasize;
}
Packet;

typedef struct ErrorResponse
{
    byte    type;
}
ErrorResponse;

#pragma pack(pop)
