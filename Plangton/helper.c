#include "server.h"
#include "core/memory.h"
#include "core/timer.h"
#include "core/platform.h"
#include "net/socket.h"

struct Player* player_find(word room, byte id)
{
    struct Server* server = server_get();
    if (room < ROOM_COUNT && id < PLAYER_COUNT)
        return &server->rooms[room].players[id];
}

struct PlayerAddress player_find_address(char* device)
{
    struct PlayerAddress result = { -1 };
    struct Server* server = server_get();

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
                return result;
            }
        }
    }
    return result;
}

struct PlayerAddress player_add(char* device, uint ip, word port)
{
    struct PlayerAddress result = { -1, -1 };
    struct Server* server = server_get();

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
                    room->players[p].active_time = sx_time_now();
                    sx_mem_copy(room->players[p].device, device, DEVICE_LEN);
                    result.room = r;
                    result.index = p;

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

void room_cleanup(struct Room* room, long timeout)
{
    struct Server* server = server_get();
    sx_mutex_lock(server->mutex);

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
    sx_mutex_unlock(server->mutex);
}

uint compute_checksum(byte* buffer, uint len)
{
    uint checksum = 0;
    for (int i = 0; i < len; i++)
        checksum += 64548 + (uint)buffer[i] * 6597;
    return checksum;
}

void server_login(byte* buffer)
{
    struct Login* login = (struct Login*)buffer;
    uint checksum = compute_checksum(buffer, sizeof(struct Login) - sizeof(uint));
    if (login->checksum != checksum) return;

    PlayerAddress address = player_find_address(login->device);
    if (address.room < 0 || address.index < 0)
        address = player_add(login->device, login->ip, login->port);

    struct LoginResponse response = { TYPE_LOGIN };
    response.room = (dword)address.room;
    response.player = (byte)address.index;
    server_send(login->ip, login->port, &response, sizeof(struct LoginResponse));
}

void server_logout(char* buffer)
{
    struct Logout* logout = (struct Logout*)buffer;
    uint checksum = compute_checksum(buffer, sizeof(struct Logout) - sizeof(uint));
    if (logout->checksum != checksum) return;

    struct Player* player = player_find(logout->room, logout->player);
    if (player == null || sx_mem_cmp(player->device, logout->device, DEVICE_LEN) != 0) return;
    struct Server* server = server_get();
    sx_mutex_lock(server->mutex);

    printf("Logout ");
    player_report(player);

    player->ip = 0;
    server->rooms[logout->room].count--;
    sx_mutex_unlock(server->mutex);
}

void server_packet(const char* buffer)
{
    struct Packet* packet = (struct Packet*)buffer;
    uint checksum = compute_checksum(buffer, sizeof(struct Packet) - sizeof(uint));
    if (packet->checksum != checksum) return;

    struct Player* player = player_find(packet->room, packet->player);
    if (player == null) return;
    struct Server* server = server_get();

    sx_mutex_lock(server->mutex);
    player->ip = packet->ip;
    player->port = packet->port;
    player->active_time = sx_time_now();
    sx_mutex_unlock(server->mutex);

    struct Room* room = &server->rooms[packet->room];
    switch (packet->option)
    {
    case 1:
    {
        buffer += sizeof(struct Packet);
        for (uint i = 0; i < PLAYER_COUNT; i++)
        {
            if (room->players[i].ip > 0)
                server_send(room->players[i].ip, room->players[i].port, buffer, packet->datasize);
        }
    }
    break;
    case 2:
    {
        buffer += sizeof(struct Packet);
        for (uint i = 0; i < PLAYER_COUNT; i++)
        {
            if (i != packet->player && room->players[i].ip > 0)
                server_send(room->players[i].ip, room->players[i].port, buffer, packet->datasize);
        }
    }
    break;
    case 3:
    {
        struct Player* other = &room->players[packet->other];
        if (other->ip > 0)
        {
            buffer += sizeof(struct Packet);
            server_send(other->ip, other->port, buffer, packet->datasize);
        }
    }
    break;
    }
}

void server_report(void)
{
    struct Server* server = server_get();

    int c = 0;
    for (uint r = 0; r < ROOM_COUNT; r++)
    {
        if (server->rooms[r].count < 1) continue;
        sx_print("Room[%d] -> %d players", r, server->rooms[r].count);
        c++;
    }
    sx_print("Totla active rooms: %d", c);
}

void room_report(int r)
{
    struct Server* server = server_get();
    struct Room* room = &server->rooms[r];

    sx_print("Room[%d] -> %d players", r, room->count);
    for (uint p = 0; p < PLAYER_COUNT; p++)
    {
        if (room->players[p].ip < 1) continue;
        player_report(&room->players[p]);
    }
}

void player_report(struct Player* player)
{
    byte ip[4] = { 0 };
    sx_mem_copy(ip, &player->ip, 4);
    sx_print("Player[%d.%d.%d.%d:%d] time[%u] device:%s", ip[0], ip[1], ip[2], ip[3], player->port, player->active_time, player->device);
}