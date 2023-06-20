using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UtilityHelper
{
    public static class Utility
    {
        public static Vector3 MakeVector3(float value)
        {
            return new Vector3(value, value, value);
        }
    }
}
