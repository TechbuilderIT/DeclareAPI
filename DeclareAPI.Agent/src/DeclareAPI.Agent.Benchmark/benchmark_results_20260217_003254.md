# DeclareAPI.Agent Model Benchmark Results

Date: 2026-02-17 00:32:54
Hardware: RTX 3050 6GB VRAM

## Summary

| Model | Pass Rate | First Token | Total Time | Tokens/s | Quality |
|-------|-----------|-------------|------------|----------|---------|
| phi3:mini | 100% | 262ms | 6773ms | 51,9 | 96/100 |

## Detailed Results

### phi3:mini

#### ✅ Simple Entity YAML (YAML)

- Time to first token: 294ms
- Total time: 9505ms
- Tokens/sec: 51,6
- Quality: Syntax=100, Complete=100, Accuracy=100

#### ✅ Simple Table SQL (SQL)

- Time to first token: 231ms
- Total time: 4042ms
- Tokens/sec: 52,2
- Quality: Syntax=100, Complete=90, Accuracy=85

