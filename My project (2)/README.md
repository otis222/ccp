# Arduino x Unity 小遊戲：Serial Star Runner

這是一個 Arduino 實體控制器 x Unity 的跑酷收集遊戲。玩家控制角色往前跑，收集能量核心、避開障礙，最後進入終點門。

已整合 `sableangle/MyProject_B` 的 X Bot 模型與 Idle / Walking / Jumping 動畫。原本的遊戲規則、收集物、障礙物、HUD 和 Serial 輸入系統保留。

## 玩法

- 收集 8 個黃色能量核心，每個 10 分。
- 撞到紅色障礙會扣 1 HP，Unity 會回傳訊號給 Arduino LED / 蜂鳴器。
- 收集全部核心後進入綠色終點門即可獲勝。
- 勾選 HUD 右下角的 `Loop finish` 後，進入終點門會重生回起點並開始下一圈。
- HP 歸零、時間歸零或掉出跑道會失敗。
- 勝利或失敗後，按跳躍或鍵盤 `R` 重新開始。

## Arduino 接線

程式位置：

```text
Arduino/SerialStarRunner/SerialStarRunner.ino
```

接線只需要兩顆按鈕：

- D2：左移按鈕，另一端接 GND。
- D3：右移按鈕，另一端接 GND。
- D8：蜂鳴器正極，負極接 GND。
- D13：使用 Arduino 內建 LED，不需要另外接 LED。

按鈕使用 `INPUT_PULLUP`，所以按下時讀值為 `LOW`。

蜂鳴器程式預設為主動蜂鳴器，也就是 D8 輸出 HIGH 時會叫。如果你用的是被動蜂鳴片，把 `Arduino/SerialStarRunner/SerialStarRunner.ino` 裡的 `activeBuzzer` 改成 `false`。

## Arduino 操作

- 只按 D2：向左移動。
- 只按 D3：向右移動。
- 同時按 D2 + D3：跳躍。

Arduino 傳給 Unity 的格式：

```text
A=-1.00;J=0;D=0
A=1.00;J=0;D=0
A=0.00;J=1;D=0
```

- `A`：左右方向，範圍 `-1.00` 到 `1.00`。
- `J`：跳躍，`1` 代表按下。
- `D`：衝刺欄位，目前保留為 `0`。

Unity 回傳 Arduino 的格式：

```text
LED:RUN
LED:HIT
LED:WIN
LED:LOSE
LED_ON
LED_OFF
```

## Unity 操作

1. 用 Unity 6000.0.60f1 或相近版本開啟此資料夾。
2. 開啟 `Assets/Scenes/SampleScene.unity`。
3. 將 Arduino sketch 上傳到板子。
4. 按 Play。
5. 在右下角輸入 Arduino 的 COM port，例如 `COM3`，按 `Connect`。

沒有 Arduino 時也可以用鍵盤測試：

- `A` / `D` 或方向鍵：左右移動。
- `Space`：跳躍。
- `Shift`：衝刺。
- `R`：勝利或失敗後重新開始。

## MyProject_B 整合內容

- `Assets/Models`：匯入 X Bot 與 Idle / Walking / Jumping FBX。
- `Assets/AnimatorController/XBot.controller`：匯入動畫狀態機。
- `SerialStarRunnerPlayer`：會驅動 `IsWalking` 和 `Jump` Animator 參數。
- `SerialStarRunnerBootstrap`：會自動把 X Bot 掛到目前的 runner 玩家物件上；若素材不存在，會退回原本幾何體玩家。
