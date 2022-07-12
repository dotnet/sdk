cmake_minimum_required(VERSION 3.15.5)

# Create project named finalizer, this will
# will generate Finalizer.vcxproj
project(Finalizer)

set(CMAKE_MACOSX_RPATH 1)
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_FIND_LIBRARY_SUFFIXES ".lib")
set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} /SUBSYSTEM:WINDOWS /ENTRY:wmainCRTStartup") 

# The WiX SDK is extracted from a NuGet package using an SDK .csproj (finalizer_shim)
# that copies the "lib" and "inc" folders to a stable location.

include_directories(../../artifacts/WixSdk/inc)
include_directories(../../artifacts/obj)
link_directories(../../artifacts/WixSdk/lib/${Platform})

add_compile_options(/MT)

add_executable(Finalizer 
  finalizer.cpp
  native.rc
)

# These are normally part of a .vcxproj in Visual Studio, but 
# appears to be missing when CMAKE generates a .vcxproj
# for arm64.
target_link_libraries(Finalizer shell32.lib)
target_link_libraries(Finalizer advapi32.lib)
target_link_libraries(Finalizer version.lib)
target_link_libraries(Finalizer msi.lib)

# Add WiX libraries
target_link_libraries(Finalizer wcautil.lib)
target_link_libraries(Finalizer dutil.lib)

install(TARGETS Finalizer)
