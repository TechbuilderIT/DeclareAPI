# DeclareAPI.Agent Model Benchmark Results

Date: 2026-02-17 00:39:30
Hardware: RTX 3050 6GB VRAM

## Summary

| Model | Pass Rate | First Token | Total Time | Tokens/s | Quality |
|-------|-----------|-------------|------------|----------|---------|
| qwen2.5:7b | 100% | 488ms | 21592ms | 17,0 | 96/100 |
| mistral:7b | 100% | 476ms | 17050ms | 20,8 | 95/100 |
| llama2:7b-chat | 100% | 652ms | 30846ms | 14,0 | 93/100 |
| phi3:mini | 100% | 239ms | 6705ms | 52,6 | 76/100 |

## Detailed Results

### phi3:mini

#### ✅ Simple Entity YAML (YAML)

- Time to first token: 279ms
- Total time: 3073ms
- Tokens/sec: 52,1
- Quality: Syntax=60, Complete=10, Accuracy=0

#### ✅ Complex Entity YAML (with FK) (YAML)

- Time to first token: 228ms
- Total time: 5433ms
- Tokens/sec: 51,9
- Quality: Syntax=90, Complete=95, Accuracy=100

#### ✅ Simple Table SQL (SQL)

- Time to first token: 231ms
- Total time: 3232ms
- Tokens/sec: 53,5
- Quality: Syntax=100, Complete=90, Accuracy=85

#### ✅ Complex Table SQL (with FK) (SQL)

- Time to first token: 217ms
- Total time: 15082ms
- Tokens/sec: 52,7
- Quality: Syntax=100, Complete=90, Accuracy=90

### qwen2.5:7b

#### ✅ Simple Entity YAML (YAML)

- Time to first token: 543ms
- Total time: 29611ms
- Tokens/sec: 18,1
- Quality: Syntax=100, Complete=95, Accuracy=100

#### ✅ Complex Entity YAML (with FK) (YAML)

- Time to first token: 473ms
- Total time: 31542ms
- Tokens/sec: 16,4
- Quality: Syntax=100, Complete=100, Accuracy=100

#### ✅ Simple Table SQL (SQL)

- Time to first token: 508ms
- Total time: 12307ms
- Tokens/sec: 16,8
- Quality: Syntax=100, Complete=90, Accuracy=85

#### ✅ Complex Table SQL (with FK) (SQL)

- Time to first token: 427ms
- Total time: 12908ms
- Tokens/sec: 16,9
- Quality: Syntax=100, Complete=90, Accuracy=100

### mistral:7b

#### ✅ Simple Entity YAML (YAML)

- Time to first token: 599ms
- Total time: 16424ms
- Tokens/sec: 21,4
- Quality: Syntax=100, Complete=100, Accuracy=100

#### ✅ Complex Entity YAML (with FK) (YAML)

- Time to first token: 428ms
- Total time: 36235ms
- Tokens/sec: 21,1
- Quality: Syntax=100, Complete=100, Accuracy=100

#### ✅ Simple Table SQL (SQL)

- Time to first token: 444ms
- Total time: 5758ms
- Tokens/sec: 19,3
- Quality: Syntax=100, Complete=80, Accuracy=85

#### ✅ Complex Table SQL (with FK) (SQL)

- Time to first token: 433ms
- Total time: 9786ms
- Tokens/sec: 21,4
- Quality: Syntax=100, Complete=80, Accuracy=100

### llama2:7b-chat

#### ✅ Simple Entity YAML (YAML)

- Time to first token: 813ms
- Total time: 47040ms
- Tokens/sec: 13,8
- Quality: Syntax=100, Complete=100, Accuracy=100

#### ✅ Complex Entity YAML (with FK) (YAML)

- Time to first token: 576ms
- Total time: 42153ms
- Tokens/sec: 14,0
- Quality: Syntax=90, Complete=100, Accuracy=100

#### ✅ Simple Table SQL (SQL)

- Time to first token: 624ms
- Total time: 12834ms
- Tokens/sec: 14,3
- Quality: Syntax=100, Complete=90, Accuracy=85

#### ✅ Complex Table SQL (with FK) (SQL)

- Time to first token: 594ms
- Total time: 21356ms
- Tokens/sec: 13,8
- Quality: Syntax=100, Complete=70, Accuracy=90

