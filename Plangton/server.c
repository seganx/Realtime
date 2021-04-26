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

struct Server server = { 0 };

uint server_get_token()
{
    sx_mutex_lock(server.mutex);
    server.token++;
    if (server.token == 0)
        server.token++;
    sx_mutex_unlock(server.mutex);
    return server.token;
}

void server_reset(struct Config config)
{
    sx_trace();

    server.config = config;

    if (server.socket > 0) sx_socket_close(server.socket);
    server.socket = sx_socket_open(config.port, true, false);

    for (size_t i = 0; i < ROOM_COUNT; i++)
        server.rooms[i].capacity = config.room_capacity;

    if (server.mutex == null)
        server.mutex = sx_mutex_create();

    sx_return();
}

void server_cleanup(void)
{
    sx_trace();
    for (uint i = 0; i < ROOM_COUNT; i++)
        if (server.rooms[i].count > 0)
            room_cleanup(server.mutex, &server.rooms[i], server.config.player_timeout);
    sx_return();
}

void server_shutdown(void)
{
    if (server.socket > 0) sx_socket_close(server.socket);
    if (server.mutex != null) sx_mutex_destroy(server.mutex);
}

void server_send(struct sockaddr* address, const void* buffer, const int size)
{
    sx_socket_send_in(server.socket, address, buffer, size);
}

void server_ping(byte* buffer, struct sockaddr* from)
{
    struct Ping* ping = (struct Ping*)buffer;
    struct PingResponse response = { TYPE_PING };
    response.time = ping->time;
    server_send(from, &response, sizeof(struct PingResponse));
}

void server_login(byte* buffer, struct sockaddr* from)
{
    struct Login* login = (struct Login*)buffer;
    uint checksum = compute_checksum(buffer, sizeof(struct Login) - sizeof(uint));
    if (login->checksum != checksum) return;

    PlayerAddress address = player_find_address(&server, login->device);
    if (address.room < 0 || address.index < 0)
        address = player_add(&server, login->device, from, server_get_token());

    struct LoginResponse response = { TYPE_LOGIN };
    response.room = (dword)address.room;
    response.player = (byte)address.index;
    response.token = address.token;
    response.checksum = compute_checksum(&response, sizeof(struct LoginResponse) - sizeof(uint));
    server_send(from, &response, sizeof(struct LoginResponse));
}

void server_logout(byte* buffer)
{
    struct Logout* logout = (struct Logout*)buffer;
    uint checksum = compute_checksum(buffer, sizeof(struct Logout) - sizeof(uint));
    if (logout->checksum != checksum) return;

    struct Player* player = player_find(&server, logout->room, logout->player);
    if (player == null || player->token != logout->token) return;

    sx_mutex_lock(server.mutex);
    {
        printf("Logout ");
        player_report(player);
        player->token = 0;
        server.rooms[logout->room].count--;
    }
    sx_mutex_unlock(server.mutex);
}

void server_packet(byte* buffer, struct sockaddr* from)
{
    struct Packet packet;
    sx_mem_copy(&packet, buffer, sizeof(struct Packet));

    struct Player* player = player_find(&server, packet.room, packet.player);
    if (player == null) return;
    if (player->token != packet.token)
    {
        buffer[0] = TYPE_EXPIRED;
        server_send(from, buffer, 1);
        return;
    }

    sx_mutex_lock(server.mutex);
    sx_mem_copy(player->from, from, ADDRESS_LEN);
    player->active_time = sx_time_now();
    sx_mutex_unlock(server.mutex);

    struct Room* room = &server.rooms[packet.room];
    switch (packet.option)
    {
    case 1:
    {
        buffer += sizeof(struct Packet) - 2;
        buffer[0] = TYPE_MESSAGE;
        buffer[1] = packet.player;
        for (uint i = 0; i < PLAYER_COUNT; i++)
        {
            if (room->players[i].token > 0)
                server_send(room->players[i].from, buffer, packet.datasize + 2);
        }
    }
    break;
    case 2:
    {
        buffer += sizeof(struct Packet) - 2;
        buffer[0] = TYPE_MESSAGE;
        buffer[1] = packet.player;
        for (uint i = 0; i < PLAYER_COUNT; i++)
        {
            if (i != packet.player && room->players[i].token > 0)
                server_send(room->players[i].from, buffer, packet.datasize + 2);
        }
    }
    break;
    case 3:
        if (packet.other < PLAYER_COUNT)
        {
            struct Player* other = &room->players[packet.other];
            if (other->token > 0)
            {
                buffer += sizeof(struct Packet) - 2;
                buffer[0] = TYPE_MESSAGE;
                buffer[1] = packet.player;
                server_send(other->from, buffer, packet.datasize + 2);
            }
        }
        break;
    }
}

void server_report(void)
{
    int c = 0;
    for (uint r = 0; r < ROOM_COUNT; r++)
    {
        if (server.rooms[r].count < 1) continue;
        sx_print("Room[%d] -> %d players", r, server.rooms[r].count);
        c++;
    }
    sx_print("Total active rooms: %d", c);
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
        sx_socket_receive(server.socket, buffer, 254, from);

        switch (buffer[0])
        {
        case TYPE_PING: server_ping(buffer, from); break;
        case TYPE_MESSAGE: server_packet(buffer, from); break;
        case TYPE_LOGIN: server_login(buffer, from); break;
        case TYPE_LOGOUT: server_logout(buffer); break;
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
    sx_mem_set(&server, 0, sizeof(struct Server));
    {
        struct Config config = { 0 };
        config.port = 31000;
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

        if (sx_str_cmp(cmd, "report server\n") == 0)
            server_report();

        if (sx_str_str(cmd, "report room") != null)
        {
            //int index = scanf()
            room_report(&server, 0);
        }

        sx_sleep(1);
    }

    for (size_t i = 0; i < THREAD_COUNTS; i++)
        sx_thread_destroy(threads[i]);

    server_shutdown();

    sx_trace_detach();
    return 0;
}
