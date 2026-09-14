using System;
using UnityEngine;

namespace for_code_formatter
{
    class Program : MonoBehaviour
    {
        // This method should trigger IDE0051 (remove unused private member) in a regular project.
        // But given we simulate a Unity MonoBehavior and we include Microsoft.Unity.Analyzers nuget,
        // given Update is a well-known Unity message, this IDE0051 should be suppressed by USP0003.
        // see https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/USP0003.md
        void Update()
        {

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
}
