// server.cpp : Defines the entry point for the application.
//

#include "server.h"
#include "net/net.h"
#include "net/socket.h"
#include "core/string.h"
#include "core/memory.h"
#include "core/trace.h"

struct Server server = { 0 };

struct Server* server_get(void)
{
    return &server;
}

void server_cleanup(void)
{
    sx_trace();
    for (uint i = 0; i < ROOM_COUNT; i++)
        room_cleanup(&server.rooms[i], server.config.player_timeout);
    sx_return();
}

void server_shutdown(void)
{
    if (server.socket_send > 0) sx_socket_close(server.socket_send);
    if (server.socket_listener > 0) sx_socket_close(server.socket_listener);
    if (server.mutex != null) sx_mutex_destroy(server.mutex);
}

void server_reset(struct Config config)
{
    sx_trace();

    server.config = config;

    if (server.socket_send > 0) sx_socket_close(server.socket_send);
    if (server.socket_listener > 0) sx_socket_close(server.socket_listener);
    printf("Initializing listener: ");
    server.socket_listener = sx_socket_open(config.listener_port, true, false);
    printf("Initializing sender: ");
    server.socket_send = sx_socket_open(config.send_port, false, false);

    for (size_t i = 0; i < ROOM_COUNT; i++)
        server.rooms[i].capacity = config.room_capacity;

    if (server.mutex == null)
        server.mutex = sx_mutex_create();

    sx_return();
}

void server_send(const uint ip, const word port, const void* buffer, const int size)
{
    sx_socket_send(server.socket_send, ip, port, buffer, size);
}

void thread_ticker(void* param)
{
    sx_trace_attach(64, "trace_worker.txt");
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
        byte buffer[256] = { 0 };
        sx_socket_receive(server.socket_listener, buffer, 255, null);
        switch (buffer[0])
        {
        case TYPE_MESSAGE: server_packet(buffer); break;
        case TYPE_LOGIN: server_login(buffer); break;
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
    {
        struct Config config = { 0 };
        config.listener_port = 31000;
        config.send_port = 31001;
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
            room_report(0);
        }

        sx_sleep(1);
    }

    for (size_t i = 0; i < THREAD_COUNTS; i++)
        sx_thread_destroy(threads[i]);

    server_shutdown();

    sx_trace_detach();
    return 0;
}
