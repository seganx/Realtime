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
#include "core/Json.h"

Server server = { 0 };

uint server_get_token()
{
    uint result;
    sx_mutex_lock(server.mutex_token);
    server.token++;
    if (server.token == 0) server.token++;
    result = server.token;
    sx_mutex_unlock(server.mutex_token);
    return result;
}

void server_init()
{
    sx_trace();

    sx_mem_set(&server, 0, sizeof(Server));
    server.token = 654987;

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

    sx_return();
}

void server_cleanup(void)
{
    sx_trace();

    ulong now = sx_time_now();
    sx_mutex_lock(server.mutex_lobby);

    for (short i = 0; i < LOBBY_CAPACITY; i++)
    {
        Player* player = &server.lobby.players[i];
        if (player->token > 0 && sx_time_diff(now, player->active_time) > server.config.player_timeout)
        {
            sx_mutex_lock(server.mutex_room);
            room_remove_player(&server, player);
            sx_mutex_unlock(server.mutex_room);

            lobby_remove_player(&server, i);
        }
    }

    sx_mutex_unlock(server.mutex_lobby);
    sx_return();
}

void server_rooms_update()
{
    sx_trace();

    ulong now = sx_time_now();
    sx_mutex_lock(server.mutex_room);
    for (short i = 0; i < ROOM_COUNT; i++)
        room_check_master(&server, now, i);
    sx_mutex_unlock(server.mutex_room);

    sx_return();
}

void server_send(const byte* address, const void* buffer, const int size)
{
    sx_socket_send_in(server.socket, (const struct sockaddr*)address, buffer, size);
}

void server_send_error(const byte* from, const byte type, const sbyte error)
{
    ErrorResponse response = { type, error };
    server_send(from, &response, sizeof(ErrorResponse));
}

void server_ping(byte* buffer, const byte* from)
{
    Ping* ping = (Ping*)buffer;

    Player* player = lobby_get_player_validate_all(&server, ping->token, ping->id, ping->room, ping->index);
    if (player == null)
    {
        server_send_error(from, TYPE_PING, ERR_EXPIRED);
        return;
    }

    ulong now = sx_time_now();
    sx_mutex_lock(server.mutex_lobby);
    sx_mem_copy(player->from, from, ADDRESS_LEN);
    player->active_time = now;
    sx_mutex_unlock(server.mutex_lobby);

    PingResponse response = { TYPE_PING, 0, ping->time, now, player->flag };
    server_send(from, &response, sizeof(PingResponse));
}

void server_process_login(byte* buffer, const byte* from)
{
    if (server.lobby.count >= LOBBY_CAPACITY)
    {
        server_send_error(from, TYPE_LOGIN, ERR_IS_FULL);
        return;
    }

    Login* login = (Login*)buffer;
    if (checksum_is_invalid(buffer, sizeof(Login) - sizeof(uint), login->checksum)) return;

    Player* player = lobby_find_player_by_device(&server, login->device);
    if (player == null)
    {
        sx_mutex_lock(server.mutex_lobby);
        player = lobby_add_player(&server, login->device, from, server_get_token());
        sx_mutex_unlock(server.mutex_lobby);
    }
    if (player == null)
    {
        server_send_error(from, TYPE_LOGIN, ERR_IS_FULL);
        return;
    }

    LoginResponse response = { TYPE_LOGIN, 0, player->token, player->id, player->room, player->index };
    response.checksum = checksum_compute((const byte*)&response, sizeof(LoginResponse) - sizeof(uint));
    server_send(from, &response, sizeof(LoginResponse));
}

void server_process_logout(byte* buffer, const byte* from)
{
    Logout* logout = (Logout*)buffer;
    if (validate_player_id_range(logout->id) == false)
        return;

    if (checksum_is_invalid(buffer, sizeof(Logout) - sizeof(uint), logout->checksum))
        return;

    Player* player = lobby_get_player_validate_all(&server, logout->token, logout->id, logout->room, logout->index);
    if (player == null)
    {
        server_send_error(from, TYPE_LOGOUT, 0);
        return;
    }

    sx_mutex_lock(server.mutex_room);
    room_remove_player(&server, player);
    sx_mutex_unlock(server.mutex_room);

    sx_mutex_lock(server.mutex_lobby);
    lobby_remove_player(&server, logout->id);
    sx_mutex_unlock(server.mutex_lobby);

    server_send_error(from, TYPE_LOGOUT, 0);
}

void server_process_create(byte* buffer, const byte* from)
{
    Create* request = (Create*)buffer;

    Player* player = lobby_get_player_validate_token(&server, request->token, request->id);
    if (player == null)
    {
        server_send_error(from, TYPE_CREATE, ERR_EXPIRED);
        return;
    }

    sx_mutex_lock(server.mutex_room);
    if (player->room < 0)
    {
        room_create(&server, player, request->open_timeout * 1000, request->properties, request->matchmaking);
    }
    sx_mutex_unlock(server.mutex_room);

    if (player->room < 0)
    {
        server_send_error(from, TYPE_CREATE, ERR_IS_FULL);
        return;
    }
    
    sx_mutex_lock(server.mutex_room);
    room_check_master(&server, sx_time_now(), player->room);
    sx_mutex_unlock(server.mutex_room);

    CreateResponse response = { TYPE_CREATE, 0, player->room, player->index, player->flag };
    server_send(from, &response, sizeof(CreateResponse));
}

