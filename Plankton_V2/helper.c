#include "server.h"
#include "core/memory.h"
#include "core/timer.h"
#include "core/platform.h"
#include "net/socket.h"
#include "helper.h"


uint checksum_compute(const byte* buffer, const uint len)
{
    uint checksum = 0;
    for (uint i = 0; i < len; i++)
        checksum += 64548 + (uint)buffer[i] * 6597;
    return checksum;
}

bool checksum_is_invalid(const byte* buffer, const uint len, const uint checksum)
{
    return checksum != checksum_compute(buffer, len);
}



/////////////////////////////////////////////////////////////////////////////
//  LOBBY 
/////////////////////////////////////////////////////////////////////////////
Player* lobby_get_player(Server* server, const short lobby)
{
    if (0 <= lobby && lobby < LOBBY_PLAYER_COUNT)
        return &server->lobby.players[lobby];
    return null;
}

Player* lobby_get_player_validate_token(Server* server, const uint token, const short lobby)
{
    Player* player = lobby_get_player(server, lobby);
    return (player != null && player->token == token) ? player : null;
}

Player* lobby_get_player_validate_all(Server* server, const uint token, const short lobby, const short room, const sbyte index)
{
    Player* player = lobby_get_player(server, lobby);
    return (player != null && player->token == token && player->room == room && player->index == index) ? player : null;
}

Player* lobby_find_player_by_device(Server* server, const char* device)
{
    Player* result = null;
    sx_mutex_lock(server->mutex_lobby);

    for (short i = 0; i < LOBBY_PLAYER_COUNT && result == null; i++)
    {
        Player* player = &server->lobby.players[i];
        if (player->token > 0 && sx_mem_cmp(player->device, device, DEVICE_LEN) == 0)
        {
            result = player;
            break;
        }
    }

    sx_mutex_unlock(server->mutex_lobby);
    return result;
}

Player* lobby_add_player(Server* server, const char* device, const byte* from, const uint token)
{
    Player* result = null;
    sx_mutex_lock(server->mutex_lobby);

    for (short i = 0; i < LOBBY_PLAYER_COUNT; i++)
    {
        Player* player = &server->lobby.players[i];
        if (player->token > 0) continue;

        sx_mem_copy(player->from, from, ADDRESS_LEN);
        sx_mem_copy(player->device, device, DEVICE_LEN);
        player->token = token;
        player->lobby = i;
        player->room = -1;
        player->index = -1;
        player->active_time = sx_time_now();

        result = player;
        server->lobby.count++;
        break;
    }

    sx_mutex_unlock(server->mutex_lobby);
    return result;
}

void lobby_remove_player(Server* server, const short lobby)
{
    sx_mutex_lock(server->mutex_lobby);
    server->lobby.players[lobby].token = 0;
    server->lobby.count--;
    sx_mutex_unlock(server->mutex_lobby);
}


/////////////////////////////////////////////////////////////////////////////
//  ROOM
/////////////////////////////////////////////////////////////////////////////
void room_add_player_auto(Server* server, Player* player, const short lobby)
{
    sx_mutex_lock(server->mutex_room);

    for (short r = 0; r < ROOM_COUNT && player->room == -1; r++)
    {
        Room* room = &server->rooms[r];
        if (room->count >= room->capacity) continue;

        for (byte i = 0; i < ROOM_PLAYER_COUNT && player->index == -1; i++)
        {
            if (room->players[i] >= 0) continue;
            room->count++;
            room->players[i] = lobby;
            player->room = r;
            player->index = i;
            break;
        }
    }

    sx_mutex_unlock(server->mutex_room);
}

void room_add_player(Server* server, Player* player, const short lobby, const short roomid)
{
    sx_mutex_lock(server->mutex_room);

    Room* room = &server->rooms[roomid];
    if (room->count < room->capacity)
    {
        for (byte i = 0; i < ROOM_PLAYER_COUNT && player->index == -1; i++)
        {
            if (room->players[i] >= 0) continue;
            room->count++;
            room->players[i] = lobby;
            player->room = roomid;
            player->index = i;
            break;
        }
    }

    sx_mutex_unlock(server->mutex_room);
}

void room_remove_player(Server* server, Player* player, const short lobby)
{
    if (player->room < 0 || player->index < 0) return;

    sx_mutex_lock(server->mutex_room);

    if (player->room < ROOM_COUNT && player->index < ROOM_PLAYER_COUNT && server->rooms[player->room].players[player->index] == lobby)
    {
        server->rooms[player->room].players[player->index] = -1;
        server->rooms[player->room].count--;
    }
    player->room = player->index = -1;

    sx_mutex_unlock(server->mutex_room);
}

void room_report(Server* server, int roomid)
{
    if (roomid < 0 || roomid >= ROOM_COUNT) return;

    Room* room = &server->rooms[roomid];
    sx_print("Room[%d] -> %d players", roomid, room->count);
    for (uint p = 0; p < ROOM_PLAYER_COUNT; p++)
    {
        Player* player = lobby_get_player(server, room->players[p]);
        if (player == null || player->token < 1) continue;
        player_report(player);
    }
}


void player_report(Player* player)
{
    sx_print("Player token[%u] time[%llu] device:%.32s", player->token, player->active_time, player->device);
}
