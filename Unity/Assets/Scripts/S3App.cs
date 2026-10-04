using PhysicalDigital.Communication;
using PhysicalDigital.Game;
using PhysicalDigital.Measurements;
using PhysicalDigital.UI;
using UnityEngine;

namespace PhysicalDigital
{
    [RequireComponent(typeof(SerialLink), typeof(ControllerInput), typeof(ToneBank))]
    [RequireComponent(typeof(SequenceGame), typeof(MeasurementRunner), typeof(Hud))]
    public sealed class S3App : MonoBehaviour
    {
        private static readonly Color[] PadColors =
        {
            new Color(0.20f, 0.80f, 0.42f),
            new Color(0.93f, 0.27f, 0.30f),
            new Color(0.98f, 0.80f, 0.22f),
            new Color(0.25f, 0.55f, 0.98f),
        };
        private static readonly int[] PadSlots = { 3, 2, 0, 1 };
        private static readonly Color Background = new Color(0.055f, 0.065f, 0.11f);
        private static readonly Color StageColor = new Color(0.10f, 0.12f, 0.19f);
        private const float CameraSize = 5.4f;
        private const float CameraDepth = -10f;
        private const float PadSize = 2.0f;
        private const float PadSpacing = 2.45f;
        private const float PadY = 0.2f;
        private const float StagePaddingX = 0.5f;
        private const float StagePaddingY = 0.9f;

        public PadView[] Pads { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureApp()
        {
            if (FindAnyObjectByType<S3App>() == null)
            {
                new GameObject("S3App").AddComponent<S3App>();
            }
        }

        private void Awake()
        {
            Application.runInBackground = true;
            SetupCamera();
            BuildStage();
            GetComponent<SequenceGame>().Init(GetComponent<ControllerInput>(), Pads, GetComponent<ToneBank>());
        }

        private static void SetupCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = cameraObject.AddComponent<Camera>();
            }
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.transform.position = new Vector3(0f, 0f, CameraDepth);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                camera.gameObject.AddComponent<AudioListener>();
            }
        }

        private void BuildStage()
        {
            Transform stage = new GameObject("Stage").transform;
            stage.SetParent(transform, false);

            SpriteRenderer plate = new GameObject("Base").AddComponent<SpriteRenderer>();
            plate.transform.SetParent(stage, false);
            plate.transform.localPosition = new Vector3(0f, PadY, 0f);
            plate.sprite = SpriteFactory.RoundedPanel;
            plate.drawMode = SpriteDrawMode.Sliced;
            plate.size = new Vector2(PadSpacing * (PadColors.Length - 1) + PadSize + StagePaddingX, PadSize + StagePaddingY);
            plate.color = StageColor;
            plate.sortingOrder = 0;

            Pads = new PadView[PadColors.Length];
            for (int i = 0; i < PadColors.Length; i++)
            {
                float x = (PadSlots[i] - (PadColors.Length - 1) / 2f) * PadSpacing;
                Pads[i] = PadView.Create(stage, $"Pad B{i + 1}", new Vector3(x, PadY, 0f), PadSize, PadColors[i]);
            }
        }
    }
}
