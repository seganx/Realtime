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

inline bool validate_player_id_range(const short id)
{
    return 0 <= id && id < LOBBY_CAPACITY;
}

inline bool validate_player_room_id_range(const short roomid)
{
    return 0 <= roomid && roomid < ROOM_COUNT;
}

inline bool validate_player_index_range(const sbyte index)
{
    return 0 <= index && index < ROOM_CAPACITY;
}


/////////////////////////////////////////////////////////////////////////////
//  LOBBY 
/////////////////////////////////////////////////////////////////////////////
Player* lobby_get_player_validate_token(Server* server, const uint token, const short id)
{
    if (validate_player_id_range(id) == false) return null;
    Player* player = &server->lobby.players[id];
    return (player->token == token) ? player : null;
}

Player* lobby_get_player_validate_all(Server* server, const uint token, const short id, const short room, const sbyte index)
{
    if (validate_player_id_range(id) == false) return null;
    Player* player = &server->lobby.players[id];
    return (player->token == token && player->room == room && player->index == index) ? player : null;
}

Player* lobby_find_player_by_device(Server* server, const char* device)
{
    for (short i = 0; i < LOBBY_CAPACITY; i++)
    {
        Player* player = &server->lobby.players[i];
        if (player->token > 0 && sx_mem_cmp(player->device, device, DEVICE_LEN) == 0)
            return player;
    }
    return null;
}

Player* lobby_add_player(Server* server, const char* device, const byte* from, const uint token)
{
    for (short i = 0; i < LOBBY_CAPACITY; i++)
    {
        Player* player = &server->lobby.players[i];
        if (player->token > 0) continue;

        sx_mem_copy(player->from, from, ADDRESS_LEN);
        sx_mem_copy(player->device, device, DEVICE_LEN);
        player->token = token;
        player->id = i;
        player->room = -1;
        player->index = -1;
        player->active_time = sx_time_now();

        server->lobby.count++;
        return player;
    }
    return null;
}

void lobby_remove_player(Server* server, const short id)
{
    server->lobby.players[id].token = 0;
    server->lobby.count--;
}


/////////////////////////////////////////////////////////////////////////////
//  ROOM
/////////////////////////////////////////////////////////////////////////////
int room_find_free(Server* server)
{
    for (short r = 0; r < ROOM_COUNT; r++)
    {
        Room* room = &server->rooms[r];
        if (room->count < room->capacity)
            return r;
    }
    return -1;
}

bool room_add_player_auto(Server* server, Player* player)
{
    int roomid = room_find_free(server);
    if (roomid < 0) return false;
    return room_add_player(server, player, roomid);
}

bool room_add_player(Server* server, Player* player, const short roomid)
{
    Room* room = &server->rooms[roomid];

    for (byte i = 0; i < ROOM_CAPACITY; i++)
    {
        if (room->players[i] == null)
        {
            room->count++;
            room->players[i] = player;
            player->room = roomid;
            player->index = i;
            return true;
        }
    }

    return false;
}

void room_remove_player(Server* server, Player* player)
{
    if (validate_player_index_range(player->index) == false) return;
    if (validate_player_room_id_range(player->room) == false) return;

    Room* room = &server->rooms[player->room];
    if (room->players[player->index] == player)
    {
        room->count--;
        room->players[player->index] = null;
    }
    player->room = player->index = -1;
}

void room_report(Server* server, int roomid)
{
    if (roomid < 0 || roomid >= ROOM_COUNT) return;

    Room* room = &server->rooms[roomid];
    sx_print("Room[%d] -> %d players", roomid, room->count);
    for (uint p = 0; p < ROOM_CAPACITY; p++)
    {
        Player* player = room->players[p];
        if (player == null || player->token < 1) continue;
        player_report(player);
    }
}


void player_report(Player* player)
{
    sx_print("Player token[%u] time[%llu] device:%.32s", player->token, player->active_time, player->device);
}
