cmake_minimum_required(VERSION 3.20)

# Create project named finalizer, this will generate Finalizer.vcxproj
project(Finalizer)

set(CMAKE_MACOSX_RPATH 1)
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_FIND_LIBRARY_SUFFIXES ".lib")
set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} /SUBSYSTEM:WINDOWS /ENTRY:wmainCRTStartup") 

# The WiX SDK is extracted from a NuGet package using an SDK .csproj (finalizer-build) that copies the "lib" and "inc" folders to a stable location.
# The defines below (values in ${}) are set in the finalizer.nativeproj.
# See: https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.CMake.Sdk#common-items
# Note: The directory paths use forward slashes because backslashes are escape characters.
include_directories(${ArtifactsDir}WixSdk/inc)
include_directories(${ArtifactsDir}obj)
link_directories(${ArtifactsDir}WixSdk/lib/${Platform})

add_compile_options(/MT)

# Microsoft.Security.SystemsADM.10086
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/W3>)
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/WX>)
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4018>) # 'expression' : signed/unsigned mismatch
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4055>) # 'conversion' : from data pointer 'type1' to function pointer 'type2'
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4146>) # unary minus operator applied to unsigned type, result still unsigned
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4242>) # 'identifier' : conversion from 'type1' to 'type2', possible loss of data
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4244>) # 'conversion' conversion from 'type1' to 'type2', possible loss of data
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4267>) # 'var' : conversion from 'size_t' to 'type', possible loss of data
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4302>) # 'conversion' : truncation from 'type 1' to 'type 2'
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4308>) # negative integral constant converted to unsigned type
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4509>) # nonstandard extension used: 'function' uses SEH and 'object' has destructor
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4510>) # 'class' : default constructor could not be generated
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4532>) # 'continue' : jump out of __finally/finally block has undefined behavior during termination handling
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4533>) # initialization of 'variable' is skipped by 'instruction'
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4610>) # object 'class' can never be instantiated - user-defined constructor required
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4611>) # interaction between 'function' and C++ object destruction is non-portable
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4700>) # uninitialized local variable 'name' used
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4701>) # Potentially uninitialized local variable 'name' used
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4703>) # Potentially uninitialized local pointer variable 'name' used
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4789>) # destination of memory copy is too small
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4995>) # 'function': name was marked as #pragma deprecated
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/we4996>) # 'function': was declared deprecated also 'std::'
add_compile_options($<$<COMPILE_LANGUAGE:C,CXX>:/guard:cf>) # Enable control flow guard

add_executable(Finalizer 
  finalizer.cpp
  native.rc
)

add_link_options(/guard:cf)

# These are normally part of a .vcxproj in Visual Studio, but appears to be missing when CMAKE generates a .vcxproj for arm64.
target_link_libraries(Finalizer shell32.lib)
target_link_libraries(Finalizer advapi32.lib)
target_link_libraries(Finalizer version.lib)
target_link_libraries(Finalizer msi.lib)
target_link_libraries(Finalizer shlwapi.lib)

# Add WiX libraries
target_link_libraries(Finalizer wcautil.lib)
target_link_libraries(Finalizer dutil.lib)

install(TARGETS Finalizer)
