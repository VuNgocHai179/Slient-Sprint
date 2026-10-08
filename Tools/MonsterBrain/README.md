# MonsterBrain — AI lai (hybrid) cho quái trong Silent-Sprint

Thư mục này chứa pipeline sinh model ONNX cho `MonsterAI`.
Model được chạy trong game bằng **Unity Inference Engine** (`com.unity.ai.inference` 2.2.1).

> **Ghi chú về tên gọi:** package này trước đây tên là **Sentis** (`com.unity.sentis`).
> Từ Unity 6, Sentis đã đổi tên thành Inference Engine: gói `com.unity.sentis` chỉ còn là
> một shim trỏ sang `com.unity.ai.inference`, và namespace trong code đổi từ `Unity.Sentis`
> sang `Unity.InferenceEngine`. Vì vậy `Packages/manifest.json` khai báo
> `com.unity.ai.inference` chứ không phải `com.unity.sentis`.

## 1. Kiến trúc

Trước đây `MonsterAI` là một máy trạng thái hữu hạn (FSM) thuần: thấy → đuổi, nghe → điều tra,
còn lại → đi tuần. Bây giờ nó được tách thành **hai lớp**:

```
                 ┌──────────────────────────────────────────┐
   observation   │  MonsterBrain  (LỚP QUYẾT ĐỊNH)         │   10 Hz
   16 chiều  ───►│  Inference Engine chạy monster_brain.onnx│──► MonsterDecision
                 │  → state_probs [3] + intents [4]         │    3 xác suất + 4 ý định
                 └──────────────────────────────────────────┘
                                  │
                                  ▼
                 ┌──────────────────────────────────────────┐
                 │  MonsterAI  (LỚP PHẢN XẠ)                │   mỗi frame
                 │  luật cứng luôn thắng brain:             │
                 │   • bắt được player khi đủ gần           │
                 │   • thấy ở cự ly sát → bắt buộc Chase     │
                 │   • nghe tiếng động ngay cạnh → Investigate│
                 │   • không thể đuổi thứ nó không nhìn thấy │
                 │   • trễ đổi trạng thái, chống rung lắc    │
                 └──────────────────────────────────────────┘
                                  │
                                  ▼
                        NavMeshAgent đi tới mục tiêu
```

**Vì sao lại là hybrid?** Model chỉ có 16 số đầu vào và chạy ở 10 Hz, nên nó không thể (và
không nên) là nguồn chân lý duy nhất. Các luật cứng trong `MonsterAI` bảo đảm quái không bao giờ
hành xử vô lý — ví dụ đuổi theo player mà nó không hề nhìn thấy. Model lo phần "cảm giác" cao cấp
hơn: sắc thái tốc độ, có nên cắt đường không, lục soát bao xa, cảnh giác đến đâu.

**Nếu thiếu model thì sao?** `MonsterBrain` tự rơi về `MonsterHeuristicPolicy` — chính sách viết
tay đúng bằng chính sách "giáo viên" đã dùng để sinh nhãn lúc train. Game không bao giờ vỡ vì
thiếu file `.onnx`; chỉ khác là quái kém "người" hơn một chút.

## 2. Giao diện model

File: `Assets/Resources/MonsterBrain/monster_brain.onnx`

| | Tên | Shape | Ghi chú |
|---|---|---|---|
| Input | `observation` | `[1, 16]` | bố cục dưới đây |
| Output | `state_probs` | `[1, 3]` | softmax: patrol, investigate, chase |
| Output | `intents` | `[1, 4]` | sigmoid: aggression, searchRadius, flank, alertness |

### 16 chiều observation

