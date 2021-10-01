// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma once

#include <windows.h>
#include <winerror.h>
#include <iostream>
#include <errno.h>
#include <wchar.h>
#include <cwchar>
#include <winreg.h>
#include <msi.h>
#include <pathcch.h>

// Configure some logging parameters for WiX
#define ExitTrace LogErrorString
#define ExitTrace1 LogErrorString
#define ExitTrace2 LogErrorString
#define ExitTrace3 LogErrorString

// Includes from WiX SDK
#include "dutil.h"
#include "regutil.h"
#include "logutil.h"
#include "pathutil.h"
#include "strutil.h"
#include "wiutil.h"
