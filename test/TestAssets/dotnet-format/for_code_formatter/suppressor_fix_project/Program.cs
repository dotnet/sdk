using System;
using UnityEngine;

namespace for_code_formatter
{
    class Program : MonoBehaviour
    {
        [SerializeField] int notReadOnly;

        public int Foo()
        {
            return notReadOnly;
        }
    }
}

namespace UnityEngine
{
    public class MonoBehaviour
    {
        // This is a placeholder for the Unity MonoBehaviour class.
        // In a real Unity project, this would be part of the Unity engine.
    }

    public class SerializeField : Attribute
    {
    }
}
