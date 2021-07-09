// server.cpp : Defines the entry point for the application.
//

#include "server.h"
#include "helper.h"
#include "net/net.h"
#include "net/socket.h"
#include "core/string.h"
#include "core/memory.h"
#include "core/trace.h"
#include "core/timer.h"
#include "core/platform.h"

Server server = { 0 };

uint server_get_token()
{
    sx_mutex_lock(server.mutex_token);
    server.token++;
    while (server.token == 0) server.token++;
    sx_mutex_unlock(server.mutex_token);
    return server.token;
}

void server_init()
{
    sx_trace();

    sx_mem_set(&server, 0, sizeof(struct Server));

    server.token = 654987;
    for (size_t r = 0; r < ROOM_COUNT; r++)
    {
        server.rooms[r].count = 0;
        for (size_t p = 0; p < ROOM_PLAYER_COUNT; p++)
            server.rooms[r].players[p] = -1;
    }

    server.mutex_token = sx_mutex_create();
    server.mutex_lobby = sx_mutex_create();
    server.mutex_room = sx_mutex_create();

    sx_return();
}

void server_shutdown(void)
{
    sx_trace();
    sx_socket_close(server.socket);
    sx_mutex_destroy(server.mutex_token);
    sx_mutex_destroy(server.mutex_lobby);
    sx_mutex_destroy(server.mutex_room);
    sx_return();
}

void server_reset(Config config)
{
    sx_trace();

    server.config = config;

    if (server.socket > 0) sx_socket_close(server.socket);
    server.socket = sx_socket_open(config.port, true, false);

    for (size_t r = 0; r < ROOM_COUNT; r++)
        server.rooms[r].capacity = config.room_capacity;

    sx_return();
}


void server_cleanup(void)
{
    sx_trace();

    sx_time now = sx_time_now();
    sx_mutex_lock(server.mutex_lobby);

    for (short i = 0; i < LOBBY_PLAYER_COUNT; i++)
    {
        Player* player = lobby_get_player(&server, i);
        if (player->token < 1) continue;
        if (sx_time_diff(now, player->active_time) > server.config.player_timeout)
        {
            room_remove_player(&server, player, i);
            lobby_remove_player(&server, i);
        }
    }

    sx_mutex_unlock(server.mutex_lobby);
    sx_return();
}

void server_send(const byte* address, const void* buffer, const int size)
{
    sx_socket_send_in(server.socket, (const struct sockaddr*)address, buffer, size);
}

void server_ping(byte* buffer, const byte* from)
{
    server_send(from, buffer, sizeof(Ping));
}

void server_send_expired(const byte* from)
{
    ExpiredResponse response = { TYPE_EXPIRED };
    server_send(from, &response, sizeof(ExpiredResponse));
}

void server_process_login(byte* buffer, const byte* from)
{
    Login* login = (Login*)buffer;
    if (checksum_is_invalid(buffer, sizeof(Login) - sizeof(uint), login->checksum)) return;

    Player* player = lobby_find_player_by_device(&server, login->device);
    if (player == null)
        player = lobby_add_player(&server, login->device, from, server_get_token());
    if (player == null) return;

    LoginResponse response = { TYPE_LOGIN };
    response.token = player->token;
    response.lobby = player->lobby;
    response.checksum = checksum_compute((const byte*)&response, sizeof(LoginResponse) - sizeof(uint));
    server_send(from, &response, sizeof(LoginResponse));
}

void server_process_logout(byte* buffer)
{
    Logout* logout = (Logout*)buffer;
    if (checksum_is_invalid(buffer, sizeof(Logout) - sizeof(uint), logout->checksum)) return;

    Player* player = lobby_get_player_validate_all(&server, logout->token, logout->lobby, logout->room, logout->index);
    if (player == null) return;

    room_remove_player(&server, player, logout->lobby);
    lobby_remove_player(&server, logout->lobby);
}

void server_process_rooms(byte* buffer, const byte* from)
{
    Rooms* rooms = (Rooms*)buffer;
    if (lobby_get_player_validate_token(&server, rooms->token, rooms->lobby) == null)
    {
        server_send_expired(from);
        return;
    }
    if (rooms->start < 0 || rooms->start >= ROOM_COUNT) return;

    RoomsResponse response = { TYPE_ROOMS, 0 };
    for (int i = rooms->start; i < ROOM_COUNT && response.count < rooms->count; i++)
    {
        Room* room = &server.rooms[i];
        if (rooms->option == 1 && room->count >= room->capacity) continue;
        response.players[response.count++] = room->count;
    }
    server_send(from, &response, sizeof(RoomsResponse));
}

void server_process_join(byte* buffer, const byte* from)
{
    Join* join = (Join*)buffer;

    Player* player = lobby_get_player_validate_token(&server, join->token, join->lobby);
    if (player == null)
    {
        server_send_expired(from);
        return;
    }

    if (player->room < 0)
    {
        if (join->room == -1)
            room_add_player_auto(&server, player, join->lobby);
        else if (0 <= join->room && join->room < ROOM_COUNT)
            room_add_player(&server, player, join->lobby, join->room);
        else return;
    }

    JoinResponse response = { TYPE_JOIN, player->room, player->index };
    server_send(from, &response, sizeof(JoinResponse));
}

void server_process_leave(byte* buffer, const byte* from)
{
    Leave* leave = (Leave*)buffer;

    Player* player = lobby_get_player_validate_all(&server, leave->token, leave->lobby, leave->room, leave->index);
    
    if (player != null)
        room_remove_player(&server, player, leave->lobby);

    LeaveResponse response = { TYPE_LEAVE };
    server_send(from, &response, sizeof(LeaveResponse));
}