| # | Tên | Ý nghĩa |
|---|---|---|
| 0 | `distNorm` | khoảng cách tới player / tầm nhìn hiệu dụng (kẹp 0..2) |
| 1 | `angleNorm` | góc lệch so với hướng mặt / 180 |
| 2 | `losClear` | 1 nếu tia từ mắt không bị vật cản chắn |
| 3 | `inSightCone` | 1 nếu player nằm trong hình nón thị giác |
| 4–5 | `relDirX/Z` | hướng tới player trong không gian local của quái |
| 6 | `playerSpeedNorm` | tốc độ player / `runSpeed` |
| 7 | `playerNoise` | 0 đứng yên · 0.4 đi bộ · 1.0 chạy |
| 8 | `staminaNorm` | thể lực player còn lại |
| 9 | `flashlightOn` | 1 nếu đèn pin player đang bật |
| 10 | `proximity` | khoảng cách / `hearingDistance` |
| 11 | `timeSinceSeen` | thời gian từ lần cuối thấy player / 8 giây |
| 12 | `hasLastKnown` | 1 nếu còn nhớ vị trí cuối |
| 13 | `lastKnownDist` | khoảng cách tới vị trí nhớ được / `stalkRadius` |
| 14 | `prevState` | state trước đó / 2 |
| 15 | `bias` | luôn 1.0 |

Bố cục này được khai báo ở **hai nơi và phải khớp nhau**:

- C#: `Assets/Scripts/Gameplay/AI/MonsterObservation.cs` (`MonsterObservationLayout`)
- Python: `Tools/MonsterBrain/generate_monster_brain.py` (`OBS_NAMES`)

Đổi thứ tự ở một bên mà không đổi bên kia thì model sẽ nhận dữ liệu vô nghĩa.
Nhớ chạy lại script train sau khi đổi.

### 4 "ý định" (intents)

| # | Tên | Quái dùng để làm gì |
|---|---|---|
| 0 | `aggression` | Nội suy tốc độ giữa `patrolSpeed` và `chaseSpeed` → cho phép rình rập thay vì luôn chạy hết tốc |
| 1 | `searchRadius` | Bán kính lục soát quanh vị trí cuối (nhân với `maxSearchRadius`) |
| 2 | `flank` | Khi đuổi, nhắm vào **vị trí player dự đoán** (`+ playerVelocity * flankPredictionTime * flank`) để cắt đường |
| 3 | `alertness` | Kéo dài thời gian lục soát và nới tầm nhìn hiệu dụng ±12% |

## 3. Chạy lại pipeline train

Yêu cầu: Python 3.9+ với `numpy`, `torch`, `onnx`, `onnxruntime`.

```bash
python Tools/MonsterBrain/generate_monster_brain.py
```

Mặc định: 40 000 mẫu, 1 200 epoch, seed 1234, ghi đè
`Assets/Resources/MonsterBrain/monster_brain.onnx`.

Tuỳ chọn:

```bash
python Tools/MonsterBrain/generate_monster_brain.py --epochs 3000 --samples 80000 --seed 7
python Tools/MonsterBrain/generate_monster_brain.py --out /tmp/thu.onnx   # thử nghiệm, không đụng vào Assets
```

Script tự kiểm tra ở cuối và **thoát với mã lỗi khác 0** nếu:

- `onnx.checker` không qua,
- kết quả `onnxruntime` lệch quá `1e-4` so với forward pass numpy,
- `state_probs` không cộng lại bằng 1,
- `intents` rơi ra ngoài `[0, 1]`.

## 4. Thay bằng model khác

Model mới chỉ cần đúng giao diện ở mục 2 là dùng được ngay — không phải sửa code C#.

Hai cách:

1. **Thay file**: ghi đè `Assets/Resources/MonsterBrain/monster_brain.onnx`.
2. **Đổi đường dẫn**: sửa `modelResourcePath` trên component `MonsterBrain`, hoặc gán thẳng
   asset vào ô `brainModel` trong Inspector.

Model được nạp tự động trong `Start()`. Nếu nạp lỗi (sai tên input/output, shape sai, thiếu
operator), `MonsterBrain` ghi `Debug.LogError` rồi chuyển sang chính sách viết tay — game vẫn chạy.

