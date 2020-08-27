using System;

namespace console_project
{
    public enum DaysEnumNeedsFlags
    {
        None = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 4,
        Thursday = 8,
        Friday = 16,
        All = Monday | Tuesday | Wednesday | Thursday | Friday
    }
}