void server_process_join(byte* buffer, const byte* from)
{
    Join* request = (Join*)buffer;

    Player* player = lobby_get_player_validate_token(&server, request->token, request->id);
    if (player == null)
    {
        server_send_error(from, TYPE_JOIN, ERR_EXPIRED);
        return;
    }

    sx_mutex_lock(server.mutex_room);
    if (player->room < 0)
    {
        room_join(&server, player, request->matchmaking);
    }
    sx_mutex_unlock(server.mutex_room);

    if (player->room < 0)
    {
        server_send_error(from, TYPE_JOIN, ERR_MATCHMAKE);
        return;
    }

    JoinResponse response = { TYPE_JOIN, 0, player->room, player->index, player->flag };
    
    sx_mutex_lock(server.mutex_room);
    room_check_master(&server, sx_time_now(), player->room);
    sx_mem_copy(response.properties, server.rooms[player->room].properties, ROOM_PROP_LEN);
    sx_mutex_unlock(server.mutex_room);

    server_send(from, &response, sizeof(JoinResponse));
}

void server_process_leave(byte* buffer, const byte* from)
{
    Leave* leave = (Leave*)buffer;

    Player* player = lobby_get_player_validate_all(&server, leave->token, leave->id, leave->room, leave->index);
    if (player != null)
    {
        sx_mutex_lock(server.mutex_room);
        room_remove_player(&server, player);
        sx_mutex_unlock(server.mutex_room);
    }

    LeaveResponse response = { TYPE_LEAVE, 0 };
    server_send(from, &response, sizeof(LeaveResponse));
}


void server_process_packet_unreliable(byte* buffer, const byte* from)
{
    PacketUnreliable* packet = (PacketUnreliable*)buffer;
    if (validate_player_index_range(packet->index) == false) return;
    if (validate_player_room_id_range(packet->room) == false) return;

    Player* player = lobby_get_player_validate_all(&server, packet->token, packet->id, packet->room, packet->index);
    if (player == null)
    {
        server_send_error(from, TYPE_PACKET_UNRELY, ERR_EXPIRED);
        return;
    }

    Room* room = &server.rooms[packet->room];
    if (packet->target == -1)
    {
        int sender = packet->index;
        int packetsize = packet->datasize + 3;
        buffer += sizeof(PacketUnreliable) - 3;
        buffer[0] = TYPE_PACKET_UNRELY;
        buffer[1] = sender;
        //buffer[2] = packet->datasize;  no need to rewrite data size
        for (uint i = 0; i < ROOM_CAPACITY; i++)
        {
            if (i == sender) continue;
            Player* other = room->players[i];
            if (other != null && other->token > 0)
                server_send(other->from, buffer, packetsize);
        }
    }
    else if (packet->target == -2)
    {
        int sender = packet->index;
        int packetsize = packet->datasize + 3;
        buffer += sizeof(PacketUnreliable) - 3;
        buffer[0] = TYPE_PACKET_UNRELY;
        buffer[1] = sender;
        //buffer[2] = packet->datasize;  no need to rewrite data size
        for (uint i = 0; i < ROOM_CAPACITY; i++)
        {
            Player* other = room->players[i];
            if (other != null && other->token > 0)
                server_send(other->from, buffer, packetsize);
        }
    }
    else if (validate_player_index_range(packet->target))
    {
        Player* other = room->players[packet->target];
        if (other != null && other->token > 0)
        {
            int sender = packet->index;
            int packetsize = packet->datasize + 3;
            buffer += sizeof(PacketUnreliable) - 3;
            buffer[0] = TYPE_PACKET_UNRELY;
            buffer[1] = sender;
            //buffer[2] = packet->datasize;  no need to rewrite data size
            server_send(other->from, buffer, packetsize);
        }
    }
}

void server_process_packet_reliable(byte* buffer, const byte* from)
{
    PacketReliable* packet = (PacketReliable*)buffer;
    if (validate_player_index_range(packet->index) == false) return;
    if (validate_player_room_id_range(packet->room) == false) return;
    if (validate_player_index_range(packet->target) == false) return;

    Player* player = lobby_get_player_validate_all(&server, packet->token, packet->id, packet->room, packet->index);
    if (player == null)
    {
        server_send_error(from, TYPE_PACKET_RELY, ERR_EXPIRED);
        return;
    }

    Room* room = &server.rooms[packet->room];
    Player* other = room->players[packet->target];
    if (other == null || other->token == 0)
    {
        sbyte target = packet->target;
        byte ack = packet->ack;
        // fake response to sender to stop trying
        buffer[0] = TYPE_PACKET_RELIED;
        buffer[1] = target;
        buffer[2] = ack;
        server_send(player->from, buffer, 3);
    }
    else
    {
        sbyte index = packet->index;
        byte ack = packet->ack;
        int packetsize = packet->datasize + 4;
        buffer += sizeof(PacketReliable) - 4;
        buffer[0] = TYPE_PACKET_RELY;
        buffer[1] = index;
        buffer[2] = ack;
        //buffer[3] = packet->datasize;  no need to rewrite data size
        server_send(other->from, buffer, packetsize);
    }
}

