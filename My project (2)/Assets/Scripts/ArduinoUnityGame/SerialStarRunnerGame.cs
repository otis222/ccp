using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArduinoUnityGame
{
    public sealed class SerialStarRunnerGame : MonoBehaviour
    {
        public struct UiReferences
        {
            public Text ScoreLabel;
            public Text TimerLabel;
            public Text MessageLabel;
            public Text SerialLabel;
            public Text HelpLabel;
            public InputField PortField;
            public Button ConnectButton;
            public Toggle GoalRespawnToggle;
        }

        private enum GameState
        {
            Playing,
            Won,
            Lost
        }

        private const int StartingHealth = 3;
        private const float StartingTime = 60f;
        private const string GoalRespawnPreferenceKey = "SerialStarRunner.GoalRespawnAtStart";

        private SerialInputReader serialInput;
        private SerialStarRunnerPlayer player;
        private SerialStarPickup[] pickups;
        private UiReferences ui;
        private GameState state;
        private int totalCores;
        private int collectedCores;
        private int completedLaps;
        private int score;
        private int health;
        private float timeRemaining;
        private float nextHitAllowedTime;
        private float temporaryMessageUntil;
        private Vector3 playerStartPosition;
        private Quaternion playerStartRotation;
        private string temporaryMessage = string.Empty;
        private bool runFeedbackSent;
        private bool respawnAtStartAfterGoal;

        public bool IsPlaying
        {
            get { return state == GameState.Playing; }
        }

        public void Configure(SerialInputReader inputReader, SerialStarRunnerPlayer runner, int collectibleCount, UiReferences references, SerialStarPickup[] pickupReferences = null)
        {
            serialInput = inputReader;
            player = runner;
            pickups = pickupReferences ?? new SerialStarPickup[0];
            totalCores = collectibleCount;
            ui = references;
            collectedCores = 0;
            completedLaps = 0;
            score = 0;
            health = StartingHealth;
            timeRemaining = StartingTime;
            state = GameState.Playing;
            respawnAtStartAfterGoal = PlayerPrefs.GetInt(GoalRespawnPreferenceKey, 0) == 1;

            if (player != null)
            {
                playerStartPosition = player.transform.position;
                playerStartRotation = player.transform.rotation;
            }

            if (ui.PortField != null)
            {
                ui.PortField.text = serialInput.PortName;
            }

            if (ui.ConnectButton != null)
            {
                ui.ConnectButton.onClick.RemoveListener(ConnectSerialFromUi);
                ui.ConnectButton.onClick.AddListener(ConnectSerialFromUi);
            }

            if (ui.GoalRespawnToggle != null)
            {
                ui.GoalRespawnToggle.onValueChanged.RemoveListener(SetRespawnAtStartAfterGoal);
                ui.GoalRespawnToggle.isOn = respawnAtStartAfterGoal;
                ui.GoalRespawnToggle.onValueChanged.AddListener(SetRespawnAtStartAfterGoal);
            }

            ShowTemporaryMessage("Collect all energy cores, then reach the green gate.", 4f);
            UpdateUi();
        }

        private void Update()
        {
            if (serialInput != null && serialInput.IsConnected && !runFeedbackSent)
            {
                serialInput.SendToArduino("LED:RUN");
                runFeedbackSent = true;
            }

            if (state == GameState.Playing)
            {
                timeRemaining -= Time.deltaTime;
                if (timeRemaining <= 0f)
                {
                    Lose("Time is up.");
                }
                else if (player != null && player.transform.position.y < -6f)
                {
                    Lose("You fell off the track.");
                }
            }
            else if (ShouldRestart())
            {
                RestartScene();
                return;
            }

            UpdateUi();
        }

        public void CollectCore(int points)
        {
            if (state != GameState.Playing)
            {
                return;
            }

            collectedCores++;
            score += points;

            if (collectedCores >= totalCores)
            {
                ShowTemporaryMessage("Gate unlocked. Reach the finish!", 3f);
            }
            else
            {
                ShowTemporaryMessage("+10 energy core", 1.3f);
            }
        }

        public void HitHazard(Vector3 hazardPosition)
        {
            if (state != GameState.Playing || Time.time < nextHitAllowedTime)
            {
                return;
            }

            nextHitAllowedTime = Time.time + 1.1f;
            health--;

            if (player != null)
            {
                player.KnockBackFrom(hazardPosition);
            }

            if (health <= 0)
            {
                Lose("Energy depleted.");
                return;
            }

            SendArduinoFeedback("LED:HIT");
            ShowTemporaryMessage("Careful. Hit a hazard!", 1.8f);
        }

        public void ReachGoal()
        {
            if (state != GameState.Playing)
            {
                return;
            }

            if (collectedCores < totalCores)
            {
                int remaining = totalCores - collectedCores;
                ShowTemporaryMessage("Need " + remaining + " more core" + (remaining == 1 ? "." : "s."), 2f);
                return;
            }

            score += Mathf.CeilToInt(timeRemaining);

            if (respawnAtStartAfterGoal)
            {
                RespawnForNextLap();
                return;
            }

            state = GameState.Won;
            if (player != null)
            {
                player.StopMotion();
            }

            SendArduinoFeedback("LED:WIN");
            ShowTemporaryMessage("Mission complete. Press jump or R to restart.", 999f);
        }

        private void Lose(string reason)
        {
            if (state == GameState.Lost)
            {
                return;
            }

            state = GameState.Lost;
            timeRemaining = Mathf.Max(0f, timeRemaining);
            if (player != null)
            {
                player.StopMotion();
            }

            SendArduinoFeedback("LED:LOSE");
            ShowTemporaryMessage(reason + " Press jump or R to restart.", 999f);
        }

        private void ConnectSerialFromUi()
        {
            if (serialInput == null)
            {
                return;
            }

            string requestedPort = ui.PortField == null ? serialInput.PortName : ui.PortField.text;
            serialInput.Connect(requestedPort);
            runFeedbackSent = false;
        }

        private void SetRespawnAtStartAfterGoal(bool enabled)
        {
            respawnAtStartAfterGoal = enabled;
            PlayerPrefs.SetInt(GoalRespawnPreferenceKey, enabled ? 1 : 0);
            PlayerPrefs.Save();

            ShowTemporaryMessage(enabled ? "Finish loop enabled. Goal returns you to start." : "Finish loop disabled. Goal completes the mission.", 2f);
        }

        private void RespawnForNextLap()
        {
            completedLaps++;
            collectedCores = 0;
            health = StartingHealth;
            timeRemaining = StartingTime;
            nextHitAllowedTime = Time.time + 0.75f;

            ResetPickups();

            if (player != null)
            {
                player.RespawnAt(playerStartPosition, playerStartRotation);
            }

            SimpleCameraFollow cameraFollow = Object.FindFirstObjectByType<SimpleCameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.SnapToTarget();
            }

            SendArduinoFeedback("LED:WIN");
            SendArduinoFeedback("LED:RUN");
            ShowTemporaryMessage("Lap " + completedLaps + " complete. Back to start.", 2.6f);
        }

        private void ResetPickups()
        {
            if (pickups == null)
            {
                return;
            }

            for (int i = 0; i < pickups.Length; i++)
            {
                if (pickups[i] != null)
                {
                    pickups[i].ResetPickup();
                }
            }
        }

        private bool ShouldRestart()
        {
            bool serialRestart = serialInput != null && serialInput.JumpPressed;
            return serialRestart || UnityEngine.Input.GetKeyDown(KeyCode.R) || UnityEngine.Input.GetKeyDown(KeyCode.Space);
        }

        private void RestartScene()
        {
            SendArduinoFeedback("LED:RUN");
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex >= 0 ? activeScene.buildIndex : 0);
        }

        private void SendArduinoFeedback(string message)
        {
            if (serialInput != null)
            {
                serialInput.SendToArduino(message);

                if (message == "LED:RUN")
                {
                    serialInput.SendToArduino("LED_OFF");
                }
                else if (message == "LED:HIT" || message == "LED:WIN" || message == "LED:LOSE")
                {
                    serialInput.SendToArduino("LED_ON");
                }
            }
        }

        private void ShowTemporaryMessage(string message, float seconds)
        {
            temporaryMessage = message;
            temporaryMessageUntil = Time.time + seconds;
        }

        private void UpdateUi()
        {
            if (ui.ScoreLabel != null)
            {
                ui.ScoreLabel.text = "Lap " + completedLaps + "   Cores " + collectedCores + "/" + totalCores + "   Score " + score;
            }

            if (ui.TimerLabel != null)
            {
                ui.TimerLabel.text = "Time " + Mathf.CeilToInt(timeRemaining) + "   HP " + Mathf.Max(health, 0);
            }

            if (ui.MessageLabel != null)
            {
                ui.MessageLabel.text = GetMessageText();
            }

            if (ui.SerialLabel != null && serialInput != null)
            {
                string portHint = BuildPortHint();
                ui.SerialLabel.text = serialInput.Status + "\nPort: " + serialInput.PortName + portHint + "\nLast: " + serialInput.LastLine;
            }

            if (ui.HelpLabel != null)
            {
                ui.HelpLabel.text = "Arduino: D2 left, D3 right, D2+D3 jump. Toggle Loop finish to return to start at the goal.";
            }
        }

        private string GetMessageText()
        {
            if (!string.IsNullOrEmpty(temporaryMessage) && Time.time <= temporaryMessageUntil)
            {
                return temporaryMessage;
            }

            if (state == GameState.Won)
            {
                return "You win. Press jump or R to restart.";
            }

            if (state == GameState.Lost)
            {
                return "Game over. Press jump or R to restart.";
            }

            return collectedCores >= totalCores ? "All cores collected. Go to the green gate." : "Collect the cores and avoid red hazards.";
        }

        private static string BuildPortHint()
        {
            string[] ports = SerialInputReader.GetAvailablePortNames();
            if (ports.Length == 0)
            {
                return string.Empty;
            }

            return "   Available: " + string.Join(", ", ports);
        }
    }
}
