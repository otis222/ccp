/*
  Serial Star Runner - two-button Arduino controller.

  Wiring:
  - Left button:
      one side -> D2
      other side -> GND
  - Right button:
      one side -> D3
      other side -> GND

  No potentiometer, no external LED, no resistor needed.
  Press D2 + D3 together to jump.

  Serial output sent to Unity:
      A=-1.00;J=0;D=0   left button held
      A=1.00;J=0;D=0    right button held
      A=0.00;J=1;D=0    both buttons held to jump
      A=0.00;J=0;D=0    no button held

  Unity can still send LED feedback to the built-in LED on D13:
      LED:RUN / LED:WIN / LED:LOSE / LED:HIT
      LED_ON / LED_OFF
*/

const int leftButtonPin = 2;
const int rightButtonPin = 3;
const int builtinLedPin = 13;
const int buzzerPin = 8;

// Most small Arduino buzzer modules are active buzzers: HIGH = beep.
// If your buzzer is passive piezo, set this to false to use tone().
const bool activeBuzzer = true;

const unsigned long sendIntervalMs = 33;
const unsigned long debounceMs = 18;

int lastLeftReading = HIGH;
int lastRightReading = HIGH;
int stableLeftReading = HIGH;
int stableRightReading = HIGH;
unsigned long lastLeftChangeMs = 0;
unsigned long lastRightChangeMs = 0;
unsigned long lastSendMs = 0;
unsigned long feedbackModeStartedMs = 0;
String feedbackMode = "RUN";
bool anyButtonPressed = false;
bool previousAnyButtonPressed = false;
bool previousJumpPressed = false;

void setup() {
  pinMode(leftButtonPin, INPUT_PULLUP);
  pinMode(rightButtonPin, INPUT_PULLUP);
  pinMode(builtinLedPin, OUTPUT);
  pinMode(buzzerPin, OUTPUT);

  Serial.begin(9600);
  delay(400);
  playReadyTone();
  Serial.println("READY");
}

void loop() {
  readUnityFeedback();
  updateBuiltinLed();
  sendControllerState();
}

void sendControllerState() {
  unsigned long now = millis();
  if (now - lastSendMs < sendIntervalMs) {
    return;
  }
  lastSendMs = now;

  bool leftPressed = readDebouncedButton(leftButtonPin, lastLeftReading, stableLeftReading, lastLeftChangeMs) == LOW;
  bool rightPressed = readDebouncedButton(rightButtonPin, lastRightReading, stableRightReading, lastRightChangeMs) == LOW;
  bool jumpPressed = leftPressed && rightPressed;
  anyButtonPressed = leftPressed || rightPressed;

  if (jumpPressed && !previousJumpPressed) {
    playJumpTone();
  } else if (anyButtonPressed && !previousAnyButtonPressed) {
    playButtonTone();
  }

  previousAnyButtonPressed = anyButtonPressed;
  previousJumpPressed = jumpPressed;

  float axis = 0.0;
  if (!jumpPressed && leftPressed) {
    axis = -1.0;
  } else if (!jumpPressed && rightPressed) {
    axis = 1.0;
  }

  Serial.print("A=");
  Serial.print(axis, 2);
  Serial.print(";J=");
  Serial.print(jumpPressed ? 1 : 0);
  Serial.println(";D=0");
}

int readDebouncedButton(int pin, int &lastReading, int &stableReading, unsigned long &lastChangeMs) {
  int reading = digitalRead(pin);
  unsigned long now = millis();

  if (reading != lastReading) {
    lastReading = reading;
    lastChangeMs = now;
  }

  if (now - lastChangeMs >= debounceMs) {
    stableReading = reading;
  }

  return stableReading;
}

void readUnityFeedback() {
  while (Serial.available() > 0) {
    String line = Serial.readStringUntil('\n');
    line.trim();
    line.toUpperCase();

    if (line == "LED_ON") {
      feedbackMode = "WIN";
      feedbackModeStartedMs = millis();
      playWinTone();
    } else if (line == "LED_OFF") {
      feedbackMode = "RUN";
      feedbackModeStartedMs = millis();
    } else if (line == "LED:RUN" || line == "LED:HIT" || line == "LED:WIN" || line == "LED:LOSE") {
      feedbackMode = line.substring(4);
      feedbackModeStartedMs = millis();
      if (feedbackMode == "HIT") {
        playHitTone();
      } else if (feedbackMode == "WIN") {
        playWinTone();
      } else if (feedbackMode == "LOSE") {
        playLoseTone();
      }
    }
  }
}

void updateBuiltinLed() {
  unsigned long elapsed = millis() - feedbackModeStartedMs;

  if (feedbackMode == "WIN") {
    digitalWrite(builtinLedPin, HIGH);
    return;
  }

  if (feedbackMode == "LOSE") {
    digitalWrite(builtinLedPin, millis() / 160 % 2 == 0 ? HIGH : LOW);
    return;
  }

  if (feedbackMode == "HIT" && elapsed < 900) {
    digitalWrite(builtinLedPin, millis() / 90 % 2 == 0 ? HIGH : LOW);
    return;
  }

  feedbackMode = "RUN";
  digitalWrite(builtinLedPin, anyButtonPressed ? HIGH : LOW);
}

void playBuzzer(int frequency, int durationMs) {
  if (activeBuzzer) {
    digitalWrite(buzzerPin, HIGH);
    delay(durationMs);
    digitalWrite(buzzerPin, LOW);
    return;
  }

  tone(buzzerPin, frequency, durationMs);
  delay(durationMs);
  noTone(buzzerPin);
  digitalWrite(buzzerPin, LOW);
}

void playReadyTone() {
  playBuzzer(1047, 90);
}

void playButtonTone() {
  playBuzzer(880, 70);
}

void playJumpTone() {
  playBuzzer(1175, 90);
}

void playHitTone() {
  playBuzzer(160, 140);
}

void playWinTone() {
  playBuzzer(988, 120);
  delay(80);
  playBuzzer(1319, 160);
}

void playLoseTone() {
  playBuzzer(330, 180);
  delay(80);
  playBuzzer(220, 220);
}
