namespace Library;

public static class LibraryClass
{
#if One
    public static string GetMessage() => "Hello from Library: One";
#else
    public static string GetMessage() => "Hello from Library: Two";
#endif
}
