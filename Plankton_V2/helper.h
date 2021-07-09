#pragma once

#include "server.h"

uint    checksum_compute(const byte* buffer, const uint len);
bool    checksum_is_invalid(const byte* buffer, const uint len, const uint checksum);

Player* lobby_get_player(Server* server, const short lobby);
Player* lobby_get_player_validate_token(Server* server, const uint token, const short lobby);
Player* lobby_get_player_validate_all(Server* server, const uint token, const short lobby, const short room, const sbyte index);
Player* lobby_find_player_by_device(Server* server, const char* device);
Player* lobby_add_player(Server* server, const char* device, const byte* from, const uint token);
void    lobby_remove_player(Server* server, const short lobby);

void    room_add_player_auto(Server* server, Player* player, const short lobby);
void    room_add_player(Server* server, Player* player, const short lobby, const short room);
void    room_remove_player(Server* server, Player* player, const short lobby);
void    room_report(Server* server, int roomid);

void    player_report(Player* player);
