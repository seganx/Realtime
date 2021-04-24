#pragma once

#include "core/def.h"
#include "core/platform.h"

#define THREAD_COUNTS   32
#define TYPE_MESSAGE    1
#define TYPE_LOGIN      2
#define TYPE_LOGOUT     3
#define ROOM_COUNT      4096
#define PLAYER_COUNT    32
#define DEVICE_LEN      32


struct Player
{
    uint ip;
    word port;
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
    uint listener_port;
    uint send_port;
    uint room_capacity;
    uint player_timeout;
};

struct Server
{
    struct Config config;
    uint socket_listener;
    uint socket_send;
    struct Room rooms[ROOM_COUNT];
    struct sx_mutex* mutex;
};

#pragma pack(push,1)
struct Login
{
    byte type;
    uint ip;
    word port;
    char device[DEVICE_LEN];
    uint checksum;
};

struct LoginResponse
{
    byte type;
    word room;
    byte player;
};

struct Logout
{
    byte type;
    char device[DEVICE_LEN];
    word room;
    byte player;
    uint checksum;
};

struct Packet
{
    byte type;
    uint ip;
    word port;
    word room;
    byte player;
    byte other;
    byte option;
    byte datasize;
    uint checksum;
};
#pragma pack(pop)

typedef struct PlayerAddress
{
    int room;
    int index;
} 
PlayerAddress;

void server_reset(struct Config config);
struct Server* server_get(void);
void server_cleanup(void);
void server_send(uint ip, word port, void* buffer, uint size);
void server_login(char* buffer);
void server_logout(char* buffer);
void server_packet(char* buffer);
void server_report(void);

struct Player* player_find(word room, byte id);
struct PlayerAddress player_find_address(char* id);
struct PlayerAddress player_add(char* device_id);
void player_report(struct Player* player);

void room_cleanup(struct Room* room, long timeout);
void room_report(int r);
