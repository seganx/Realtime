#include "server.h"
#include "core/memory.h"
#include "core/timer.h"
#include "core/trace.h"
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
    if (token > 0 && validate_player_id_range(id) == false) return null;
    Player* player = &server->lobby.players[id];
    return (player->token == token) ? player : null;
}

Player* lobby_get_player_validate_all(Server* server, const uint token, const short id, const short room, const sbyte index)
{
    if (token > 0 && validate_player_id_range(id) == false) return null;
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
    if (validate_player_id_range(id) == false) return;
    if (server->lobby.players[id].token == 0) return;
    server->lobby.players[id].token = 0;
    server->lobby.count--;
}


/////////////////////////////////////////////////////////////////////////////
//  ROOM
/////////////////////////////////////////////////////////////////////////////
int room_find_empty(Server* server)
{
    for (short r = 0; r < ROOM_COUNT; r++)
    {
        Room* room = &server->rooms[r];
        if (room->count == 0)
            return r;
    }
    return -1;
}

int room_find_free(Server* server)
{
    for (short r = 0; r < ROOM_COUNT; r++)
    {
        Room* room = &server->rooms[r];
        if (room->count < server->config.room_capacity)
            return r;
    }
    return -1;
}

bool room_is_open(const Room* room, const ulong now)
{
    if (room->open_time == 0) return false;  // this means that the room is never openned!
    if (room->open_timeout == 0) return true;
    return sx_time_diff(now, room->open_time) <= room->open_timeout;
}

bool room_is_match(Room* room, int* params)
{
    bool result = true;
    for (byte i = 0; i < ROOM_PARAMS && result; i++)
    {
        int room_param = room->matchmaking[i];
        int param_low = params[i * 2];
        int param_high = params[i * 2 + 1];
        result = result && param_low <= room_param && room_param <= param_high;
    }
    return result;
}

bool room_create(Server* server, Player* player, ulong timeout, byte* properties, sint* matchmaking)
{
    int roomid = room_find_empty(server);
    if (roomid < 0) return false;
    Room* room = &server->rooms[roomid];
    room->open_time = sx_time_now();
    room->open_timeout = timeout;
    sx_mem_copy(room->properties, properties, ROOM_PROP_LEN);
    sx_mem_copy(room->matchmaking, matchmaking, ROOM_PARAMS * sizeof(sint));
    return room_add_player(server, player, roomid);
}

bool room_join(Server* server, Player* player, int* params)
{
    ulong now = sx_time_now();
    for (short roomid = 0; roomid < ROOM_COUNT; roomid++)
    {
        Room* room = &server->rooms[roomid];
        if (room->count < 1 || room->count >= server->config.room_capacity) continue;
        if (room_is_open(room, now) && room_is_match(room, params))
            return room_add_player(server, player, roomid);
    }
    return false;
}

bool room_add_player(Server* server, Player* player, const short roomid)
{
    if (validate_player_index_range(player->index)) return false;
    if (validate_player_room_id_range(player->room)) return false;

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
    player->flag = 0;
}

void room_check_master(Server* server, ulong now, const short roomid)
{
    Room* room = &server->rooms[roomid];
    if (room->count < 1) return;

    sx_trace();

    // find the current master
    Player* current_master = null;
    for (uint p = 0; p < ROOM_CAPACITY; p++)
    {
        Player* player = room->players[p];
        if (player == null || player->token < 1) continue;
        if (sx_flag_has(player->flag, FLAG_MASTER))
        {
            current_master = player;
            break;
        }
    }

    // validate current master
    if (current_master != null && sx_time_diff(now, current_master->active_time) < server->config.player_master_timeout)
        sx_return();

    // validation failed so remove current master
    if (current_master != null)
        sx_flag_rem(current_master->flag, FLAG_MASTER);

    // find new master 
    for (uint p = 0; p < ROOM_CAPACITY; p++)
    {
        Player* player = room->players[p];
        if (player == null || player->token < 1) continue;
        if (sx_time_diff(now, player->active_time) < server->config.player_master_timeout)
        {
            sx_flag_add(player->flag, FLAG_MASTER);
            break;
        }
    }

    sx_return();
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
    sx_print("Player flag[%d] token[%u] time[%llu] device:%.32s", player->flag, player->token, player->active_time, player->device);
}
