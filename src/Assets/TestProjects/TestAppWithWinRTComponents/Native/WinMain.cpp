#include "pch.h"

using namespace winrt;
using namespace Windows::Foundation;
using namespace Coords;
using namespace Posns;

int __stdcall wWinMain(HINSTANCE, HINSTANCE, LPWSTR, int)
{
    init_apartment(apartment_type::single_threaded);

    Coord a = Coord();
    Coord b = Coord(39.0, 80.0);

    std::wostringstream coordstringstream;
    coordstringstream << L"Coord test: " << a.ToString().c_str() << L" and " << b.ToString().c_str() << L" --> " << a.Distance(b) << std::endl;

    ::MessageBoxW(::GetDesktopWindow(), coordstringstream.str().c_str(), L"C++/WinRT Desktop Application", MB_OK);

    Posn x = Posn();
    Posn y = Posn(39.0, 80.0);

    std::wostringstream posnstringstream;
    posnstringstream << L"Posn test: " << x.ToString().c_str() << L" and " << y.ToString().c_str() << L" --> " << x.Distance(y) << std::endl;

    ::MessageBoxW(::GetDesktopWindow(), posnstringstream.str().c_str(), L"C++/WinRT Desktop Application", MB_OK);
}
