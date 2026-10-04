using System.Collections.Generic;
using System.Diagnostics;
using PhysicalDigital.Communication;
using PhysicalDigital.Protocols;
using UnityEngine;

namespace PhysicalDigital.Game
{
    public sealed class ControllerInput : MonoBehaviour
    {
        private const float KeyboardPotSpeed = 0.6f;

        private readonly List<ControllerState> batch = new List<ControllerState>();
        private readonly List<int> pressedThisFrame = new List<int>();
        private readonly Queue<int> pendingKeyPresses = new Queue<int>();
        private readonly bool[] keyHeld = new bool[ControllerState.ButtonCount];
        private SerialLink link;
        private bool keyPotUp;
        private bool keyPotDown;
        private float simulatedPot01 = 0.5f;

        private byte serialButtons;
        private int serialPot;
        private bool hasSerialData;

        private double queueLatencySumMs;
        private long queueLatencyCount;

        public IReadOnlyList<int> PressedThisFrame => pressedThisFrame;
        public bool UsingSerial => hasSerialData;
        public int PotRaw => hasSerialData ? serialPot : Mathf.RoundToInt(simulatedPot01 * ControllerState.PotMax);
        public float Pot01 => PotRaw / (float)ControllerState.PotMax;

        public double AvgQueueLatencyMs => queueLatencyCount > 0 ? queueLatencySumMs / queueLatencyCount : 0.0;

        public bool IsHeld(int index)
        {
            return (serialButtons & (1 << index)) != 0 || keyHeld[index];
        }

        public void ResetQueueLatency()
        {
            queueLatencySumMs = 0.0;
            queueLatencyCount = 0;
        }

        private void Start()
        {
            link = GetComponent<SerialLink>();
        }

        private void Update()
        {
            pressedThisFrame.Clear();
            while (pendingKeyPresses.Count > 0)
            {
                pressedThisFrame.Add(pendingKeyPresses.Dequeue());
            }

            batch.Clear();
            link.Drain(batch);
            long now = Stopwatch.GetTimestamp();
            foreach (ControllerState state in batch)
            {
                int rising = state.Buttons & ~serialButtons;
                for (int i = 0; i < ControllerState.ButtonCount; i++)
                {
                    if ((rising & (1 << i)) != 0)
                    {
                        pressedThisFrame.Add(i);
                    }
                }
                serialButtons = state.Buttons;
                serialPot = state.Pot;
                hasSerialData = true;
                queueLatencySumMs += SerialLink.TicksToMs(now - state.RxTimestamp);
                queueLatencyCount++;
            }

            if (!link.IsOpen)
            {
                serialButtons = 0;
                hasSerialData = false;
            }

            float direction = (keyPotUp ? 1f : 0f) - (keyPotDown ? 1f : 0f);
            simulatedPot01 = Mathf.Clamp01(simulatedPot01 + direction * KeyboardPotSpeed * Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown && e.type != EventType.KeyUp)
            {
                return;
            }
            bool down = e.type == EventType.KeyDown;

            int index = KeyToButton(e.keyCode);
            if (index >= 0)
            {
                if (down && !keyHeld[index])
                {
                    pendingKeyPresses.Enqueue(index);
                }
                keyHeld[index] = down;
            }
            else if (e.keyCode == KeyCode.UpArrow)
            {
                keyPotUp = down;
            }
            else if (e.keyCode == KeyCode.DownArrow)
            {
                keyPotDown = down;
            }
        }

        private static int KeyToButton(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                    return 0;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    return 1;
                case KeyCode.Alpha3:
                case KeyCode.Keypad3:
                    return 2;
                case KeyCode.Alpha4:
                case KeyCode.Keypad4:
                    return 3;
                default:
                    return -1;
            }
        }
    }
}