void server_process_packet(byte* buffer, const byte* from)
{
    Packet packet;
    sx_mem_copy(&packet, buffer, sizeof(struct Packet));

    Player* player = lobby_get_player_validate_all(&server, packet.token, packet.lobby, packet.room, packet.index);
    if (player == null)
    {
        server_send_expired(from);
        return;
    }

    sx_mutex_lock(server.mutex_lobby);
    sx_mem_copy(player->from, from, ADDRESS_LEN);
    player->active_time = sx_time_now();
    sx_mutex_unlock(server.mutex_lobby);

    struct Room* room = &server.rooms[packet.room];
    switch (packet.option)
    {
    case 1:
    {
        buffer += sizeof(Packet) - 2;
        buffer[0] = TYPE_MESSAGE;
        buffer[1] = packet.index;
        for (uint i = 0; i < ROOM_PLAYER_COUNT; i++)
        {
            Player* other = lobby_get_player(&server, room->players[i]);
            if (other->token > 0)
                server_send(other->from, buffer, packet.datasize + 2);
        }
    }
    break;
    case 2:
    {
        buffer += sizeof(struct Packet) - 2;
        buffer[0] = TYPE_MESSAGE;
        buffer[1] = packet.index;
        for (uint i = 0; i < ROOM_PLAYER_COUNT; i++)
        {
            if (i == packet.index) continue;
            Player* other = lobby_get_player(&server, room->players[i]);
            if (other->token > 0)
                server_send(other->from, buffer, packet.datasize + 2);
        }
    }
    break;
    case 3:
        if (0 <= packet.other && packet.other < ROOM_PLAYER_COUNT)
        {
            Player* other = lobby_get_player(&server, room->players[packet.other]);
            if (other->token > 0)
            {
                buffer += sizeof(struct Packet) - 2;
                buffer[0] = TYPE_MESSAGE;
                buffer[1] = packet.index;
                server_send(other->from, buffer, packet.datasize + 2);
            }
        }
        break;
    }
}

void server_report(void)
{
    int total_rooms = 0, total_players = 0;
    for (uint r = 0; r < ROOM_COUNT; r++)
    {
        if (server.rooms[r].count < 1) continue;
        sx_print("Room[%d] -> %d players", r, server.rooms[r].count);
        total_rooms++;
        total_players += server.rooms[r].count;
    }
    sx_print("Total active rooms: %d\nTotal active players: %d", total_rooms, total_players);
}


//////////////////////////////////////////////////////////////////////////////////
// MAIN
//////////////////////////////////////////////////////////////////////////////////
void thread_ticker(void* param)
{
    sx_trace_attach(64, "trace_ticker.txt");
    sx_trace();

    while (true)
    {
        server_cleanup();
        sx_sleep(1000);
    }

    sx_trace_detach();
}

void thread_listener(void* param)
{
    sx_trace_attach(64, "trace_worker.txt");
    sx_trace();

    while (true)
    {
        byte from[ADDRESS_LEN] = { 0 };
        byte buffer[256] = { 0 };
        sx_socket_receive(server.socket, buffer, 255, (struct sockaddr*)from);

        switch (buffer[0])
        {
        case TYPE_PING: server_ping(buffer, from); break;
        case TYPE_MESSAGE: server_process_packet(buffer, from); break;
        case TYPE_LOGIN: server_process_login(buffer, from); break;
        case TYPE_LOGOUT: server_process_logout(buffer); break;
        case TYPE_ROOMS: server_process_rooms(buffer, from); break;
        case TYPE_JOIN: server_process_join(buffer, from); break;
        case TYPE_LEAVE: server_process_leave(buffer, from); break;
        }
    }

    sx_trace_detach();
}

int main()
{
    sx_trace_attach(64, "trance.txt");
    sx_trace();
    sx_net_initialize();

    // initialize server with default config
    server_init();
    {
        Config config = { 0 };
        config.port = 36000;
        config.room_capacity = 16;
        config.player_timeout = 300;
        server_reset(config);

        char t[64] = { 0 };
        sx_time_print(t, 64, sx_time_now());
        sx_print("Server started on %s", t);
    }

    struct sx_thread* threads[THREAD_COUNTS] = { null };
    threads[0] = sx_thread_create(1, thread_ticker, null);
    for (size_t i = 1; i < THREAD_COUNTS; i++)
        threads[i] = sx_thread_create(i + 1, thread_listener, null);

    char cmd[128] = { 0 };
    while (sx_str_cmp(cmd, "exit\n") != 0)
    {
        sx_mem_set(cmd, 0, 128);
        fgets(cmd, 127, stdin);

        char cmd1[32] = { 0 };
        char cmd2[32] = { 0 };
        int value = 0;
        sscanf_s(cmd, "%s %s %d", cmd1, 32, cmd2, 32, &value);

        if (sx_str_cmp(cmd1, "report") == 0)
        {
            if (sx_str_cmp(cmd2, "server") == 0)
                server_report();
            else if (sx_str_cmp(cmd2, "room") == 0)
                room_report(&server, value);
        }

        sx_sleep(1);
    }

    for (size_t i = 0; i < THREAD_COUNTS; i++)
        sx_thread_destroy(threads[i]);

    server_shutdown();

    sx_trace_detach();
    return 0;
}
