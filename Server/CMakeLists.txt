# CMakeList.txt : CMake project for server, include source and define
# project specific logic here.
#

cmake_minimum_required(VERSION 3.8)

project(server)

file(GLOB server_files 
	"*.c"
	"core/*.c"
	"net/*.c"
	)

add_executable(server ${server_files})

