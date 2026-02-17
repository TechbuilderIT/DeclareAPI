# DeclareAPI.Agent Model Benchmark Results

Date: 2026-02-17 00:49:13
Hardware: RTX 3050 6GB VRAM

## Summary

| Model | Pass Rate | First Token | Total Time | Tokens/s | Quality |
|-------|-----------|-------------|------------|----------|---------|
| qwen2.5:7b | 100% | 441ms | 16374ms | 19,5 | 97/100 |
| qwen2.5-coder:3b | 100% | 232ms | 5184ms | 63,7 | 96/100 |
| mistral:7b | 100% | 440ms | 12190ms | 23,7 | 95/100 |
| phi3:mini | 100% | 234ms | 6245ms | 55,0 | 94/100 |

## Detailed Results

### qwen2.5-coder:3b

#### ✅ Simple Entity YAML (YAML)

- Time to first token: 266ms
- Total time: 6827ms
- Tokens/sec: 64,6
- Quality: Syntax=100, Complete=100, Accuracy=100

#### ✅ Complex Entity YAML (with FK) (YAML)

- Time to first token: 235ms
- Total time: 7668ms
- Tokens/sec: 64,9
- Quality: Syntax=100, Complete=100, Accuracy=100

#### ✅ Simple Table SQL (SQL)

- Time to first token: 223ms
- Total time: 3046ms
- Tokens/sec: 62,4
- Quality: Syntax=100, Complete=90, Accuracy=85

#### ✅ Complex Table SQL (with FK) (SQL)

- Time to first token: 204ms
- Total time: 3196ms
- Tokens/sec: 62,9
- Quality: Syntax=100, Complete=90, Accuracy=90

### qwen2.5:7b

#### ✅ Simple Entity YAML (YAML)

- Time to first token: 504ms
- Total time: 17666ms
- Tokens/sec: 19,3
- Quality: Syntax=100, Complete=100, Accuracy=100

#### ✅ Complex Entity YAML (with FK) (YAML)

- Time to first token: 447ms
- Total time: 25975ms
- Tokens/sec: 19,3
- Quality: Syntax=100, Complete=100, Accuracy=100

#### ✅ Simple Table SQL (SQL)

- Time to first token: 435ms
- Total time: 10385ms
- Tokens/sec: 19,6
- Quality: Syntax=100, Complete=90, Accuracy=85

#### ✅ Complex Table SQL (with FK) (SQL)

- Time to first token: 377ms
- Total time: 11471ms
- Tokens/sec: 19,6
- Quality: Syntax=100, Complete=90, Accuracy=100

### mistral:7b

#### ✅ Simple Entity YAML (YAML)

- Time to first token: 549ms
- Total time: 18654ms
- Tokens/sec: 24,0
- Quality: Syntax=100, Complete=100, Accuracy=100

#### ✅ Complex Entity YAML (with FK) (YAML)

- Time to first token: 397ms
- Total time: 17184ms
- Tokens/sec: 23,9
- Quality: Syntax=100, Complete=100, Accuracy=100

#### ✅ Simple Table SQL (SQL)

- Time to first token: 423ms
- Total time: 6173ms
- Tokens/sec: 23,5
- Quality: Syntax=100, Complete=80, Accuracy=85

#### ✅ Complex Table SQL (with FK) (SQL)

- Time to first token: 390ms
- Total time: 6750ms
- Tokens/sec: 23,4
- Quality: Syntax=100, Complete=80, Accuracy=100

### phi3:mini

#### ✅ Simple Entity YAML (YAML)

- Time to first token: 280ms
- Total time: 6553ms
- Tokens/sec: 54,8
- Quality: Syntax=100, Complete=100, Accuracy=100

#### ✅ Complex Entity YAML (with FK) (YAML)

- Time to first token: 227ms
- Total time: 8061ms
- Tokens/sec: 54,8
- Quality: Syntax=100, Complete=100, Accuracy=100

#### ✅ Simple Table SQL (SQL)

- Time to first token: 224ms
- Total time: 5034ms
- Tokens/sec: 55,4
- Quality: Syntax=100, Complete=70, Accuracy=85

#### ✅ Complex Table SQL (with FK) (SQL)

- Time to first token: 203ms
- Total time: 5333ms
- Tokens/sec: 55,1
- Quality: Syntax=100, Complete=90, Accuracy=90

