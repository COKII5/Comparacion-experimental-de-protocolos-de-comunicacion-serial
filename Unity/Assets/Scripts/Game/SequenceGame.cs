using System.Collections.Generic;
using UnityEngine;

namespace PhysicalDigital.Game
{
    public sealed class SequenceGame : MonoBehaviour
    {
        public enum Phase
        {
            Attract,
            Showing,
            Input,
            RoundWon,
            GameOver,
        }

        private const float SlowestStep = 0.90f;
        private const float FastestStep = 0.22f;
        private const float SlowestTimeout = 5.0f;
        private const float FastestTimeout = 2.0f;
        private const float LitFraction = 0.65f;
        private const float FirstRoundLeadIn = 0.8f;
        private const float NextRoundLeadIn = 0.35f;
        private const float RoundWonPause = 0.8f;
        private const float GameOverLockout = 1.0f;
        private const float PressFlashSeconds = 0.2f;
        private const float ErrorFlashSeconds = 0.6f;
        private const float HintFlashSeconds = 1.2f;
        private const string BestScoreKey = "S3_BestScore";

        private readonly List<int> sequence = new List<int>();
        private ControllerInput input;
        private PadView[] pads;
        private ToneBank tones;
        private int showIndex;
        private bool showLit;
        private int inputIndex;
        private float timer;
        private float inputTimer;

        public Phase CurrentPhase { get; private set; } = Phase.Attract;
        public int Round => sequence.Count;
        public int Progress => inputIndex;
        public int Score { get; private set; }
        public int Best { get; private set; }
        public string Message { get; private set; } = "";

        public float StepSeconds => Mathf.Lerp(SlowestStep, FastestStep, input.Pot01);
        public float InputTimeout => Mathf.Lerp(SlowestTimeout, FastestTimeout, input.Pot01);
        public float InputTimeLeft01 => CurrentPhase == Phase.Input ? Mathf.Clamp01(inputTimer / InputTimeout) : 0f;

        public void Init(ControllerInput controllerInput, PadView[] padViews, ToneBank toneBank)
        {
            input = controllerInput;
            pads = padViews;
            tones = toneBank;
            Best = PlayerPrefs.GetInt(BestScoreKey, 0);
            EnterAttract();
        }

        private void Update()
        {
            if (input == null)
            {
                return;
            }
            float deltaTime = Time.deltaTime;

            switch (CurrentPhase)
            {
                case Phase.Attract:
                    AnimateAttract();
                    ShowHeldButtons();
                    if (input.PressedThisFrame.Count > 0)
                    {
                        StartGame();
                    }
                    break;

                case Phase.Showing:
                    UpdateShowing(deltaTime);
                    break;

                case Phase.Input:
                    ShowHeldButtons();
                    UpdateInput(deltaTime);
                    break;

                case Phase.RoundWon:
                    timer -= deltaTime;
                    if (timer <= 0f)
                    {
                        AddStep();
                        BeginShowing(NextRoundLeadIn);
                    }
                    break;

                case Phase.GameOver:
                    ShowHeldButtons();
                    timer -= deltaTime;
                    if (timer <= 0f && input.PressedThisFrame.Count > 0)
                    {
                        StartGame();
                    }
                    break;
            }
        }

        private void EnterAttract()
        {
            CurrentPhase = Phase.Attract;
            Message = "Press any button to start";
        }

        private void StartGame()
        {
            sequence.Clear();
            Score = 0;
            foreach (PadView pad in pads)
            {
                pad.SetIdle(0f);
            }
            AddStep();
            BeginShowing(FirstRoundLeadIn);
        }

        private void AddStep()
        {
            sequence.Add(Random.Range(0, pads.Length));
        }

        private void BeginShowing(float leadIn)
        {
            CurrentPhase = Phase.Showing;
            Message = "Watch the sequence...";
            showIndex = 0;
            showLit = false;
            timer = leadIn;
            foreach (PadView pad in pads)
            {
                pad.SetHeld(false);
            }
        }

        private void UpdateShowing(float deltaTime)
        {
            timer -= deltaTime;
            if (timer > 0f)
            {
                return;
            }

            float step = StepSeconds;
            if (!showLit)
            {
                if (showIndex >= sequence.Count)
                {
                    CurrentPhase = Phase.Input;
                    inputIndex = 0;
                    inputTimer = InputTimeout;
                    Message = "Your turn!";
                    return;
                }
                int pad = sequence[showIndex];
                pads[pad].Flash(step * LitFraction);
                tones.PlayPad(pad);
                showLit = true;
                timer = step * LitFraction;
            }
            else
            {
                showLit = false;
                showIndex++;
                timer = step * (1f - LitFraction);
            }
        }

        private void UpdateInput(float deltaTime)
        {
            inputTimer -= deltaTime;
            if (inputTimer <= 0f)
            {
                GameOver("Time's up!");
                return;
            }

            foreach (int pressed in input.PressedThisFrame)
            {
                pads[pressed].Flash(PressFlashSeconds);
                tones.PlayPad(pressed);

                if (pressed != sequence[inputIndex])
                {
                    GameOver("Wrong sequence!");
                    return;
                }

                inputIndex++;
                inputTimer = InputTimeout;
                Message = $"Your turn! {inputIndex}/{sequence.Count}";
                if (inputIndex == sequence.Count)
                {
                    Score = sequence.Count;
                    if (Score > Best)
                    {
                        Best = Score;
                        PlayerPrefs.SetInt(BestScoreKey, Best);
                    }
                    CurrentPhase = Phase.RoundWon;
                    Message = "Correct!";
                    timer = RoundWonPause;
                    return;
                }
            }
        }

        private void GameOver(string reason)
        {
            int expected = sequence[inputIndex];
            CurrentPhase = Phase.GameOver;
            Message = $"{reason}  You reached {Score} · Press a button to restart";
            tones.PlayError();
            foreach (PadView pad in pads)
            {
                pad.ErrorFlash(ErrorFlashSeconds);
            }
            pads[expected].Flash(HintFlashSeconds);
            timer = GameOverLockout;
        }

        private void ShowHeldButtons()
        {
            for (int i = 0; i < pads.Length; i++)
            {
                pads[i].SetHeld(input.IsHeld(i));
            }
        }

        private void AnimateAttract()
        {
            for (int i = 0; i < pads.Length; i++)
            {
                float phase = Time.time * 2.2f - i * 0.9f;
                pads[i].SetIdle(Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase)), 6f) * 0.55f);
            }
        }
    }
}
