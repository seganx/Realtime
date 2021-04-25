#pragma once

#include "server.h"

uint compute_checksum(const byte* buffer, const uint len);

void player_report(struct Player* player);
struct Player* player_find(struct Server* server, word room, byte id);
struct PlayerAddress player_find_address(struct Server* server, char* id);
struct PlayerAddress player_add(struct Server* server, char* device, uint ip, word port, uint token);

void room_cleanup(struct sx_mutex* mutex, struct Room* room, long timeout);
void room_report(struct Server* server, int r);