void server_process_packet_relied(byte* buffer, const byte* from)
{
    PacketRelied* packet = (PacketRelied*)buffer;
    if (validate_player_index_range(packet->index) == false) return;
    if (validate_player_room_id_range(packet->room) == false) return;
    if (validate_player_index_range(packet->target) == false) return;

    Player* player = lobby_get_player_validate_all(&server, packet->token, packet->id, packet->room, packet->index);
    if (player == null)
    {
        server_send_error(from, TYPE_PACKET_RELIED, ERR_EXPIRED);
        return;
    }

    Room* room = &server.rooms[packet->room];
    Player* other = room->players[packet->target];
    if (other != null && other->token > 0)
    {
        sbyte index = packet->index;
        byte ack = packet->ack;
        buffer[0] = TYPE_PACKET_RELIED;
        buffer[1] = index;
        buffer[2] = ack;
        server_send(other->from, buffer, 3);
    }
}

void server_report(void)
{
    sx_print("Total players connected: %d", server.lobby.count);

    int total_rooms = 0, total_players = 0;
    for (uint r = 0; r < ROOM_COUNT; r++)
    {
        if (server.rooms[r].count < 1) continue;
        sx_print("Room[%d] -> %d players", r, server.rooms[r].count);
        total_rooms++;
        total_players += server.rooms[r].count;
    }
    sx_print("Total active rooms: %d\nTotal players in room: %d", total_rooms, total_players);
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
        server_rooms_update();
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
        byte buffer[1024] = { 0 };
        sx_socket_receive(server.socket, buffer, 512, (struct sockaddr*)from);

        switch (buffer[0])
        {
        case TYPE_PING: server_ping(buffer, from); break;
        case TYPE_PACKET_UNRELY: server_process_packet_unreliable(buffer, from); break;
        case TYPE_PACKET_RELY: server_process_packet_reliable(buffer, from); break;
        case TYPE_PACKET_RELIED: server_process_packet_relied(buffer, from); break;
        case TYPE_LOGIN: server_process_login(buffer, from); break;
        case TYPE_LOGOUT: server_process_logout(buffer, from); break;
        case TYPE_CREATE: server_process_create(buffer, from); break;
        case TYPE_JOIN: server_process_join(buffer, from); break;
        case TYPE_LEAVE: server_process_leave(buffer, from); break;
        }
    }

    sx_trace_detach();
}

Config LoadConfig()
{
    sx_trace();

    Config config = { 0 };
    config.port = 36000;
    config.room_capacity = ROOM_CAPACITY;
    config.player_timeout = 300000;
    config.player_master_timeout = 5000;

    FILE* file = null;
    if (fopen_s(&file, "config.json", "r") == 0)
    {
        char json_string[1024] = { 0 };
        fread_s(json_string, sizeof(json_string), 1, sizeof(json_string), file);

        sx_json_node json_nodes[64] = { 0 };
        sx_json json = { 0 };
        json.nodes = json_nodes;
        json.nodescount = 64;

        sx_json_node* root = sx_json_parse(&json, json_string, sx_str_len(json_string));

        config.port = sx_json_read_int(root, "port", config.port);
        config.room_capacity = sx_json_read_int(root, "room_capacity", config.room_capacity);
        config.player_timeout = sx_json_read_int(root, "player_timeout", config.player_timeout);
        config.player_master_timeout = sx_json_read_int(root, "player_master_timeout", config.player_master_timeout);

        fclose(file);
    }

    sx_return(config);
}

int main()
{
    sx_trace_attach(64, "trace.txt");
    sx_trace();
    sx_net_initialize();

    // initialize server with default config
    server_init();
    {
        Config config = LoadConfig();
        server_reset(config);

        char t[64] = { 0 };
        sx_time_print(t, 64, sx_time_now());
        sx_print("server started on %s", t);
        sx_print("port: %d", config.port);
        sx_print("room capacity: %d", config.room_capacity);
        sx_print("player timeout: %d", config.player_timeout);
        sx_print("player master timeout: %d", config.player_master_timeout);
    }

    struct sx_thread* threads[THREAD_COUNTS] = { null };
    threads[0] = sx_thread_create(1, thread_ticker, null);
    for (int i = 1; i < THREAD_COUNTS; i++)
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
            else if (sx_str_cmp(cmd2, "time") == 0)
                sx_print("%llu", sx_time_now());
        }

        sx_sleep(1);
    }

    for (size_t i = 0; i < THREAD_COUNTS; i++)
        sx_thread_destroy(threads[i]);

    server_shutdown();

    sx_trace_detach();
    return 0;
}
