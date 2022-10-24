#pragma once

#include "core/def.h"

#define TYPE_PING           1
#define TYPE_LOGIN          10
#define TYPE_LOGOUT         11
#define TYPE_CREATE         20
#define TYPE_JOIN           30
#define TYPE_LEAVE          31
#define TYPE_PACKET_UNRELY  40
#define TYPE_PACKET_RELY    41
#define TYPE_PACKET_RELIED  42

#define FLAG_MASTER         1

#define ERR_INVALID         -1
#define ERR_EXPIRED         -2
#define ERR_IS_FULL         -3
#define ERR_MATCHMAKE       -4

#define DEVICE_LEN          32
#define THREAD_COUNTS       32
#define ADDRESS_LEN         32
#define ROOM_PROP_LEN       32
#define ROOM_COUNT          1024
#define ROOM_CAPACITY       4
#define ROOM_PARAMS         4
#define LOBBY_CAPACITY      (ROOM_COUNT * ROOM_CAPACITY)

#define LOG                 1

typedef struct Player
{
    char    device[DEVICE_LEN];
    byte    from[ADDRESS_LEN];
    uint    token;
    short   id;
    short   room;
    sbyte   index;
    byte    flag;
    ulong   active_time;
}
Player;

typedef struct Lobby
{
    uint    count;
    Player  players[LOBBY_CAPACITY];
}
Lobby;

typedef struct Room
{
    sbyte   count;
    ulong   open_time;
    ulong   open_timeout;
    byte    properties[ROOM_PROP_LEN];
    sint    matchmaking[ROOM_PARAMS];
    Player* players[ROOM_CAPACITY];
}
Room;

typedef struct Config
{
    ushort  port;
    sbyte   room_capacity;
    uint    player_timeout;
    uint    player_master_timeout;
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
    uint    token;
    short   id;
    short   room;
    sbyte   index;
    ulong   time;
}
Ping;

typedef struct PingResponse
{
    byte    type;
    sbyte   error;
    ulong   time;
    ulong   now;
    byte    flag;
}
PingResponse;

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
    sbyte   error;
    uint    token;
    short   id;
    short   room;
    sbyte   index;
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

typedef struct Create
{
    byte    type;
    uint    token;
    short   id;
    ushort  open_timeout;
    byte    properties[ROOM_PROP_LEN];
    sint    matchmaking[ROOM_PARAMS];
}
Create;

typedef struct CreateResponse
{
    byte    type;
    sbyte   error;
    short   room;
    sbyte   index;
    byte    flag;
}
CreateResponse;

typedef struct Join
{
    byte    type;
    uint    token;
    short   id;
    sint    matchmaking[ROOM_PARAMS * 2];
}
Join;

typedef struct JoinResponse
{
    byte    type;
    sbyte   error;
    short   room;
    sbyte   index;
    byte    flag;
    byte    properties[ROOM_PROP_LEN];
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
    sbyte   error;
}
LeaveResponse;

typedef struct PacketUnreliable
{
    byte    type;
    uint    token;
    short   id;
    short   room;
    sbyte   index;
    sbyte   target;
    byte    datasize;
}
PacketUnreliable;

typedef struct PacketReliable
{
    byte    type;
    uint    token;
    short   id;
    short   room;
    sbyte   index;
    sbyte   target;
    byte    ack;
    byte    datasize;
}
PacketReliable;

typedef struct PacketRelied
{
    byte    type;
    uint    token;
    short   id;
    short   room;
    sbyte   index;
    sbyte   target;
    byte    ack;
}
PacketRelied;

typedef struct ErrorResponse
{
    byte    type;
    sbyte   error;
}
ErrorResponse;

#pragma pack(pop)