Sau này nếu muốn dùng model **học tăng cường thật** (self-play, không cần teacher) thì chỉ cần
train ra mạng có cùng input/output rồi thay file; toàn bộ phần còn lại giữ nguyên.

## 5. Cấu hình trong Inspector

Component `MonsterBrain` (thêm vào cùng GameObject với `MonsterAI`):

| Trường | Mặc định | Ghi chú |
|---|---|---|
| `modelResourcePath` | `MonsterBrain/monster_brain` | đường dẫn trong `Resources`, không có đuôi file |
| `brainModel` | *(trống)* | gán trực tiếp để ưu tiên hơn `modelResourcePath` |
| `backendType` | `CPU` | CPU ổn định và không gây stall do đọc ngược từ GPU. Đổi sang `GPUCompute` nếu model lớn hơn nhiều |
| `inferenceInterval` | `0.1` | giây giữa hai lần suy luận (10 Hz) |
| `logDecisions` | `false` | in xác suất + intents ra Console mỗi lần suy luận |

Trên `MonsterAI` có thêm nhóm **Reflex Layer**:

| Trường | Mặc định | Ghi chú |
|---|---|---|
| `pointBlankChaseFactor` | `0.4` | thấy player trong `sightDistance × hệ số này` → bắt buộc Chase |
| `closeAudibleRange` | `0.5` | nghe tiếng động trong `hearingDistance × hệ số này` → ít nhất Investigate |
| `stateSwitchMargin` | `0.15` | biên độ ưu tiên khi đổi trạng thái, chống rung lắc |

`MonsterAI` tự tìm `MonsterBrain` trên cùng GameObject, và tự thêm vào nếu chưa có — nên prefab
`Monster` hiện tại chạy được ngay mà không cần sửa gì. Thêm component vào prefab để cấu hình được
trong Inspector.

> ⚠️ **`obstacleMask` (trên `MonsterAI`)**: phải là layer của **vật cản tầm nhìn** — tường, cửa,
> địa hình — hiện đặt là `Default | Obstacle` (`m_Bits: 129`). **Tuyệt đối không để layer `Player`
> trong mask này.** Tia kiểm tra tầm nhìn đi từ mắt quái tới người player, nên nếu Player nằm trong
> mask thì tia luôn "đụng" chính player ngay khi tới đích, `losClear` luôn bằng 0, quái không bao
> giờ thấy ai → không đuổi, không bắt được, không có jumpscare (đây chính là lỗi cũ: `m_Bits: 64`
> = chỉ layer Player). `MonsterPerception` có bỏ qua collider của chính quái và của player khi kiểm
> tra, nhưng mask vẫn phải chứa layer môi trường thì tường mới chặn được tầm nhìn.

## 6. Ghi chú

- Model hiện tại là **behaviour cloning**: nó bắt chước chính sách viết tay ở trên, chứ chưa tự
  học ra chiến thuật mới. Nó "thông minh hơn" ở chỗ liên tục hoá được nhiều tín hiệu cùng lúc
  (tốc độ, thể lực, đèn pin, trí nhớ, khoảng cách) và cho ra sắc thái hành vi mà FSM cứng trước
  đây không biểu diễn được — đồng thời vẫn thay được bằng model train thật sau này.
- Bố cục 16 chiều cố tình **không** đưa vào thông tin bản đồ. Nếu muốn quái học chiến thuật phức
  tạp hơn (chặn cửa, phục kích theo phòng), hãy thêm đặc trưng dạng đấy rồi train lại.
- Chỉ chạy **một** `MonsterBrain` cho mỗi quái. Nếu sau này có nhiều quái, chi phí suy luận tăng
  tuyến tính theo số lượng nhưng vẫn rất nhẹ (mạng 16→32→32→7, khoảng 1.5 nghìn tham số).
