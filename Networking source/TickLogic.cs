using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualVoid.Networking
{
    public class TickLogic : MonoBehaviour
    {
        public static long tick;
        public static long delayTick;

        public float ticksPerSecond = 32;
        public static float secPerTick;

        private void Start()
        {
            Time.fixedDeltaTime = 1f / ticksPerSecond;
            secPerTick = 1f / ticksPerSecond;
        }

        private void FixedUpdate()
        {
            tick++;
            delayTick = tick - 3;
        }
    }

}
