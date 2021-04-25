#include "server.h"
#include "core/memory.h"
#include "core/timer.h"
#include "core/platform.h"
#include "net/socket.h"


uint compute_checksum(const byte* buffer, const uint len)
{
    uint checksum = 0;
    for (uint i = 0; i < len; i++)
        checksum += 64548 + (uint)buffer[i] * 6597;
    return checksum;
}

void player_report(struct Player* player)
{
    byte ip[4] = { 0 };
    sx_mem_copy(ip, &player->ip, 4);
    sx_print("Player[%d.%d.%d.%d:%u] token[%u] time[%u] device:%.32s", ip[0], ip[1], ip[2], ip[3], player->port, player->token, player->active_time, player->device);
}

struct Player* player_find(struct Server* server, word room, byte id)
{
    if (room < ROOM_COUNT && id < PLAYER_COUNT)
        return &server->rooms[room].players[id];
    return null;
}

struct PlayerAddress player_find_address(struct Server* server, char* device)
{
    struct PlayerAddress result = { -1 };
    for (uint r = 0; r < ROOM_COUNT; r++)
    {
        if (server->rooms[r].count < 1) continue;

        for (uint p = 0; p < PLAYER_COUNT; p++)
        {
            struct Player* player = &server->rooms[r].players[p];
            if (player->ip > 0 && sx_mem_cmp(player->device, device, DEVICE_LEN) == 0)
            {
                result.room = r;
                result.index = p;
                result.token = player->token;
                return result;
            }
        }
    }
    return result;
}

struct PlayerAddress player_add(struct Server* server, char* device, uint ip, word port, uint token)
{
    struct PlayerAddress result = { -1, -1 };

    for (uint r = 0; r < ROOM_COUNT; r++)
    {
        struct Room* room = &server->rooms[r];
        if (room->count < room->capacity)
        {
            for (uint p = 0; p < PLAYER_COUNT; p++)
            {
                sx_mutex_lock(server->mutex);
                if (room->players[p].ip == 0)
                {
                    room->count++;
                    room->players[p].ip = ip;
                    room->players[p].port = port;
                    room->players[p].token = token;
                    room->players[p].active_time = sx_time_now();
                    sx_mem_copy(room->players[p].device, device, DEVICE_LEN);
                    result.room = r;
                    result.index = p;
                    result.token = token;

                    printf("Added ");
                    player_report(&room->players[p]);

                    sx_mutex_unlock(server->mutex);
                    return result;
                }
                sx_mutex_unlock(server->mutex);
            }
        }
    }
    return result;
}


void room_cleanup(struct sx_mutex* mutex, struct Room* room, long timeout)
{
    sx_mutex_lock(mutex);
    sx_time now = sx_time_now();
    for (uint i = 0; i < PLAYER_COUNT; i++)
    {
        struct Player* player = &room->players[i];
        if (player->ip > 0 && sx_time_diff(now, player->active_time) > timeout)
        {
            printf("Cleaned ");
            player_report(player);

            player->ip = 0;
            room->count--;
        }
    }
    sx_mutex_unlock(mutex);
}

void room_report(struct Server* server, int r)
{
    if (r >= ROOM_COUNT) return;

    struct Room* room = &server->rooms[r];
    sx_print("Room[%d] -> %d players", r, room->count);
    for (uint p = 0; p < PLAYER_COUNT; p++)
    {
        if (room->players[p].ip < 1) continue;
        player_report(&room->players[p]);
    }
}
