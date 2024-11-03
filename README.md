# fbi

> **f**ile **b**ifurcation **i**nterface; high-performance fuzzy diff/patch implementation for .NET.

---

**fbi** is fuzzy diff library implementing APIs for creating diffs and applying them as patches for .NET, directly inspired by and originally based on [Chicken-Bones/DiffPatch](https://github.com/Chicken-Bones/DiffPatch).

This project *works*, and, from my testing, it works *well*. That being said, the API is not finalized and is subject to change, and while it is perfectly usable in projects, API guarantees are not guaranteed until a proper release cycle is entered.

At the time of writing, this project is ~3.33x faster than DiffPatch and consumes ~0.5x the memory:

```
| Method          | Mean       | Error    | StdDev   | Gen0        | Gen1       | Gen2       | Allocated  |
|---------------- |-----------:|---------:|---------:|------------:|-----------:|-----------:|-----------:|
| DiffFbi         |   309.6 ms |  5.92 ms |  7.27 ms |  61000.0000 | 29000.0000 | 10000.0000 |  582.98 MB |
| DiffCodeChicken | 1,032.9 ms | 20.26 ms | 46.95 ms | 151000.0000 | 51000.0000 |  3000.0000 | 1243.09 MB |
```
