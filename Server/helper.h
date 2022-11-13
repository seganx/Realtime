#pragma once

#include "server.h"
#include "core/timer.h"

uint    checksum_compute(const byte* buffer, const uint len);
bool    checksum_is_invalid(const byte* buffer, const uint len, const uint checksum);

bool    validate_player_id_range(const short id);
bool    validate_player_room_id_range(const short roomid);
bool    validate_player_index_range(const sbyte index);

bool    is_player_loggedin(const Player* player);
bool    is_player_not_loggedin(const Player* player);
bool    is_player_joined_room(const Player* player);
bool    is_player_not_joined_room(const Player* player);


Player* lobby_get_player_validate_token(Server* server, const uint token, const short id);
Player* lobby_get_player_validate_all(Server* server, const uint token, const short id, const short room, const sbyte index);
Player* lobby_find_player_by_device(Server* server, const char* device);
Player* lobby_add_player(Server* server, const char* device, const byte* from, const uint token);
void    lobby_remove_player(Server* server, const short id);

bool    room_create(Server* server, Player* player, ulong timeout, byte* properties, sint* matchmaking);
bool    room_join(Server* server, Player* player, int* params);

bool    room_add_player(Server* server, Player* player, const short roomid);
void    room_remove_player(Server* server, Player* player);
void    room_check_master(Server* server, ulong now, const short roomid);
void    room_report(Server* server, int roomid);

void    player_report(Player* player);
