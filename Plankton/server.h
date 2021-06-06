#pragma once

#include "core/def.h"
#include "core/platform.h"

#define TYPE_PING       1
#define TYPE_LOGIN      2
#define TYPE_LOGOUT     3
#define TYPE_MESSAGE    4
#define TYPE_EXPIRED    5
#define ROOM_COUNT      1024
#define PLAYER_COUNT    16
#define DEVICE_LEN      32
#define THREAD_COUNTS   32
#define ADDRESS_LEN     32
#define DATA_MAXLEN     230

struct Player
{
    byte from[ADDRESS_LEN];
    uint token;
    char device[DEVICE_LEN];
    long active_time;
};

struct Room
{
    uint count;
    uint capacity;
    struct Player players[PLAYER_COUNT];
};

struct Config
{
    uint port;
    uint room_capacity;
    uint player_timeout;
};

struct Server
{
    struct Config config;
    uint socket;
    uint token;
    struct Room rooms[ROOM_COUNT];
    struct sx_mutex* mutex;
};

#pragma pack(push,1)
struct Ping
{
    byte type;
    unsigned long long time;
};

struct PingResponse
{
    byte type;
    unsigned long long time;
};

struct Login
{
    byte type;
    char device[DEVICE_LEN];
    uint checksum;
};

struct LoginResponse
{
    byte type;
    word room;
    byte player;
    uint token;
    uint checksum;
};

struct Logout
{
    byte type;
    word room;
    byte player;
    uint token;
    uint checksum;
};

struct Packet
{
    byte type;
    word room;
    byte player;
    uint token;
    byte other;
    byte option;
    byte datasize;
};
#pragma pack(pop)

typedef struct PlayerAddress
{
    int room;
    int index;
    uint token;
} 
PlayerAddress;

void server_reset(struct Config config);
void server_send(struct sockaddr* address, const void* buffer, const int size);
