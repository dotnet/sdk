using MyApp.Test;

//Stuff
namespace MyApp
{
    public class DefaultTrueIncluded { }

    public class DefaultFalseIncluded { }

#if DEBUG1
    public class InsideUnknownDirectiveNoEmit { }
#endif

//-:cnd
#if DEBUG2
    public class InsideUnknownDirectiveEmit { }
#endif
//+:cnd
}